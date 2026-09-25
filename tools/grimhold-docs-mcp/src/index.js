import { McpServer } from '@modelcontextprotocol/server';
import { serveStdio } from '@modelcontextprotocol/server/stdio';
import * as z from 'zod/v4';
import { authorizeGoogleDrive } from './auth.js';
import { loadConfig } from './config.js';
import { DocumentRepository } from './documentRepository.js';
import { extractHeadings, extractSection } from './markdown.js';
import { searchDocuments } from './search.js';

const MAX_DOCUMENT_CHARS = 100_000;

function textResult(value) {
  return { content: [{ type: 'text', text: value }] };
}

function errorResult(error) {
  return {
    content: [{ type: 'text', text: error instanceof Error ? error.message : String(error) }],
    isError: true,
  };
}

function formatCatalog(documents) {
  return JSON.stringify(
    documents.map(document => ({
      id: document.id,
      title: document.title,
      path: document.path,
      modifiedTime: document.modifiedTime,
      url: document.url,
    })),
    null,
    2,
  );
}

function formatSearchResults(results) {
  return JSON.stringify(
    results.map(result => ({
      id: result.id,
      title: result.title,
      path: result.path,
      modifiedTime: result.modifiedTime,
      url: result.url,
      score: result.score,
      matchingHeadings: result.matchingHeadings,
      snippets: result.snippets,
    })),
    null,
    2,
  );
}

async function createServer() {
  const config = loadConfig();
  const auth = await authorizeGoogleDrive(config);
  const repository = new DocumentRepository({ auth, ...config });
  const server = new McpServer({ name: 'grimhold-docs', version: '0.1.0' });

  server.registerTool(
    'list_documents',
    {
      description: 'List the current authoritative Project Grimhold Google Docs available under the configured Drive documentation tree.',
      annotations: {
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: true,
      },
      inputSchema: z.object({
        force_refresh: z.boolean().optional().default(false).describe('Refresh Drive metadata instead of using the short-lived catalog cache.'),
      }),
    },
    async ({ force_refresh }) => {
      try {
        return textResult(formatCatalog(await repository.listDocuments({ forceRefresh: force_refresh })));
      } catch (error) {
        return errorResult(error);
      }
    },
  );

  server.registerTool(
    'search_documents',
    {
      description: 'Search authoritative Project Grimhold documentation by title, headings and current document content. Returns ranked metadata and compact snippets, not full documents.',
      annotations: {
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: true,
      },
      inputSchema: z.object({
        query: z.string().min(2).describe('Gameplay concept, rule, system or responsibility to find.'),
        limit: z.number().int().min(1).max(10).optional().default(5),
      }),
    },
    async ({ query, limit }) => {
      try {
        const documents = await repository.getAllDocumentsWithContent();
        return textResult(formatSearchResults(searchDocuments(documents, query, limit)));
      } catch (error) {
        return errorResult(error);
      }
    },
  );

  server.registerTool(
    'get_document_outline',
    {
      description: 'Read only the heading outline of one authoritative Project Grimhold document before requesting larger sections.',
      annotations: {
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: true,
      },
      inputSchema: z.object({
        document_id: z.string().min(1),
      }),
    },
    async ({ document_id }) => {
      try {
        const document = await repository.getDocument(document_id);
        return textResult(JSON.stringify({
          id: document.id,
          title: document.title,
          path: document.path,
          modifiedTime: document.modifiedTime,
          url: document.url,
          headings: extractHeadings(document.markdown),
        }, null, 2));
      } catch (error) {
        return errorResult(error);
      }
    },
  );

  server.registerTool(
    'get_document',
    {
      description: 'Read the current content of one authoritative Project Grimhold document. Provide heading to return only one section and reduce context usage.',
      annotations: {
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: true,
      },
      inputSchema: z.object({
        document_id: z.string().min(1),
        heading: z.string().min(1).optional().describe('Optional heading text. When provided, returns that section only.'),
        max_chars: z.number().int().min(1000).max(MAX_DOCUMENT_CHARS).optional().default(50_000),
      }),
    },
    async ({ document_id, heading, max_chars }) => {
      try {
        const document = await repository.getDocument(document_id);
        let selectedContent = document.markdown;
        let selectedHeading = null;

        if (heading) {
          const section = extractSection(document.markdown, heading);
          if (!section) {
            return {
              ...errorResult(new Error(`Heading not found: ${heading}`)),
              content: [{
                type: 'text',
                text: `Heading not found: ${heading}\n\nAvailable headings:\n${extractHeadings(document.markdown).map(item => `- ${item.title}`).join('\n')}`,
              }],
            };
          }

          selectedContent = section.content;
          selectedHeading = section.heading;
        }

        const truncated = selectedContent.length > max_chars;
        const content = truncated
          ? `${selectedContent.slice(0, max_chars).trimEnd()}\n\n[TRUNCATED]`
          : selectedContent;

        const metadata = [
          `Title: ${document.title}`,
          `Document ID: ${document.id}`,
          `Path: ${document.path || '/'}`,
          `Modified: ${document.modifiedTime ?? 'unknown'}`,
          `URL: ${document.url}`,
          `Export format: ${document.exportMimeType}`,
          selectedHeading ? `Section: ${selectedHeading}` : null,
          `Truncated: ${truncated}`,
        ].filter(Boolean).join('\n');

        return textResult(`${metadata}\n\n${content}`);
      } catch (error) {
        return errorResult(error);
      }
    },
  );

  return server;
}

void serveStdio(createServer);
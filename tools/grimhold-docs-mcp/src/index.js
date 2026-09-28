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
      description: 'List every authoritative Project Grimhold Google Doc under the configured Drive documentation tree, with id, title, path, modified time and URL. Returns metadata only, no content. To find which document owns a rule, prefer search_documents; use this to browse the catalog or confirm a document exists.',
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
      description: 'Find which authoritative Project Grimhold documents own a gameplay concept. Matching is keyword-based, not semantic: an exact phrase found in a title, heading or body ranks highest, then individual word matches, ignoring case and accents. Use the terms the design documents themselves use. Returns up to limit results with id, title, path, modified time, URL, score, matching headings and short snippets, never full content; follow up with get_document_outline or get_document using the returned id.',
      annotations: {
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: true,
      },
      inputSchema: z.object({
        query: z.string().min(2).describe('Gameplay concept, rule, system or responsibility to find.'),
        limit: z.number().int().min(1).max(10).optional().default(5).describe('Maximum number of ranked documents to return (1-10, default 5).'),
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
      description: 'Read the heading outline of one authoritative Project Grimhold document, without its body text. Returns id, title, path, modified time, URL and the list of headings. Use it to pick an exact heading before calling get_document with heading, when a document is too large to read whole.',
      annotations: {
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: true,
      },
      inputSchema: z.object({
        document_id: z.string().min(1).describe('Document id as returned in the id field of search_documents or list_documents.'),
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
      description: 'Read the current Markdown content of one authoritative Project Grimhold document, preceded by metadata (title, id, path, modified time, URL, truncation flag). Pass heading to return a single section: it matches the first heading whose text contains the given text, ignoring case, accents and punctuation, and returns that heading with all of its subsections. If no heading matches, the call fails and lists the available headings. Content longer than max_chars is cut and ends with [TRUNCATED]; prefer requesting a narrower section over raising max_chars.',
      annotations: {
        readOnlyHint: true,
        destructiveHint: false,
        idempotentHint: true,
        openWorldHint: true,
      },
      inputSchema: z.object({
        document_id: z.string().min(1).describe('Document id as returned in the id field of search_documents or list_documents.'),
        heading: z.string().min(1).optional().describe('Text contained in the target heading (case-, accent- and punctuation-insensitive). The first matching heading wins, so use enough words to be unique. Get exact headings from get_document_outline.'),
        max_chars: z.number().int().min(1000).max(MAX_DOCUMENT_CHARS).optional().default(50_000).describe('Maximum characters of content returned (1,000-100,000, default 50,000). Longer content is truncated and marked [TRUNCATED].'),
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
import { google } from 'googleapis';

const FOLDER_MIME_TYPE = 'application/vnd.google-apps.folder';
const DOCUMENT_MIME_TYPE = 'application/vnd.google-apps.document';
const MARKDOWN_EXPORT_MIME_TYPE = 'text/markdown';
const TEXT_EXPORT_MIME_TYPE = 'text/plain';

function asText(data) {
  if (typeof data === 'string') {
    return data;
  }

  if (Buffer.isBuffer(data)) {
    return data.toString('utf8');
  }

  if (data instanceof ArrayBuffer) {
    return Buffer.from(data).toString('utf8');
  }

  return String(data ?? '');
}

export class DocumentRepository {
  #drive;
  #rootFolderId;
  #excludedFolderIds;
  #catalogTtlMs;
  #catalog = [];
  #catalogLoadedAt = 0;
  #contentCache = new Map();

  constructor({ auth, rootFolderId, excludedFolderIds, catalogTtlMs }) {
    this.#drive = google.drive({ version: 'v3', auth });
    this.#rootFolderId = rootFolderId;
    this.#excludedFolderIds = excludedFolderIds;
    this.#catalogTtlMs = catalogTtlMs;
  }

  async listDocuments({ forceRefresh = false } = {}) {
    const isFresh = Date.now() - this.#catalogLoadedAt < this.#catalogTtlMs;
    if (!forceRefresh && isFresh) {
      return this.#catalog;
    }

    const documents = [];
    const pendingFolders = [{ id: this.#rootFolderId, path: '' }];

    while (pendingFolders.length > 0) {
      const folder = pendingFolders.shift();
      if (this.#excludedFolderIds.has(folder.id)) {
        continue;
      }

      let pageToken;
      do {
        const response = await this.#drive.files.list({
          q: `'${folder.id}' in parents and trashed = false`,
          spaces: 'drive',
          pageSize: 1000,
          pageToken,
          supportsAllDrives: true,
          includeItemsFromAllDrives: true,
          fields: 'nextPageToken, files(id,name,mimeType,modifiedTime,webViewLink)',
        });

        for (const file of response.data.files ?? []) {
          if (!file.id || !file.name || !file.mimeType) {
            continue;
          }

          if (file.mimeType === FOLDER_MIME_TYPE) {
            if (!this.#excludedFolderIds.has(file.id)) {
              pendingFolders.push({
                id: file.id,
                path: folder.path ? `${folder.path}/${file.name}` : file.name,
              });
            }
            continue;
          }

          if (file.mimeType !== DOCUMENT_MIME_TYPE) {
            continue;
          }

          documents.push(Object.freeze({
            id: file.id,
            title: file.name,
            modifiedTime: file.modifiedTime ?? null,
            url: file.webViewLink ?? `https://docs.google.com/document/d/${file.id}/edit`,
            path: folder.path,
          }));
        }

        pageToken = response.data.nextPageToken ?? undefined;
      } while (pageToken);
    }

    documents.sort((a, b) => a.title.localeCompare(b.title, 'es'));
    this.#catalog = Object.freeze(documents);
    this.#catalogLoadedAt = Date.now();
    this.#dropStaleContent();
    return this.#catalog;
  }

  async getDocument(documentId) {
    const catalog = await this.listDocuments();
    const metadata = catalog.find(document => document.id === documentId);
    if (!metadata) {
      throw new Error(`Document ${documentId} is not inside the configured authoritative documentation tree.`);
    }

    const cached = this.#contentCache.get(documentId);
    if (cached?.modifiedTime === metadata.modifiedTime) {
      return { ...metadata, markdown: cached.markdown, exportMimeType: cached.exportMimeType };
    }

    const exported = await this.#exportDocument(documentId);
    this.#contentCache.set(documentId, {
      modifiedTime: metadata.modifiedTime,
      markdown: exported.content,
      exportMimeType: exported.mimeType,
    });

    return {
      ...metadata,
      markdown: exported.content,
      exportMimeType: exported.mimeType,
    };
  }

  async getAllDocumentsWithContent() {
    const catalog = await this.listDocuments();
    return Promise.all(catalog.map(document => this.getDocument(document.id)));
  }

  async #exportDocument(documentId) {
    try {
      const response = await this.#drive.files.export(
        { fileId: documentId, mimeType: MARKDOWN_EXPORT_MIME_TYPE },
        { responseType: 'text' },
      );

      return { content: asText(response.data), mimeType: MARKDOWN_EXPORT_MIME_TYPE };
    } catch (markdownError) {
      try {
        const response = await this.#drive.files.export(
          { fileId: documentId, mimeType: TEXT_EXPORT_MIME_TYPE },
          { responseType: 'text' },
        );

        return { content: asText(response.data), mimeType: TEXT_EXPORT_MIME_TYPE };
      } catch (textError) {
        throw new Error(
          `Failed to export document ${documentId}. Markdown error: ${markdownError.message}. Text error: ${textError.message}.`,
        );
      }
    }
  }

  #dropStaleContent() {
    const current = new Map(this.#catalog.map(document => [document.id, document.modifiedTime]));

    for (const [documentId, cached] of this.#contentCache.entries()) {
      if (!current.has(documentId) || current.get(documentId) !== cached.modifiedTime) {
        this.#contentCache.delete(documentId);
      }
    }
  }
}

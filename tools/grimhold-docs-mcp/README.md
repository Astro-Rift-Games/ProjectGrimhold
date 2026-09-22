# Grimhold Docs MCP

Read-only MCP server that exposes the current Project Grimhold Google Drive documentation to local coding agents without duplicating the document catalog in `AGENTS.md`.

## Scope

The server is intentionally narrow:

- Reads Google Docs below one configured Drive root folder.
- Recurses through subfolders.
- Excludes configured folders such as `BORRADORES`.
- Never writes, renames, moves, shares or deletes Drive content.
- Caches document bodies by Drive `modifiedTime`.
- Uses compact search results and section reads to reduce model context usage.

## Tools

### `list_documents`

Lists authoritative documents with ID, title, Drive path, modification time and URL.

### `search_documents`

Searches titles, headings and current document bodies. Returns ranked metadata, matching headings and short snippets instead of full documents.

### `get_document_outline`

Returns only headings for one document. Use this before reading a large document when the needed section is not known.

### `get_document`

Reads one document. Passing `heading` returns only that Markdown section. `max_chars` bounds the response.

## Requirements

- Node.js 20 or newer.
- A Google Cloud project with Google Drive API enabled.
- OAuth 2.0 Desktop credentials for a Google account that can read the Project Grimhold documentation folder.

The MCP TypeScript/JavaScript server SDK v2 is ESM-only and supports local process-spawned integrations through stdio. This project therefore runs as an ESM Node process over stdio.

## Google OAuth setup

1. In Google Cloud Console, create or select a project.
2. Enable **Google Drive API**.
3. Configure the OAuth consent screen.
4. Create an **OAuth client ID** with application type **Desktop app**.
5. Download the credentials JSON and save it as `credentials.json` in this directory.
6. Copy `.env.example` to `.env`.
7. Run `npm install`.
8. Run `npm run auth` once and complete the browser authorization flow.

The server requests only:

```text
https://www.googleapis.com/auth/drive.readonly
```

`credentials.json`, `.env`, `.auth/` and `node_modules/` are ignored by Git.

## Project Grimhold configuration

`.env.example` currently points at the authoritative Game Design root folder:

```text
15hA44M-8_9rO_YXn5IQANWSn91JVkYMp
```

The current `BORRADORES` folder is excluded by ID so backup or draft documents cannot outrank live documents in search.

The server does not hardcode individual document IDs or titles. Adding a new Google Doc under the configured tree makes it discoverable automatically after the catalog cache refreshes.

## Run

```bash
npm start
```

The process communicates over stdin/stdout. Operational logs use stderr so they do not corrupt MCP JSON-RPC traffic.

A generic MCP client entry is:

```json
{
  "mcpServers": {
    "grimhold-docs": {
      "command": "node",
      "args": ["C:/ABSOLUTE/PATH/TO/tools/grimhold-docs-mcp/src/index.js"],
      "env": {
        "GRIMHOLD_DRIVE_ROOT_FOLDER_ID": "15hA44M-8_9rO_YXn5IQANWSn91JVkYMp",
        "GRIMHOLD_DRIVE_EXCLUDED_FOLDER_IDS": "1ghPbMxzaZRGMRasaBvldj1Pcnb0SgyT1",
        "GOOGLE_OAUTH_CREDENTIALS_PATH": "C:/ABSOLUTE/PATH/TO/tools/grimhold-docs-mcp/credentials.json",
        "GOOGLE_OAUTH_TOKEN_PATH": "C:/ABSOLUTE/PATH/TO/tools/grimhold-docs-mcp/.auth/token.json"
      }
    }
  }
}
```

Use the exact configuration surface required by the host that launches the MCP process.

## Validation

Pure search and Markdown sectioning logic can be validated without Google credentials:

```bash
npm test
npm run check
```

The Drive integration requires `npm install`, OAuth credentials and live access to the configured folder.

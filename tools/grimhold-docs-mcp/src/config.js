import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { config as loadDotEnv } from 'dotenv';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export const MCP_ROOT = path.resolve(__dirname, '..');

loadDotEnv({
  path: path.join(MCP_ROOT, '.env'),
  quiet: true,
});

function resolveFromMcpRoot(value) {
  if (!value) {
    return value;
  }

  return path.isAbsolute(value)
    ? value
    : path.resolve(MCP_ROOT, value);
}

const DEFAULT_CATALOG_TTL_MS = 60_000;

function parseCsv(value) {
  return new Set(
    (value ?? '')
      .split(',')
      .map(item => item.trim())
      .filter(Boolean),
  );
}

function parsePositiveInteger(value, fallback) {
  if (!value) {
    return fallback;
  }

  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    throw new Error(`Expected a positive integer but received: ${value}`);
  }

  return parsed;
}

export function loadConfig(env = process.env) {
  const rootFolderId = env.GRIMHOLD_DRIVE_ROOT_FOLDER_ID?.trim();
  if (!rootFolderId) {
    throw new Error('GRIMHOLD_DRIVE_ROOT_FOLDER_ID is required.');
  }

  return Object.freeze({
    rootFolderId,
    excludedFolderIds: parseCsv(env.GRIMHOLD_DRIVE_EXCLUDED_FOLDER_IDS),
    credentialsPath: resolveFromMcpRoot(env.GOOGLE_OAUTH_CREDENTIALS_PATH),
    tokenPath: resolveFromMcpRoot(env.GOOGLE_OAUTH_TOKEN_PATH),
    catalogTtlMs: parsePositiveInteger(env.GRIMHOLD_DOCS_CATALOG_TTL_MS, DEFAULT_CATALOG_TTL_MS),
  });
}

import fs from 'node:fs/promises';
import path from 'node:path';
import { authenticate } from '@google-cloud/local-auth';
import { google } from 'googleapis';

const DRIVE_READONLY_SCOPE = 'https://www.googleapis.com/auth/drive.readonly';

async function loadSavedCredentials(tokenPath) {
  try {
    const raw = await fs.readFile(tokenPath, 'utf8');
    return google.auth.fromJSON(JSON.parse(raw));
  } catch (error) {
    if (error?.code === 'ENOENT') {
      return null;
    }

    throw error;
  }
}

async function readOAuthClient(credentialsPath) {
  const raw = await fs.readFile(credentialsPath, 'utf8');
  const credentials = JSON.parse(raw);
  const client = credentials.installed ?? credentials.web;

  if (!client?.client_id || !client?.client_secret) {
    throw new Error('OAuth credentials must contain an installed or web client with client_id and client_secret.');
  }

  return client;
}

async function saveCredentials(authClient, credentialsPath, tokenPath) {
  const refreshToken = authClient.credentials?.refresh_token;
  if (!refreshToken) {
    throw new Error('Google OAuth did not return a refresh token; cannot persist authentication.');
  }

  const oauthClient = await readOAuthClient(credentialsPath);
  const payload = {
    type: 'authorized_user',
    client_id: oauthClient.client_id,
    client_secret: oauthClient.client_secret,
    refresh_token: refreshToken,
  };

  await fs.mkdir(path.dirname(tokenPath), { recursive: true });
  await fs.writeFile(tokenPath, JSON.stringify(payload, null, 2), { mode: 0o600 });
}

export async function authorizeGoogleDrive({ credentialsPath, tokenPath }) {
  const savedClient = await loadSavedCredentials(tokenPath);
  if (savedClient) {
    return savedClient;
  }

  const authClient = await authenticate({
    scopes: [DRIVE_READONLY_SCOPE],
    keyfilePath: credentialsPath,
  });

  await saveCredentials(authClient, credentialsPath, tokenPath);
  return authClient;
}

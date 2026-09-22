import { authorizeGoogleDrive } from './auth.js';
import { loadConfig } from './config.js';

try {
  const config = loadConfig();
  await authorizeGoogleDrive(config);
  console.error(`Google Drive authentication ready. Token: ${config.tokenPath}`);
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
}

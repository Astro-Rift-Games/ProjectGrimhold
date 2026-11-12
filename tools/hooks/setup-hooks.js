#!/usr/bin/env node
const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const repoRoot = path.resolve(__dirname, '..', '..');
const hooksDir = path.join(repoRoot, '.githooks');
const prePushPath = path.join(hooksDir, 'pre-push');

console.log('[GitHooks] Configuring local git hooks for Project Grimhold...');

if (!fs.existsSync(hooksDir)) {
  fs.mkdirSync(hooksDir, { recursive: true });
}

if (!fs.existsSync(prePushPath)) {
  console.error(`[GitHooks] Error: ${prePushPath} does not exist.`);
  process.exit(1);
}

try {
  // Configure git to use .githooks directory
  execSync('git config core.hooksPath .githooks', { cwd: repoRoot, stdio: 'inherit' });
  console.log('[GitHooks] Successfully set core.hooksPath to .githooks');

  // On POSIX systems, ensure executable permissions
  if (process.platform !== 'win32') {
    try {
      fs.chmodSync(prePushPath, '755');
      console.log('[GitHooks] Granted executable permission to .githooks/pre-push');
    } catch (chmodErr) {
      console.warn(`[GitHooks] Warning: Could not chmod .githooks/pre-push: ${chmodErr.message}`);
    }
  }

  console.log('[GitHooks] Hooks path configured. The pre-push gate is disabled by default;');
  console.log('[GitHooks] set GRIMHOLD_PREPUSH_GATE=1 to run it (secrets, backend suites, Unity EditMode subset).');
} catch (err) {
  console.error('[GitHooks] Failed to configure git hooks:', err.message);
  process.exit(1);
}

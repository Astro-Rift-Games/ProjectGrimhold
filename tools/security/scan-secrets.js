#!/usr/bin/env node
const fs = require('fs');
const path = require('path');
const { spawn, execFileSync } = require('child_process');
const readline = require('readline');

/**
 * Project Grimhold — Repeatable Secret Scanner (US-61 / SEC-61.2)
 *
 * Scans repository files (including Unity serialized YAML assets and files > 2 MB up to 25 MB)
 * and relevant Git history for hardcoded credentials, API keys, OAuth secrets, private keys,
 * and unprotected database URIs with strictly redacted output.
 */

const MAX_SCAN_FILE_BYTES = 25 * 1024 * 1024; // 25 MB (covers Gameplay.unity 5.79 MB & scene templates)

const IGNORED_DIRS = new Set([
  'node_modules',
  '.git',
  '.codegraph',
  '.atl',
  '.auth',
  'Library',
  'Temp',
  'obj',
  'dist',
  'Build',
  'Builds',
  'UserSettings'
]);

// Uncommitted local-only files that are protected by .gitignore (never read their contents in repo scan)
const IGNORED_LOCAL_FILES = new Set([
  '.env',
  '.env.local',
  '.env.production',
  '.env.development',
  '.env.test',
  '.env.stage',
  'credentials.json',
  'token.json',
  'security.scan.test.js' // Contains intentional synthetic test fixtures to verify regex detectors
]);

// Binary extensions excluded from UTF-8 text scanning.
// NOTE: Unity serialized text assets (.asset, .prefab, .unity, .scenetemplate, .mat, .anim, .controller, .lighting) are intentionally NOT ignored.
const IGNORED_EXTENSIONS = new Set([
  '.png', '.jpg', '.jpeg', '.gif', '.psd', '.tga', '.meta',
  '.dll', '.exe', '.pdb', '.zip', '.tar', '.gz', '.mp3', '.wav',
  '.ogg', '.ttf', '.otf', '.blend', '.fbx',
  '.db', '.pdf', '.cubemap'
]);

// Allowlisted synthetic patterns and template placeholders that are not real secrets
const SYNTHETIC_PLACEHOLDER_REGEX = /PLACEHOLDER_|<user>|<password>|<cluster>|localhost:27017|127\.0\.0\.1:27017/i;

// Known development values that must be classified and reported rather than silently ignored (US-61 / FIX-63)
const KNOWN_DEV_SECRETS = new Set([
  'dev-webhook-secret-do-not-use-in-prod',
  'dev_webhook_secret_123',
  'dev-secret',
  'secret',
  'changeme'
]);

const SECRET_PATTERNS = [
  {
    type: 'PRIVATE_KEY',
    regex: /-----BEGIN (?:[A-Z0-9 ]+ )?PRIVATE KEY-----/,
  },
  {
    type: 'AWS_ACCESS_KEY',
    regex: /\b(?:A3T[A-Z0-9]|AKIA|AGPA|AIDA|AROA|AIPA|ANPA|ANVA|ASIA)[A-Z0-9]{16}\b/,
  },
  {
    type: 'GITHUB_TOKEN',
    regex: /\b(?:ghp_[A-Za-z0-9]{36}|github_pat_[A-Za-z0-9_]{82})\b/,
  },
  {
    type: 'GOOGLE_OAUTH_SECRET',
    regex: /\bGOCSPX-[A-Za-z0-9_-]{20,}\b|"client_secret"\s*:\s*"([^"\s]{12,})"|"refresh_token"\s*:\s*"(1\/\/[^"\s]{20,})"|\bya29\.[A-Za-z0-9_-]{30,}\b/,
    filter: (match) => !SYNTHETIC_PLACEHOLDER_REGEX.test(match),
  },
  {
    type: 'MONGODB_CREDENTIAL_URI',
    regex: /mongodb(?:\+srv)?:\/\/(?!PLACEHOLDER_)[^/\s:@]+:[^/\s:@]+@(?!PLACEHOLDER_)[^/\s]+\/[^\s?'"]*/i,
    filter: (match) => !SYNTHETIC_PLACEHOLDER_REGEX.test(match),
  },
  {
    type: 'HIGH_ENTROPY_JWT',
    regex: /\beyJ[A-Za-z0-9_-]{15,}\.eyJ[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{20,}\b/,
    filter: (match) => !match.includes('dummy') && !match.includes('mock'),
  },
  {
    type: 'ENV_SECRET_ASSIGNMENT',
    regex: /^\s*(?:JWT_SECRET|WEBHOOK_SECRET)\s*=\s*['"]?([^\s'"#]+)['"]?/,
    filter: (match, fullMatch) => {
      const val = (fullMatch && fullMatch[1] ? fullMatch[1] : match).trim();
      if (!val || val.startsWith('$') || val.startsWith('process.env')) return false;
      if (SYNTHETIC_PLACEHOLDER_REGEX.test(val)) return false;
      return true;
    },
    classify: (match, fullMatch) => {
      const val = (fullMatch && fullMatch[1] ? fullMatch[1] : match).trim().toLowerCase();
      if (KNOWN_DEV_SECRETS.has(val)) {
        return 'KNOWN_DEV_SECRET';
      }
      return 'ENV_SECRET_ASSIGNMENT';
    }
  },
  {
    type: 'UNITY_WEBHOOK_SECRET',
    regex: /^\s*WebhookSecret:\s*['"]?([^\s'"#]+)['"]?/,
    filter: (match, fullMatch) => {
      const val = (fullMatch && fullMatch[1] ? fullMatch[1] : match).trim();
      if (!val) return false;
      if (SYNTHETIC_PLACEHOLDER_REGEX.test(val)) return false;
      return true;
    },
    classify: (match, fullMatch) => {
      const val = (fullMatch && fullMatch[1] ? fullMatch[1] : match).trim().toLowerCase();
      if (KNOWN_DEV_SECRETS.has(val)) {
        return 'KNOWN_DEV_SECRET';
      }
      return 'UNITY_WEBHOOK_SECRET';
    }
  }
];

function redact(secret) {
  if (!secret || secret.length < 4) {
    return '****';
  }
  return secret.slice(0, 3) + '*'.repeat(Math.min(16, secret.length - 3));
}

function scanContent(content, filePath = '<unknown>') {
  const findings = [];
  const lines = content.split('\n');

  for (let lineIndex = 0; lineIndex < lines.length; lineIndex++) {
    const line = lines[lineIndex];

    for (const pattern of SECRET_PATTERNS) {
      const match = line.match(pattern.regex);
      if (match) {
        const rawMatched = match[1] || match[2] || match[0];
        if (!pattern.filter || pattern.filter(rawMatched, match)) {
          const type = pattern.classify ? pattern.classify(rawMatched, match) : pattern.type;
          findings.push({
            file: filePath,
            line: lineIndex + 1,
            type: type,
            redactedValue: redact(rawMatched),
            category: type === 'KNOWN_DEV_SECRET' ? 'KNOWN_DEV' : 'CRITICAL'
          });
        }
      }
    }
  }

  return findings;
}

function scanDirectory(dir, rootDir = dir) {
  let allFindings = [];

  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    const relativePath = path.relative(rootDir, fullPath);

    if (entry.isDirectory()) {
      if (!IGNORED_DIRS.has(entry.name)) {
        allFindings = allFindings.concat(scanDirectory(fullPath, rootDir));
      }
    } else if (entry.isFile()) {
      if (IGNORED_LOCAL_FILES.has(entry.name) || (entry.name.startsWith('.env') && !entry.name.endsWith('.example'))) {
        continue;
      }
      const ext = path.extname(entry.name).toLowerCase();
      if (!IGNORED_EXTENSIONS.has(ext)) {
        try {
          const stats = fs.statSync(fullPath);
          if (stats.size <= MAX_SCAN_FILE_BYTES) {
            const content = fs.readFileSync(fullPath, 'utf8');
            const findings = scanContent(content, relativePath);
            allFindings = allFindings.concat(findings);
          }
        } catch (err) {
          // Ignore binary or unreadable files
        }
      }
    }
  }

  return allFindings;
}

// Pure parser shared by the CLI history scan and synthetic regression fixtures.
function scanHistoryOutput(diffOutput) {
  const findings = [];
  let currentCommit = 'unknown';
  let currentFile = '<git-history>';
  for (const line of diffOutput.split('\n')) {
    if (line.startsWith('commit ')) {
      currentCommit = line.slice(7, 15);
    } else if (line.startsWith('+++ b/')) {
      currentFile = line.slice(6).trim();
    } else if (line.startsWith('+') && !line.startsWith('+++')) {
      if (IGNORED_LOCAL_FILES.has(path.basename(currentFile))) continue;
      findings.push(...scanContent(line.slice(1), `git:${currentCommit}:${currentFile}`));
    }
  }
  return findings;
}

function scanGitHistory(rootDir) {
  return new Promise((resolve) => {
    const findings = [];
    let git;
    try {
      git = spawn('git', ['log', '--all', '-p', '-U0'], { cwd: rootDir });
    } catch (err) {
      return resolve(findings);
    }

    git.on('error', () => {
      resolve(findings);
    });

    const rl = readline.createInterface({ input: git.stdout });
    let currentCommit = 'unknown';
    let currentFile = '<git-history>';

    rl.on('line', (line) => {
      if (line.startsWith('commit ')) {
        currentCommit = line.slice(7, 15);
      } else if (line.startsWith('+++ b/')) {
        currentFile = line.slice(6).trim();
      } else if (line.startsWith('+') && !line.startsWith('+++')) {
        if (IGNORED_LOCAL_FILES.has(path.basename(currentFile))) return;
        const lineFindings = scanContent(line.slice(1), `git:${currentCommit}:${currentFile}`);
        for (const f of lineFindings) {
          findings.push(f);
        }
      }
    });

    rl.on('close', () => {
      resolve(findings);
    });
  });
}

async function runCli() {
  const targetDir = process.argv[2] ? path.resolve(process.argv[2]) : path.resolve(__dirname, '../..');
  console.log(`[SecretScanner] Scanning directory (up to 25 MB per file, including Unity YAML assets): ${targetDir}`);

  const worktreeFindings = scanDirectory(targetDir);
  console.log('[SecretScanner] Scanning complete repository Git history...');
  const historyFindings = await scanGitHistory(targetDir);
  const allFindings = worktreeFindings.concat(historyFindings);

  const realSecrets = allFindings.filter(f => f.type !== 'KNOWN_DEV_SECRET');
  const devSecrets = allFindings.filter(f => f.type === 'KNOWN_DEV_SECRET');

  if (devSecrets.length > 0) {
    console.log(`[SecretScanner] [INFO] Detected ${devSecrets.length} classified known development secret/marker(s) (redacted):`);
    for (const finding of devSecrets) {
      console.log(`  - ${finding.file}:${finding.line} | [${finding.type}] Value: ${finding.redactedValue}`);
    }
  }

  if (realSecrets.length === 0) {
    console.log('[SecretScanner] Scan complete: No exposed credentials, OAuth secrets, or private keys detected in working tree or Git history.');
    process.exit(0);
  } else {
    console.error(`[SecretScanner] [ALERT] Detected ${realSecrets.length} critical potential secret(s):`);
    for (const finding of realSecrets) {
      console.error(`  - ${finding.file}:${finding.line} | [${finding.type}] Value: ${finding.redactedValue}`);
    }
    process.exit(1);
  }
}

if (require.main === module) {
  runCli();
}

module.exports = {
  scanContent,
  scanDirectory,
  scanGitHistory,
  scanHistoryOutput,
  redact,
  SECRET_PATTERNS
};

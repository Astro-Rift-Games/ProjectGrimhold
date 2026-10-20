#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

/**
 * Project Grimhold — Repeatable Secret Scanner (US-61 / SEC-61.2)
 *
 * Scans repository files for hardcoded credentials, API keys, private keys,
 * and unprotected database URIs with strictly redacted output.
 */

const IGNORED_DIRS = new Set([
  'node_modules',
  '.git',
  '.codegraph',
  '.atl',
  'Library',
  'Temp',
  'obj',
  'dist',
  'Build',
  'Builds',
  'UserSettings'
]);

// Uncommitted local-only files that are protected by .gitignore
const IGNORED_LOCAL_FILES = new Set([
  '.env',
  '.env.local',
  '.env.production',
  '.env.development',
  '.env.test',
  '.env.stage',
  'security.scan.test.js' // Contains intentional test fixtures to verify regex detectors
]);

const IGNORED_EXTENSIONS = new Set([
  '.png', '.jpg', '.jpeg', '.gif', '.psd', '.tga', '.asset', '.prefab',
  '.unity', '.mat', '.meta', '.dll', '.exe', '.pdb', '.zip', '.tar', '.gz',
  '.mp3', '.wav', '.ogg', '.ttf', '.otf', '.anim', '.controller', '.blend',
  '.fbx', '.db', '.pdf'
]);

// Allowlisted synthetic patterns that explicitly document examples without risking exposure
const SYNTHETIC_PLACEHOLDER_REGEX = /PLACEHOLDER_|dev-webhook-secret-do-not-use-in-prod|<user>|<password>|localhost:27017|127\.0\.0\.1:27017/i;

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
    type: 'MONGODB_CREDENTIAL_URI',
    regex: /mongodb(?:\+srv)?:\/\/(?!PLACEHOLDER_)[^/\s:@]+:[^/\s:@]+@(?!PLACEHOLDER_)[^/\s]+\/[^\s?]*/i,
    filter: (match) => !SYNTHETIC_PLACEHOLDER_REGEX.test(match),
  },
  {
    type: 'HIGH_ENTROPY_JWT',
    regex: /\beyJ[A-Za-z0-9_-]{15,}\.eyJ[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{20,}\b/,
    // Disregard test-generated dummy tokens or fixture strings if explicitly synthetic
    filter: (match) => !match.includes('dummy') && !match.includes('mock'),
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
        const rawMatched = match[0];
        if (!pattern.filter || pattern.filter(rawMatched)) {
          findings.push({
            file: filePath,
            line: lineIndex + 1,
            type: pattern.type,
            redactedValue: redact(rawMatched),
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
      if (IGNORED_LOCAL_FILES.has(entry.name)) {
        continue;
      }
      const ext = path.extname(entry.name).toLowerCase();
      if (!IGNORED_EXTENSIONS.has(ext)) {
        try {
          const stats = fs.statSync(fullPath);
          // Limit inspection to files under 2 MB
          if (stats.size <= 2 * 1024 * 1024) {
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

function runCli() {
  const targetDir = process.argv[2] ? path.resolve(process.argv[2]) : path.resolve(__dirname, '../..');
  console.log(`[SecretScanner] Scanning directory: ${targetDir}`);

  const findings = scanDirectory(targetDir);

  if (findings.length === 0) {
    console.log('[SecretScanner] Scan complete: No exposed credentials or private keys detected.');
    process.exit(0);
  } else {
    console.error(`[SecretScanner] [ALERT] Detected ${findings.length} potential secret(s) in repository:`);
    for (const finding of findings) {
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
  redact,
  SECRET_PATTERNS
};

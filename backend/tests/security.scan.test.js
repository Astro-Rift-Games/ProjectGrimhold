const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const { scanContent, scanHistoryOutput, redact } = require('../../tools/security/scan-secrets');

describe('Secret Scanner Unit Tests (US-61 / SEC-61.2)', () => {
  test('history output reports commit and path without exposing the raw key', () => {
    const fakeKey = 'ghp_' + 'S'.repeat(36);
    const history = [
      'commit abcdef0123456789',
      'diff --git a/backend/config.js b/backend/config.js',
      '--- a/backend/config.js',
      '+++ b/backend/config.js',
      '@@ -0,0 +1 @@',
      '+const key = "' + fakeKey + '";',
      'commit 123456789abcdef0',
      '+++ b/backend/other.js',
      '+const harmless = true;'
    ].join('\n');
    const findings = scanHistoryOutput(history);
    assert.equal(findings.length, 1);
    assert.equal(findings[0].file, 'git:abcdef01:backend/config.js');
    assert.equal(findings[0].type, 'GITHUB_TOKEN');
    assert.equal(JSON.stringify(findings).includes(fakeKey), false);
    assert.match(findings[0].redactedValue, /\*/);
    assert.deepEqual(scanHistoryOutput(''), []);
  });

  test('redact() replaces middle/trailing characters with asterisks', () => {
    assert.equal(redact(''), '****');
    assert.equal(redact('abc'), '****');
    const redacted = redact('AKIA1234567890ABCDEF');
    assert.ok(redacted.startsWith('AKI'));
    assert.ok(redacted.endsWith('****'));
    assert.equal(redacted.includes('1234567890ABCDEF'), false, 'Sensitive body must be redacted');
  });

  test('scanContent detects private keys with redacted output', () => {
    const sample = 'const key = "-----BEGIN PRIVATE KEY-----\\nMIIEvgIBADANBgk...";';
    const findings = scanContent(sample, 'mock/file.js');
    assert.equal(findings.length, 1);
    assert.equal(findings[0].type, 'PRIVATE_KEY');
    assert.equal(findings[0].file, 'mock/file.js');
    assert.equal(findings[0].line, 1);
    assert.ok(findings[0].redactedValue.includes('***'));
  });

  test('scanContent detects AWS access keys, GitHub tokens, and Google OAuth secrets', () => {
    const awsSample = 'AWS_KEY=AKIAIOSFODNN7EXAMPLE';
    const ghSample = 'GITHUB_PAT=ghp_123456789012345678901234567890123456';
    const googleSample = '{"client_secret": "GOCSPX-RealSecretValue1234567890"}';

    const awsFindings = scanContent(awsSample, 'config.js');
    assert.equal(awsFindings.length, 1);
    assert.equal(awsFindings[0].type, 'AWS_ACCESS_KEY');

    const ghFindings = scanContent(ghSample, 'token.js');
    assert.equal(ghFindings.length, 1);
    assert.equal(ghFindings[0].type, 'GITHUB_TOKEN');

    const googleFindings = scanContent(googleSample, 'credentials.json');
    assert.equal(googleFindings.length, 1);
    assert.equal(googleFindings[0].type, 'GOOGLE_OAUTH_SECRET');
    assert.equal(googleFindings[0].redactedValue.includes('RealSecretValue'), false);
  });

  test('scanContent detects real MongoDB credentials in URIs', () => {
    const realUri = 'MONGODB_URI=mongodb+srv://admin_prod:SecretPass123!@cluster0.abcde.mongodb.net/prod_db';
    const findings = scanContent(realUri, 'production.env');
    assert.equal(findings.length, 1);
    assert.equal(findings[0].type, 'MONGODB_CREDENTIAL_URI');
    assert.ok(!findings[0].redactedValue.includes('SecretPass123!'), 'Password must never appear in findings');
  });

  test('scanContent detects non-placeholder JWT_SECRET, WEBHOOK_SECRET, and Unity WebhookSecret', () => {
    const envSample = 'JWT_SECRET=prod_super_secret_hmac_key_998877665544332211\nWEBHOOK_SECRET=prod_webhook_secret_key_998877665544332211';
    const envFindings = scanContent(envSample, 'backend/.env.example');
    assert.equal(envFindings.length, 2);
    assert.equal(envFindings[0].type, 'ENV_SECRET_ASSIGNMENT');
    assert.equal(envFindings[1].type, 'ENV_SECRET_ASSIGNMENT');

    const unityAssetSample = '  WebhookSecret: prod_unity_shared_webhook_secret_2026';
    const unityFindings = scanContent(unityAssetSample, 'Project Grimhold/Assets/Resources/BackendConfiguration.asset');
    assert.equal(unityFindings.length, 1);
    assert.equal(unityFindings[0].type, 'UNITY_WEBHOOK_SECRET');
  });

  test('scanContent ignores documented synthetic placeholders and local dev defaults', () => {
    const syntheticExample = [
      'MONGODB_URI=mongodb://localhost:27017/grimhold_dev',
      'MONGODB_URI=mongodb+srv://PLACEHOLDER_USER:PLACEHOLDER_PASSWORD@PLACEHOLDER_CLUSTER.mongodb.net/grimhold_dev',
      'WEBHOOK_SECRET=dev-webhook-secret-do-not-use-in-prod',
      'JWT_SECRET=PLACEHOLDER_JWT_SECRET_MIN_32_CHARS_LONG',
      '  WebhookSecret: dev_webhook_secret_123'
    ].join('\n');

    const findings = scanContent(syntheticExample, 'backend/.env.example');
    assert.equal(findings.length, 0, 'Synthetic placeholders must not trigger alerts');
  });
});

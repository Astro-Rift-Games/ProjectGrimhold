const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('path');
const { scanContent, redact } = require('../../tools/security/scan-secrets');

describe('Secret Scanner Unit Tests (US-61 / SEC-61.2)', () => {
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

  test('scanContent detects AWS access keys and GitHub tokens', () => {
    const awsSample = 'AWS_KEY=AKIAIOSFODNN7EXAMPLE';
    const ghSample = 'GITHUB_PAT=ghp_123456789012345678901234567890123456';

    const awsFindings = scanContent(awsSample, 'config.js');
    assert.equal(awsFindings.length, 1);
    assert.equal(awsFindings[0].type, 'AWS_ACCESS_KEY');

    const ghFindings = scanContent(ghSample, 'token.js');
    assert.equal(ghFindings.length, 1);
    assert.equal(ghFindings[0].type, 'GITHUB_TOKEN');
  });

  test('scanContent detects real MongoDB credentials in URIs', () => {
    const realUri = 'MONGODB_URI=mongodb+srv://admin_prod:SecretPass123!@cluster0.abcde.mongodb.net/prod_db';
    const findings = scanContent(realUri, 'production.env');
    assert.equal(findings.length, 1);
    assert.equal(findings[0].type, 'MONGODB_CREDENTIAL_URI');
    assert.ok(!findings[0].redactedValue.includes('SecretPass123!'), 'Password must never appear in findings');
  });

  test('scanContent ignores documented synthetic placeholders', () => {
    const syntheticExample = [
      'MONGODB_URI=mongodb://localhost:27017/grimhold_dev',
      'MONGODB_URI=mongodb+srv://PLACEHOLDER_USER:PLACEHOLDER_PASSWORD@PLACEHOLDER_CLUSTER.mongodb.net/grimhold_dev',
      'WEBHOOK_SECRET=dev-webhook-secret-do-not-use-in-prod',
      'JWT_SECRET=PLACEHOLDER_JWT_SECRET_MIN_32_CHARS_LONG'
    ].join('\n');

    const findings = scanContent(syntheticExample, 'backend/.env.example');
    assert.equal(findings.length, 0, 'Synthetic placeholders must not trigger alerts');
  });
});

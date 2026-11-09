const { describe, test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const { spawnSync, execFileSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

// Black-box tests for .githooks/pre-push: a stub `node` on PATH records every
// gate step, so the hook's control flow is observed without running the suites.
const repoRoot = path.resolve(__dirname, '..', '..');
const hookPath = path.join(repoRoot, '.githooks', 'pre-push');
const ZERO = '0'.repeat(40);
const SHA = 'a'.repeat(40);

function findShell() {
  try {
    const cmd = process.platform === 'win32' ? 'where' : 'which';
    return execFileSync(cmd, ['sh'], { encoding: 'utf8' }).split(/\r?\n/)[0].trim();
  } catch {
    return null;
  }
}

const shell = findShell();

describe('Pre-push quality gate hook', { skip: shell ? false : 'sh is not available' }, () => {
  let stubDir;
  let callLog;

  before(() => {
    stubDir = fs.mkdtempSync(path.join(os.tmpdir(), 'grimhold-hook-'));
    callLog = path.join(stubDir, 'calls.log');
  });

  after(() => {
    fs.rmSync(stubDir, { recursive: true, force: true });
  });

  function writeNodeStub(failOnArg) {
    const script = [
      '#!/bin/sh',
      `echo "$*" >> "${callLog.replace(/\\/g, '/')}"`,
      failOnArg ? `case "$*" in *${failOnArg}*) exit 1;; esac` : '',
      'exit 0',
      ''
    ].join('\n');
    const stub = path.join(stubDir, 'node');
    fs.writeFileSync(stub, script);
    fs.chmodSync(stub, 0o755);
  }

  function runHook(stdin, { withNode = true } = {}) {
    fs.rmSync(callLog, { force: true });
    if (!withNode) fs.rmSync(path.join(stubDir, 'node'), { force: true });
    const result = spawnSync(shell, [hookPath], {
      cwd: repoRoot,
      input: stdin,
      encoding: 'utf8',
      env: { ...process.env, CI: '1', PATH: [stubDir, path.dirname(shell)].join(path.delimiter) }
    });
    const calls = fs.existsSync(callLog) ? fs.readFileSync(callLog, 'utf8').trim().split('\n') : [];
    return { ...result, calls };
  }

  test('a push that only deletes branches skips the gate', () => {
    writeNodeStub();
    const result = runHook(`(delete) ${ZERO} refs/heads/old ${SHA}\n`);
    assert.equal(result.status, 0);
    assert.deepEqual(result.calls, []);
  });

  test('a push that deletes and updates refs still runs every gate step', () => {
    writeNodeStub();
    const stdin = [
      `refs/heads/feature ${SHA} refs/heads/feature ${ZERO}`,
      `(delete) ${ZERO} refs/heads/old ${SHA}`,
      ''
    ].join('\n');
    const result = runHook(stdin);
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.calls.length, 3);
    assert.match(result.calls[0], /scan-secrets\.js/);
    assert.match(result.calls[1], /--test tests\/[^ ]+\.parity\.test\.js/);
    assert.doesNotMatch(result.calls[1], /tests\/(concurrency|integration)\//);
    assert.match(result.calls[2], /tests\/concurrency\/.*tests\/integration\//);
  });

  test('a push without ref lines runs every gate step', () => {
    writeNodeStub();
    const result = runHook('');
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.calls.length, 3);
  });

  test('a detected secret blocks the push before any test suite runs', () => {
    writeNodeStub('scan-secrets');
    const result = runHook(`refs/heads/feature ${SHA} refs/heads/feature ${ZERO}\n`);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /CREDENCIALES O SECRETOS DETECTADOS/);
    assert.equal(result.calls.length, 1);
  });

  test('a failing test suite blocks the push with remediation steps', () => {
    writeNodeStub('.parity.test.js');
    const result = runHook(`refs/heads/feature ${SHA} refs/heads/feature ${ZERO}\n`);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /PUSH BLOQUEADO — TESTS O CONTRATOS DE PARIDAD FALLIDOS/);
    assert.match(result.stderr, /npm test/);
    assert.equal(result.calls.length, 2);
  });

  test('a missing Node.js blocks the push with an accurate message', {
    skip: fs.existsSync(path.join(path.dirname(shell), process.platform === 'win32' ? 'node.exe' : 'node'))
      ? 'node lives next to sh; cannot hide it from PATH'
      : false
  }, () => {
    const result = runHook(`refs/heads/feature ${SHA} refs/heads/feature ${ZERO}\n`, { withNode: false });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /PUSH BLOQUEADO — NODE\.JS NO ENCONTRADO/);
    assert.doesNotMatch(result.stderr, /CREDENCIALES/);
  });

  test('the hook is committed as executable so POSIX Git runs it', () => {
    const entry = execFileSync('git', ['ls-files', '-s', '.githooks/pre-push'], { cwd: repoRoot, encoding: 'utf8' });
    assert.match(entry, /^100755 /);
  });
});

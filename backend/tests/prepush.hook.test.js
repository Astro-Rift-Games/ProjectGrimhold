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
  let unityLog;
  let changedFiles;
  let unityProject;

  before(() => {
    stubDir = fs.mkdtempSync(path.join(os.tmpdir(), 'grimhold-hook-'));
    callLog = path.join(stubDir, 'calls.log');
    unityLog = path.join(stubDir, 'unity.log');
    changedFiles = path.join(stubDir, 'changed.txt');
    unityProject = path.join(stubDir, 'UnityProject');
  });

  const posix = (p) => p.replace(/\\/g, '/');

  function writeStub(name, lines) {
    const stub = path.join(stubDir, name);
    fs.writeFileSync(stub, ['#!/bin/sh', ...lines, ''].join('\n'));
    fs.chmodSync(stub, 0o755);
  }

  // `git` answers the hook's changed-file queries with a fixed list, or fails like an unknown commit.
  function writeGitStub(files, { fails = false } = {}) {
    fs.writeFileSync(changedFiles, files.length ? files.join('\n') + '\n' : '');
    writeStub('git', fails ? ['echo "fatal: bad object" >&2', 'exit 128'] : [`cat "${posix(changedFiles)}"`]);
  }

  // `unity` simulates the open Editor: editorState is ready | playing | closed; run is the result JSON.
  function writeUnityStub({ editorState = 'ready', run = { status: 'completed', total: 3, passed: 3, failed: 0, failures: [] }, writesResult = true } = {}) {
    const resultFile = posix(path.join(unityProject, 'Temp', 'PrePushUnityTests.json'));
    const status = editorState === 'playing'
      ? '{"status": "playing", "compiling": false, "playMode": "playing"}'
      : '{"status": "ready", "compiling": false, "playMode": "stopped"}';
    writeStub('unity', [
      `echo "$*" >> "${posix(unityLog)}"`,
      editorState === 'closed' ? 'exit 12' : '',
      'case "$*" in',
      `  *"editor status"*) echo '${status}' ;;`,
      writesResult
        ? `  *"script eval"*) mkdir -p "$(dirname "${resultFile}")"; echo '${JSON.stringify(run)}' > "${resultFile}"; echo '{"result": "started"}' ;;`
        : '  *"script eval"*) echo \'{"result": "started"}\' ;;',
      'esac',
      'exit 0'
    ]);
  }

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

  function runHook(stdin, { withNode = true, changed = [], gitFails = false, unity = {}, withUnity = true, env = {} } = {}) {
    for (const file of [callLog, unityLog]) fs.rmSync(file, { force: true });
    fs.rmSync(unityProject, { recursive: true, force: true });
    if (!withNode) fs.rmSync(path.join(stubDir, 'node'), { force: true });
    writeGitStub(changed, { fails: gitFails });
    if (withUnity) writeUnityStub(unity);
    else fs.rmSync(path.join(stubDir, 'unity'), { force: true });
    const result = spawnSync(shell, [hookPath], {
      cwd: repoRoot,
      input: stdin,
      encoding: 'utf8',
      env: {
        ...process.env,
        CI: '1',
        PATH: [stubDir, path.dirname(shell)].join(path.delimiter),
        GRIMHOLD_UNITY_PROJECT: unityProject,
        GRIMHOLD_UNITY_POLL_SECONDS: '0',
        ...env
      }
    });
    const read = (file) => (fs.existsSync(file) ? fs.readFileSync(file, 'utf8').trim().split('\n') : []);
    return { ...result, calls: read(callLog), unityCalls: read(unityLog) };
  }

  const UPDATE = `refs/heads/feature ${SHA} refs/heads/feature ${'b'.repeat(40)}\n`;
  const UNITY_FILE = 'Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs';

  test('a push that does not touch the Unity project skips the Unity gate', () => {
    writeNodeStub();
    const result = runHook(UPDATE, { changed: ['backend/src/app.js', 'docs/README.md'] });
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.calls.length, 3);
    assert.deepEqual(result.unityCalls, []);
    assert.match(result.stdout, /sin cambios en Project Grimhold/);
  });

  test('a Unity change runs the EditMode subset in the open Editor and passes', () => {
    writeNodeStub();
    const result = runHook(UPDATE, { changed: [UNITY_FILE] });
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.unityCalls.some((c) => /editor status/.test(c)), 'checks the Editor state');
    assert.ok(result.unityCalls.some((c) => /script eval .*PrePushUnityTestRun\.Start/.test(c)), 'starts the subset');
    assert.match(result.stdout, /\[4\/4\].*Unity/);
    assert.match(result.stdout, /3 tests de Unity pasaron/);
  });

  test('failing Unity tests block the push and name the failures', () => {
    writeNodeStub();
    const run = { status: 'completed', total: 3, passed: 2, failed: 1, failures: [{ name: 'Tests.EditMode.Loot.FooTests.Bar', message: 'Expected 1' }] };
    const result = runHook(UPDATE, { changed: [UNITY_FILE], unity: { run } });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /PUSH BLOQUEADO — TESTS DE UNITY FALLIDOS/);
    assert.match(result.stderr, /Tests\.EditMode\.Loot\.FooTests\.Bar/);
  });

  test('a closed Editor blocks a Unity push and asks to open it', () => {
    writeNodeStub();
    const result = runHook(UPDATE, { changed: [UNITY_FILE], unity: { editorState: 'closed' } });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /PUSH BLOQUEADO — UNITY NO ESTÁ LISTO/);
    assert.match(result.stderr, /Abrí el proyecto en Unity/);
    assert.ok(!result.unityCalls.some((c) => /script eval/.test(c)), 'never starts tests');
  });

  test('an Editor in Play Mode blocks a Unity push', () => {
    writeNodeStub();
    const result = runHook(UPDATE, { changed: [UNITY_FILE], unity: { editorState: 'playing' } });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /PUSH BLOQUEADO — UNITY NO ESTÁ LISTO/);
    assert.match(result.stderr, /Play Mode/);
    assert.ok(!result.unityCalls.some((c) => /script eval/.test(c)), 'never starts tests');
  });

  test('a missing Unity CLI blocks a Unity push', () => {
    writeNodeStub();
    const result = runHook(UPDATE, { changed: [UNITY_FILE], withUnity: false });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /PUSH BLOQUEADO — UNITY NO ESTÁ LISTO/);
    assert.match(result.stderr, /Unity CLI/);
  });

  test('a new branch with Unity changes runs the Unity gate', () => {
    writeNodeStub();
    const result = runHook(`refs/heads/new ${SHA} refs/heads/new ${ZERO}\n`, { changed: [UNITY_FILE] });
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.unityCalls.some((c) => /script eval .*PrePushUnityTestRun\.Start/.test(c)));
  });

  test('an unknown remote commit still runs the Unity gate instead of skipping it', () => {
    writeNodeStub();
    const result = runHook(UPDATE, { gitFails: true });
    assert.equal(result.status, 0, result.stderr);
    assert.ok(result.unityCalls.some((c) => /script eval .*PrePushUnityTestRun\.Start/.test(c)));
  });

  test('a Unity run that cannot select tests blocks with its reason', () => {
    writeNodeStub();
    const run = { status: 'error', total: 0, passed: 0, failed: 0, failures: [{ name: 'selection', message: 'No EditMode tests selected.' }] };
    const result = runHook(UPDATE, { changed: [UNITY_FILE], unity: { run } });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /PUSH BLOQUEADO — TESTS DE UNITY NO EJECUTADOS/);
    assert.match(result.stderr, /No EditMode tests selected\./);
    assert.doesNotMatch(result.stderr, /Fallaron 0 de 0/);
  });

  test('a Unity run that never reports blocks after the timeout', () => {
    writeNodeStub();
    const result = runHook(UPDATE, { changed: [UNITY_FILE], unity: { writesResult: false }, env: { GRIMHOLD_UNITY_TIMEOUT_SECONDS: '3' } });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /no terminaron en 3 s/);
  });

  test('backend failures block before the Unity gate runs', () => {
    writeNodeStub('.parity.test.js');
    const result = runHook(UPDATE, { changed: [UNITY_FILE] });
    assert.equal(result.status, 1);
    assert.deepEqual(result.unityCalls, []);
  });

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

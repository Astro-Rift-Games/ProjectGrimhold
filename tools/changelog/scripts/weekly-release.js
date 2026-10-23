import { execFileSync } from 'node:child_process';
import { appendFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { git, readCommits } from '../src/git.js';
import { config } from '../src/config.js';
import { renderReleaseNotes } from '../src/render-players.js';
import { createReleasePlan, writePlannedRelease } from '../src/release-plan.js';

const manifestPath = 'tools/changelog/publication.json';
const settingsPath = 'Project Grimhold/ProjectSettings/ProjectSettings.asset';
const cli = fileURLToPath(new URL('../src/cli.js', import.meta.url));
const generated = ['CHANGELOG_DEV.md', 'CHANGELOG.md', 'changelog-players.json'];
const shaPattern = /^[a-f0-9]{40}$/;
const versionPattern = /^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$/;
const head = root => git(root, ['rev-parse', 'HEAD']).trim();
const isAncestor = (root, older, newer) => {
  try { git(root, ['merge-base', '--is-ancestor', older, newer]); return true; }
  catch (error) { if (error.status === 1) return false; throw error; }
};
function save(state, phase) {
  state.phase = phase;
  writeFileSync(join(state.output, 'state.json'), `${JSON.stringify(state, null, 2)}\n`);
}
function snapshot(root) {
  const refs = Object.fromEntries(git(root, ['ls-remote', '--heads', 'origin', 'main', 'New-Testing'])
    .trim().split('\n').filter(Boolean).map(line => { const [sha, ref] = line.split(/\s+/); return [ref.split('/').at(-1), sha]; }));
  if (!shaPattern.test(refs.main ?? '') || !shaPattern.test(refs['New-Testing'] ?? '')) throw Error('Remote main and New-Testing are required');
  return { main: refs.main, development: refs['New-Testing'] };
}
export function assertSnapshots(state, expected = state.snapshots) {
  const actual = snapshot(state.candidate);
  if (actual.main !== expected.main || actual.development !== expected.development) {
    throw Error(`Remote branches changed; expected ${JSON.stringify(expected)}, observed ${JSON.stringify(actual)}. No forced update is permitted.`);
  }
}
function commit(root, message, date, paths) {
  git(root, ['add', '--', ...paths]);
  if (!git(root, ['diff', '--cached', '--name-only']).trim()) return head(root);
  git(root, ['-c', 'commit.gpgsign=false', 'commit', '-m', message], { GIT_AUTHOR_DATE: date, GIT_COMMITTER_DATE: date });
  return head(root);
}
function merge(root, sha, date, message) {
  try {
    git(root, ['-c', 'commit.gpgsign=false', 'merge', '--no-ff', '--no-edit', '-m', message, sha], {
      GIT_AUTHOR_DATE: date, GIT_COMMITTER_DATE: date,
    });
  } catch (error) {
    const files = git(root, ['diff', '--name-only', '--diff-filter=U']).trim();
    if (files) {
      git(root, ['merge', '--abort']);
      throw Error(`Merge conflicts: ${files}. Resolve in development; remote branches were not changed by this merge.`, { cause: error });
    }
    throw error;
  }
}
function bundleVersion(root, ref) {
  const text = ref ? git(root, ['show', `${ref}:${settingsPath}`]) : readFileSync(join(root, settingsPath), 'utf8');
  const matches = [...text.matchAll(/^  bundleVersion: (.+)$/gm)];
  if (matches.length !== 1) throw Error('Expected exactly one serialized bundleVersion');
  return matches[0][1].trim();
}
function runGenerator(root, command, args = []) {
  return execFileSync(process.execPath, [cli, command, '--root', root, ...args], { encoding: 'utf8' });
}
function tagTarget(root, tag, remote = false) {
  if (!remote) {
    if (!git(root, ['tag', '--list', tag]).trim()) return null;
    return git(root, ['rev-parse', `${tag}^{commit}`]).trim();
  }
  const values = git(root, ['ls-remote', '--tags', 'origin', `refs/tags/${tag}`, `refs/tags/${tag}^{}`]).trim().split('\n').filter(Boolean);
  if (!values.length) return null;
  return (values.find(line => line.endsWith('^{}')) ?? values[0]).split(/\s+/)[0];
}
function inspectManifest(state) {
  const path = join(state.candidate, manifestPath);
  if (!existsSync(path)) return null;
  const manifest = JSON.parse(readFileSync(path, 'utf8'));
  if (manifest.schemaVersion !== 1 || !versionPattern.test(manifest.version ?? '')
    || !shaPattern.test(manifest.sourceCommit ?? '') || !shaPattern.test(manifest.development ?? '')
    || !shaPattern.test(manifest.baseMain ?? '') || !/^\d{4}-\d{2}-\d{2}$/.test(manifest.releaseDate ?? '')) throw Error('Invalid publication manifest; manual inspection required');
  const target = git(state.candidate, ['log', '-1', '--format=%H', '--', manifestPath]).trim();
  const version = bundleVersion(state.candidate, target);
  if (version !== manifest.version || !isAncestor(state.candidate, manifest.sourceCommit, target)) throw Error('Publication manifest does not match its immutable target');
  return { manifest, target };
}

/** Prepare only in a disposable clone. No worktrees and no remote writes. */
export async function prepareCandidate(root, output, { api } = {}) {
  root = resolve(root); output = resolve(output);
  const location = relative(root, output);
  if (!location || (!location.startsWith('..') && !isAbsolute(location))) throw Error('Output/scratch must be outside the source repository');
  mkdirSync(output, { recursive: true });
  const snapshots = snapshot(root);
  const candidate = mkdtempSync(join(tmpdir(), 'grimhold-weekly-candidate-'));
  git(root, ['clone', '--no-checkout', '--no-hardlinks', root, candidate]);
  const origin = git(root, ['remote', 'get-url', 'origin']).trim();
  git(candidate, ['remote', 'set-url', 'origin', origin]);
  git(candidate, ['fetch', '--tags', 'origin', '+refs/heads/main:refs/remotes/origin/main', '+refs/heads/New-Testing:refs/remotes/origin/New-Testing']);
  for (const [key, branch] of [['main', 'main'], ['development', 'New-Testing']]) {
    if (git(candidate, ['rev-parse', `origin/${branch}`]).trim() !== snapshots[key]) throw Error('Remote branches changed while fetching; retry from a fresh snapshot');
  }
  git(candidate, ['config', 'user.name', 'Project Grimhold release bot']);
  git(candidate, ['config', 'user.email', '41898282+github-actions[bot]@users.noreply.github.com']);
  git(candidate, ['config', 'core.autocrlf', 'false']);
  const hooks = join(output, 'empty-hooks'); mkdirSync(hooks, { recursive: true });
  git(candidate, ['config', 'core.hooksPath', hooks]);
  git(candidate, ['checkout', '--detach', snapshots.main]);
  const date = [snapshots.main, snapshots.development].map(ref => git(candidate, ['show', '-s', '--format=%cI', ref]).trim()).sort().at(-1);
  const state = { schemaVersion: 1, candidate, output, snapshots, date, baselineVersion: bundleVersion(candidate), recovery: false };
  save(state, 'preparing');
  try {
    const prior = inspectManifest(state);
    if (prior) {
      const tag = `v${prior.manifest.version}`;
      const release = api ? await api.find(tag) : null;
      let recorded = null;
      try { recorded = JSON.parse(readFileSync(join(candidate, 'changelog-players.json'), 'utf8')).releases.find(r => r.tag === tag)?.commit; } catch { /* incomplete files force recovery */ }
      if (!release || release.draft || recorded !== prior.target || !isAncestor(candidate, snapshots.main, snapshots.development)) {
        state.recovery = true; state.target = prior.target;
        state.plan = { ...prior.manifest, version: prior.manifest.version };
        state.integratedMain = snapshots.main;
        git(candidate, ['checkout', '--detach', state.target]);
        save(state, 'prepared'); return state;
      }
    }
    if (!readCommits(candidate, `${snapshots.main}..${snapshots.development}`).some(c => !c.excluded)) {
      save(state, 'no-changes'); return state;
    }
    merge(candidate, snapshots.development, date, 'chore(changelog): integrate New-Testing into main');
    state.plan = createReleasePlan(candidate);
    if (state.plan.version) {
      writePlannedRelease(candidate, state.plan); // Strict note validation occurs before any push.
      const path = join(candidate, settingsPath);
      writeFileSync(path, readFileSync(path, 'utf8').replace(/^  bundleVersion: .+$/m, `  bundleVersion: ${state.plan.version}`));
      const manifest = { schemaVersion: 1, ...state.plan, baseMain: snapshots.main, development: snapshots.development };
      writeFileSync(join(candidate, manifestPath), `${JSON.stringify(manifest, null, 2)}\n`);
      commit(candidate, `chore(changelog): prepare v${state.plan.version}`, date, [...generated, settingsPath, manifestPath]);
    } else {
      runGenerator(candidate, 'generate', ['--only', 'dev', '--strict']);
      commit(candidate, 'chore(changelog): update integrated developer history', date, ['CHANGELOG_DEV.md']);
      if (bundleVersion(candidate) !== state.baselineVersion) throw Error('No-release integration must preserve main bundleVersion');
    }
    state.target = head(candidate);
    save(state, 'prepared'); return state;
  } catch (error) { state.error = error.message; save(state, 'preparation-failed'); throw error; }
}

function ensureTag(state) {
  const { candidate, target, plan } = state;
  const tag = `v${plan.version}`;
  const existing = tagTarget(candidate, tag, true);
  if (existing && existing !== target) throw Error(`Tag ${tag} exists on a different commit; never move it`);
  const local = tagTarget(candidate, tag);
  if (local && local !== target) throw Error(`Local tag ${tag} does not match publication target`);
  if (!local) git(candidate, ['-c', 'tag.gpgsign=false', 'tag', '-a', tag, target, '-m', `Project Grimhold ${tag}`], { GIT_COMMITTER_DATE: `${plan.releaseDate}T12:00:00Z` });
  return { tag, existing };
}
function taggedArtifacts(state, tag) {
  const { candidate, target } = state;
  git(candidate, ['checkout', '--detach', target]);
  runGenerator(candidate, 'generate', ['--ref', target, '--strict']);
  runGenerator(candidate, 'check', ['--ref', target, '--strict']);
  const json = readFileSync(join(candidate, 'changelog-players.json'), 'utf8');
  const release = JSON.parse(json).releases.find(r => r.tag === tag);
  if (release?.commit !== target) throw Error('Tag/JSON target mismatch');
  return { json, notes: renderReleaseNotes(release, { ...config, developmentBranch: 'main' }) };
}
function syncDevelopment(state, main) {
  const expected = { main, development: state.snapshots.development };
  assertSnapshots(state, expected);
  if (isAncestor(state.candidate, main, expected.development)) return;
  git(state.candidate, ['checkout', '--detach', expected.development]);
  merge(state.candidate, main, state.date, 'chore(changelog): synchronize published main into New-Testing');
  assertSnapshots(state, expected);
  push(state.candidate, 'HEAD:refs/heads/New-Testing');
  git(state.candidate, ['fetch', 'origin', 'refs/heads/New-Testing:refs/remotes/origin/New-Testing']);
}

/** Remote writes are opt-in; validate is mandatory, even for dry-run and recovery. */
export async function publishCandidate(state, { dryRun = false, validate, api } = {}) {
  if (state.phase === 'no-changes') return state;
  try {
    if (typeof validate !== 'function') throw Error('Mandatory candidate validation is unavailable');
    if (state.plan.version && !dryRun && !api) throw Error('GitHub Release API is required before integration');
    await validate(state);
    if (head(state.candidate) !== state.target) throw Error('Validation changed the candidate revision');
    if (git(state.candidate, ['status', '--porcelain']).trim()) throw Error('Validation changed candidate files; refusing an unverified target');
    assertSnapshots(state);
    let tagged;
    if (state.plan.version) {
      tagged = ensureTag(state); // Detect collisions before first main push.
      const artifacts = taggedArtifacts(state, tagged.tag);
      writeFileSync(join(state.output, 'release-notes.md'), artifacts.notes);
      writeFileSync(join(state.output, 'changelog-players.json'), artifacts.json);
      // Remove generated post-tag differences from the disposable checkout by returning
      // to the immutable prepared tree using exact generated paths only.
      git(state.candidate, ['restore', '--source', state.target, '--', ...generated]);
      if (dryRun) { save(state, 'dry-run'); return state; }
    } else if (dryRun) { save(state, 'dry-run'); return state; }
    if (!state.recovery) {
      assertSnapshots(state);
      save(state, 'main-push-pending');
      push(state.candidate, `${state.target}:refs/heads/main`);
      state.integratedMain = state.target;
      save(state, 'integrated');
    }
    if (state.plan.version) {
      if (!api) throw Error('GitHub Release API is required after integration');
      assertSnapshots(state, { main: state.integratedMain, development: state.snapshots.development });
      if (!tagged.existing) push(state.candidate, `refs/tags/${tagged.tag}:refs/tags/${tagged.tag}`);
      save(state, 'tagged');
      await api.publish(tagged.tag, state.target, readFileSync(join(state.output, 'release-notes.md'), 'utf8'), readFileSync(join(state.output, 'changelog-players.json'), 'utf8'), () => assertSnapshots(state, { main: state.integratedMain, development: state.snapshots.development }));
      save(state, 'published');
      // Post-tag versioned files reference the tagged SHA, not their own later commit.
      assertSnapshots(state, { main: state.integratedMain, development: state.snapshots.development });
      git(state.candidate, ['checkout', '--detach', state.integratedMain]);
      runGenerator(state.candidate, 'generate', ['--ref', state.integratedMain, '--strict']);
      const metadata = commit(state.candidate, `chore(changelog): record published ${tagged.tag}`, state.date, generated);
      if (metadata !== state.integratedMain) {
        assertSnapshots(state, { main: state.integratedMain, development: state.snapshots.development });
        push(state.candidate, `${metadata}:refs/heads/main`);
        state.integratedMain = metadata;
      }
      save(state, 'metadata-recorded');
    }
    syncDevelopment(state, state.integratedMain);
    git(state.candidate, ['fetch', 'origin', 'refs/heads/main:refs/remotes/origin/main']);
    save(state, 'complete'); return state;
  } catch (error) { state.error = error.message; state.failurePhase = state.phase; save(state, 'failed'); throw error; }
}

export class GitHubReleases {
  constructor(token, repository = 'Astro-Rift-Games/ProjectGrimhold') {
    if (!token) throw Error('GH_TOKEN is required for GitHub API access');
    this.token = token; this.repository = repository;
  }
  async request(path, options = {}, raw = false) {
    const response = await fetch(`https://api.github.com/repos/${this.repository}${path}`, {
      signal: AbortSignal.timeout(60000), ...options, headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${this.token}`, Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28', ...options.headers },
    });
    if (response.status === 404 && !options.method) return null;
    if (!response.ok) throw Error(`GitHub ${options.method ?? 'GET'} ${path}: HTTP ${response.status}; inspect/retry the same publication, not a new version`);
    return raw ? Buffer.from(await response.arrayBuffer()) : response.json();
  }
  find(tag) { return this.request(`/releases/tags/${encodeURIComponent(tag)}`); }
  async publish(tag, sha, notes, bytes, guard = () => {}) {
    let release = await this.find(tag);
    if (!release) { await guard(); release = await this.request('/releases', { method: 'POST', body: JSON.stringify({ tag_name: tag, target_commitish: sha, name: tag, body: notes, draft: true, prerelease: false }) }); }
    if (release.prerelease || release.tag_name !== tag || release.body !== notes) throw Error('Existing release metadata differs; manual inspection required (no overwrite)');
    const assets = await this.request(`/releases/${release.id}/assets?per_page=100`);
    if (!Array.isArray(assets) || assets.length >= 100) throw Error('Unbounded asset listing; manual inspection required');
    const matches = assets.filter(asset => asset.name === 'changelog-players.json');
    if (matches.length > 1) throw Error('Duplicate release assets');
    if (matches.length) {
      const existing = await this.request(`/releases/assets/${matches[0].id}`, { headers: { Accept: 'application/octet-stream' } }, true);
      if (!existing || existing.toString('utf8') !== bytes) throw Error('Existing release asset differs; never clobber a publication');
    } else {
      if (!release.draft) throw Error('Published release is missing its required asset; inspect before repair');
      await guard();
      const response = await fetch(`https://uploads.github.com/repos/${this.repository}/releases/${release.id}/assets?name=changelog-players.json`, {
        signal: AbortSignal.timeout(60000), method: 'POST', headers: { Authorization: `Bearer ${this.token}`, 'Content-Type': 'application/json' }, body: bytes,
      });
      if (!response.ok) throw Error(`GitHub upload failed: HTTP ${response.status}; resume the same tag`);
      const uploaded = await response.json();
      const confirmed = await this.request(`/releases/assets/${uploaded.id}`, { headers: { Accept: 'application/octet-stream' } }, true);
      if (!confirmed || confirmed.toString('utf8') !== bytes) throw Error('Uploaded asset could not be verified; keep the release draft');
    }
    if (release.draft) { await guard(); release = await this.request(`/releases/${release.id}`, { method: 'PATCH', body: JSON.stringify({ draft: false }) }); }
    if (release.draft || release.tag_name !== tag) throw Error('Release publication was not confirmed');
  }
}

function push(root, refspec) {
  const remote = git(root, ['remote', 'get-url', 'origin']).trim();
  const env = process.env.GH_TOKEN && /^https:\/\/github\.com\/Astro-Rift-Games\/ProjectGrimhold(?:\.git)?$/.test(remote) ? {
    GIT_CONFIG_COUNT: '1',
    GIT_CONFIG_KEY_0: 'http.https://github.com/.extraheader',
    GIT_CONFIG_VALUE_0: `AUTHORIZATION: basic ${Buffer.from(`x-access-token:${process.env.GH_TOKEN}`).toString('base64')}`,
    GIT_TERMINAL_PROMPT: '0',
  } : { GIT_TERMINAL_PROMPT: '0' };
  git(root, ['push', 'origin', refspec], env);
}
// Dedicated runner still must have no ambient credentials on disk. Child checks
// never inherit publication/API tokens or Git's process-local authentication.
function validationEnvironment() {
  return Object.fromEntries(Object.entries(process.env).filter(([name]) => !/TOKEN|SECRET|PASSWORD|CREDENTIAL|GIT_CONFIG|GIT_ASKPASS/i.test(name)));
}
function validateBackend(state) {
  const backend = join(state.candidate, 'backend');
  if (!existsSync(join(backend, 'package.json'))) return;
  if (!existsSync(join(backend, 'package-lock.json'))) throw Error('Mandatory backend validation requires its lockfile');
  const npm = [process.env.npm_execpath, join(dirname(process.execPath), 'node_modules/npm/bin/npm-cli.js'), join(dirname(process.execPath), '../lib/node_modules/npm/bin/npm-cli.js')].find(path => path && isAbsolute(path) && existsSync(path));
  if (!npm) throw Error('Mandatory backend validation requires the existing npm CLI');
  const options = { cwd: backend, stdio: 'inherit', env: { ...validationEnvironment(), MONGOMS_DISABLE_POSTINSTALL: '1' }, timeout: 1200000 };
  execFileSync(process.execPath, [npm, 'ci', '--ignore-scripts'], options);
  for (const script of ['test', 'test:concurrency', 'test:integration']) {
    execFileSync(process.execPath, [npm, 'run', script], options);
  }
}
function unityValidation(state) {
  const editor = process.env.UNITY_EDITOR_PATH;
  if (!editor || !isAbsolute(editor) || !existsSync(editor)) throw Error('Mandatory licensed Unity Editor missing: set UNITY_EDITOR_PATH on a dedicated runner');
  const project = join(state.candidate, 'Project Grimhold');
  const expectedEditor = /^m_EditorVersion: (.+)$/m.exec(readFileSync(join(project, 'ProjectSettings/ProjectVersion.txt'), 'utf8'))?.[1];
  const executableVersion = execFileSync(editor, ['-version'], { encoding: 'utf8', timeout: 60000 }).trim();
  if (!expectedEditor || !executableVersion.includes(expectedEditor)) throw Error(`Unity Editor must match ${expectedEditor}`);
  execFileSync(process.execPath, ['--test'], { cwd: join(state.candidate, 'tools/changelog'), stdio: 'inherit', env: validationEnvironment(), timeout: 600000 });
  validateBackend(state);
  const versionArgs = state.plan.version ? ['-releaseVersion', state.plan.version] : ['-baselineVersion', state.baselineVersion];
  execFileSync(editor, ['-batchmode', '-projectPath', project, '-executeMethod', 'ReleaseValidation.Validate', ...versionArgs, '-logFile', join(state.output, 'unity-validation.log')], { stdio: 'inherit', env: validationEnvironment(), timeout: 900000 });
  for (const platform of ['EditMode', 'PlayMode']) {
    const results = join(state.output, `${platform}.xml`);
    execFileSync(editor, ['-batchmode', '-projectPath', project, '-runTests', '-testPlatform', platform, '-testResults', results, '-logFile', join(state.output, `${platform}.log`)], { stdio: 'inherit', env: validationEnvironment(), timeout: 1800000 });
    const xml = readFileSync(results, 'utf8');
    const count = Number(/<test-run\b[^>]*\btotal=\x22(\d+)\x22/.exec(xml)?.[1] ?? 0);
    if (platform === 'PlayMode' && [...xml.matchAll(/<test-case\b[^>]*fullname=\x22ApplicationVersionLabelTests\.[^\x22]*\x22[^>]*result=\x22Passed\x22/g)].length < 4) throw Error('Mandatory ApplicationVersionLabel tests did not all pass');
    if (count <= 0 || !/<test-run\b[^>]*\bresult="Passed"/.test(xml) || !/<test-run\b[^>]*\bfailed="0"/.test(xml)) throw Error(`${platform} tests did not report Passed with zero failures`);
  }
}

async function main(args) {
  const usage = 'Usage: node tools/changelog/scripts/weekly-release.js --root REPO --output OUT [--publish]';
  const options = {};
  while (args.length) {
    const key = args.shift();
    if (!['--root', '--output', '--publish'].includes(key) || key in options) throw Error(usage);
    if (key === '--publish') { options[key] = true; continue; }
    const value = args.shift(); if (!value || value.startsWith('--')) throw Error(usage); options[key] = value;
  }
  if (!options['--root'] || !options['--output']) throw Error(usage);
  const root = resolve(options['--root']);
  if (options['--publish']) {
    if (!process.env.GH_TOKEN) throw Error('GH_TOKEN is required before any publication operation');
    if (process.env.WEEKLY_RELEASE_ENABLED !== 'true' || process.env.WEEKLY_RELEASE_BOOTSTRAPPED !== 'true' || process.env.GITHUB_REF !== 'refs/heads/main') throw Error('Publication is disabled until explicit bootstrap/activation on main');
    const remote = git(root, ['remote', 'get-url', 'origin']).trim();
    if (!/^https:\/\/github\.com\/Astro-Rift-Games\/ProjectGrimhold(?:\.git)?$/.test(remote)) throw Error('Official publication remote mismatch');
  }
  const api = process.env.GH_TOKEN ? new GitHubReleases(process.env.GH_TOKEN) : undefined;
  const state = await prepareCandidate(root, options['--output'], { api });
  await publishCandidate(state, { dryRun: !options['--publish'], validate: unityValidation, api });
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `## Weekly release

Phase: **${state.phase}**. Metadata only; no game build produced.
`);
  console.log(JSON.stringify({ phase: state.phase, version: state.plan?.version ?? null, target: state.target, report: join(state.output, 'state.json'), buildValidated: false }));
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main(process.argv.slice(2)).catch(error => { console.error(error.message); process.exitCode = 1; });
}
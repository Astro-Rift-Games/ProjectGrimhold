import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { git, readCommits } from '../src/git.js';
import { createReleasePlan, writePlannedRelease } from '../src/release-plan.js';
const scratch = mkdtempSync(join(tmpdir(), 'grimhold-changelog-pipeline-'));
console.log('Pipeline scratch: ' + scratch);
let index = 0;
const cli = fileURLToPath(new URL('../src/cli.js', import.meta.url));
function repository() {
  const root = join(scratch, String(index++)); mkdirSync(root);
  git(root, ['init', '--initial-branch=New-Testing']);
  git(root, ['config', 'user.name', 'Test']); git(root, ['config', 'user.email', 'test@example.invalid']);
  git(root, ['config', 'core.autocrlf', 'false']);
  mkdirSync(join(root, 'tools/changelog'), {recursive:true});
  writeFileSync(join(root, 'tools/changelog/player-overrides.json'), JSON.stringify({schemaVersion:1, overrides:[]}));
  return root;
}
function commit(root, title, body = '') {
  git(root, ['add', '.']); git(root, ['-c','commit.gpgsign=false','commit','--allow-empty','-m',title,'-m',body], {GIT_AUTHOR_DATE:'2026-01-02T00:00:00Z',GIT_COMMITTER_DATE:'2026-01-02T00:00:00Z'});
  return git(root,['rev-parse','HEAD']).trim();
}
function run(root, ...args) { return spawnSync(process.execPath,[cli,...args,'--root',root],{encoding:'utf8'}); }
test('Git enrichment preserves exact paths and supplies stable patch evidence', () => {
  const root=repository(); const path='Project Grimhold/Assets/Scripts/Inventory/Slot Rules.cs';
  mkdirSync(join(root,'Project Grimhold/Assets/Scripts/Inventory'),{recursive:true});
  writeFileSync(join(root,path),'one'); const sha=commit(root,'Update inventory');
  const c=readCommits(root,'HEAD').find(c=>c.sha===sha);
  assert.ok(c.files.includes(path));
});
test('strict public validation rejects before writing any generated output', () => {
  const root=repository(); commit(root,'fix: repair','Player-Fix: typo');
  const result=run(root,'generate','--strict');
  assert.equal(result.status,1); assert.match(result.stderr,/Unknown player trailer/);
  assert.equal(existsSync(join(root,'CHANGELOG_DEV.md')),false);
  assert.equal(existsSync(join(root,'docs/changelog')),false);
});
test('developer generation includes archives, audits drift and remains stable after automation', () => {
  const root=repository(); commit(root,'feat(inventory): physical inventory slots','Player-Added: Carry supplies.');
  assert.equal(run(root,'generate').status,0);
  assert.ok(existsSync(join(root,'docs/changelog/report.json')));
  const before=readFileSync(join(root,'docs/changelog/report.json'),'utf8');
  commit(root,'chore(changelog): update developer changelog [skip ci]');
  assert.equal(run(root,'generate').status,0);
  assert.equal(readFileSync(join(root,'docs/changelog/report.json'),'utf8'),before);
  const archive=readdirSync(join(root,'docs/changelog')).find(name=>name!== 'README.md' && name.endsWith('.md'));
  assert.ok(archive); writeFileSync(join(root,'docs/changelog',archive),'drift');
  const check=run(root,'check'); assert.equal(check.status,1); assert.match(check.stdout,/Differs: docs\/changelog/);
});
test('release preparation refuses missing public notes before any writes', () => {
  const root=repository(); commit(root,'feat: internal only'); const plan=createReleasePlan(root);
  assert.throws(()=>writePlannedRelease(root,plan),/no (visible player|player-facing|public) notes/i);
  assert.equal(existsSync(join(root,'CHANGELOG_DEV.md')),false);
});

test('repeated subjects only deduplicate when actual patches match across releases', () => {
  const root=repository(); writeFileSync(join(root,'data.txt'),'one\n'); const first=commit(root,'feat: update','Player-Added: New adventure.');
  git(root,['tag','v1.0.0']);
  writeFileSync(join(root,'data.txt'),'two\n'); commit(root,'feat: update','Player-Added: New adventure.');
  assert.equal(createReleasePlan(root).version,'1.1.0');
  assert.equal(run(root,'generate').status,0);
  assert.equal(JSON.parse(readFileSync(join(root,'docs/changelog/report.json'))).canonicalEntries,2);
});
test('manual reapplications preserve distinct canonical entries and all originals', () => {
  const root=repository(); writeFileSync(join(root,'data.txt'),'one\n'); const first=commit(root,'feat: update','Player-Added: New adventure.');
  git(root,['tag','v1.0.0']);
  writeFileSync(join(root,'data.txt'),'two\n'); commit(root,'chore: reset file');
  writeFileSync(join(root,'data.txt'),'one\n'); commit(root,'fix: restore file');
  writeFileSync(join(root,'data.txt'),'two\n'); commit(root,'chore: reset file');
  // Both identical modifications are non-empty; the metadata alone never proves equivalence.
  const commits=readCommits(root,'HEAD'); const resets=commits.filter(c=>c.subject==='chore: reset file');
  assert.equal(resets.length,2); assert.ok(resets[0].patchId); assert.equal(resets[0].patchId,resets[1].patchId);
  assert.equal(run(root,'generate').status,0);
  const report=JSON.parse(readFileSync(join(root,'docs/changelog/report.json')));
  assert.equal(report.rawCommits,4); assert.equal(report.canonicalEntries,4);
});

test('restoration after a published revert is new impact, not duplicate history', () => {
  const root=repository(); const original=commit(root,'feat: doors','Player-Added: Doors open.'); git(root,['tag','v1.0.0']);
  const reverted=commit(root,'revert: doors','This reverts commit '+original+'.'); git(root,['tag','v1.0.1']);
  commit(root,'feat: doors','Player-Added: Doors open.\n\n(cherry picked from commit '+original+')');
  assert.equal(createReleasePlan(root).version,'1.1.0');
});
test('revert of a published functional revert warrants an impact patch', () => {
  const root=repository(); const original=commit(root,'feat: doors','Player-Added: Doors open.'); git(root,['tag','v1.0.0']);
  const reverted=commit(root,'revert: doors','This reverts commit '+original+'.'); git(root,['tag','v1.0.1']);
  commit(root,'revert: restore doors','This reverts commit '+reverted+'.');
  assert.equal(createReleasePlan(root).version,'1.0.2');
});

test('release-boundary changes remove only owned obsolete archives and check detects them', () => {
  const root=repository(); const first=commit(root,'feat: January','Player-Added: January adventure.');
  git(root,['-c','commit.gpgsign=false','commit','--allow-empty','-m','fix: February','-m','Player-Fixed: February doors.'],{GIT_AUTHOR_DATE:'2026-02-02T00:00:00Z',GIT_COMMITTER_DATE:'2026-02-02T00:00:00Z'});
  assert.equal(run(root,'generate').status,0);
  const obsolete=join(root,'docs/changelog/history-2026-02.md'); assert.ok(existsSync(obsolete));
  git(root,['tag','v1.0.0',first]);
  assert.equal(run(root,'generate').status,0); assert.equal(existsSync(obsolete),false);
  assert.ok(existsSync(join(root,'docs/changelog/unreleased.md')));
  assert.equal(run(root,'check').status,0);
  writeFileSync(obsolete,'STALE FALSE HISTORY');
  const result=run(root,'check'); assert.equal(result.status,1); assert.match(result.stderr+result.stdout,/Unexpected|Unmanaged|obsolete/i);
  assert.equal(readFileSync(obsolete,'utf8'),'STALE FALSE HISTORY');
});

test('invalid ownership inventory cannot escape the generated directory or mutate outputs', () => {
  const root=repository(); commit(root,'feat: content','Player-Added: New content.'); assert.equal(run(root,'generate').status,0);
  const main=readFileSync(join(root,'CHANGELOG_DEV.md'),'utf8');
  writeFileSync(join(root,'user.txt'),'keep user data');
  const manifest=join(root,'docs/changelog/generated-files.json');
  writeFileSync(manifest,JSON.stringify({schemaVersion:1,generator:'tools/changelog',files:['docs/changelog/../../user.txt']}));
  const result=run(root,'generate'); assert.equal(result.status,1); assert.match(result.stderr,/Invalid generated history inventory/);
  assert.equal(readFileSync(join(root,'user.txt'),'utf8'),'keep user data');
  assert.equal(readFileSync(join(root,'CHANGELOG_DEV.md'),'utf8'),main);
});

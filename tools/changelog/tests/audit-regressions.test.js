import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { git, readCommits } from '../src/git.js';
import { parseCommit, developerVersions } from '../src/commits.js';
import { classifyCommit } from '../src/classify.js';
import { createReleasePlan } from '../src/release-plan.js';
const scratch = mkdtempSync(join(tmpdir(), 'grimhold-changelog-audit-regressions-'));
console.log('Audit regression scratch: ' + scratch);
let sequence = 0;
const cli = fileURLToPath(new URL('../src/cli.js', import.meta.url));
function repository() {
  const root = join(scratch, String(sequence++)); mkdirSync(root);
  git(root, ['init', '--initial-branch=main']);
  git(root, ['config', 'user.name', 'Audit Test']); git(root, ['config', 'user.email', 'test@example.invalid']);
  git(root, ['config', 'core.autocrlf', 'false']);
  mkdirSync(join(root, 'tools/changelog'), { recursive:true });
  writeFileSync(join(root,'tools/changelog/player-overrides.json'),'{"schemaVersion":1,"overrides":[]}');
  return root;
}
function commit(root, subject, body='') {
  git(root,['add','.']); git(root,['-c','commit.gpgsign=false','commit','--allow-empty','-m',subject,'-m',body]);
  return git(root,['rev-parse','HEAD']).trim();
}
function parsed(subject, files) { return parseCommit({sha:'a'.repeat(40),subject,body:'',files,date:'2026-01-01T00:00:00Z'}); }
test('historical artifact evidence precedes generic Add inference, not explicit types', () => {
  for (const subject of ['Add persistence strategy doc for Vertical Slice','Add VisibilityArchitechture.md','Add base contracts architecture specification']) {
    const c=classifyCommit(parsed(subject,['Docs/Architecture/Contract.md'])); assert.equal(c.section,'Documentation'); assert.equal(c.secondary,true);
  }
  assert.equal(classifyCommit(parsed('Add regression harness',['Assets/Tests/EditMode/InventoryTests.cs'])).section,'Tests');
  assert.equal(classifyCommit(parsed('feat: add inventory slots',['Docs/Architecture/Inventory.md'])).section,'Features');
  assert.equal(classifyCommit(parsed('Add inventory slots',['Assets/Scripts/Inventory.cs','Docs/Architecture/Inventory.md'])).section,'Features');
});
test('adding a build profile and updating project configuration is infrastructure', () => {
  const c=classifyCommit(parsed('Add Windows build profile; update URP & project',['Assets/Settings/Build Profiles/Windows.asset','ProjectSettings/EditorBuildSettings.asset']));
  assert.equal(c.section,'Architecture & Infrastructure');
});
test('manual inversion and later reapplication remain separate changes and version impact', () => {
  const root=repository(); writeFileSync(join(root,'config.txt'),'base\n'); commit(root,'chore: base');
  writeFileSync(join(root,'config.txt'),'active\n'); const first=commit(root,'fix: update config','Player-Fixed: Connections work.'); git(root,['tag','v1.0.0']);
  writeFileSync(join(root,'config.txt'),'base\n'); commit(root,'chore: manual configuration reset');
  writeFileSync(join(root,'config.txt'),'active\n'); const second=commit(root,'fix: update config','Player-Fixed: Connections work.');
  const commits=readCommits(root,'HEAD'); const versions=developerVersions([{version:'Unreleased',commits}]);
  assert.equal(versions[0].entries.length,4); assert.equal(versions[0].entries.filter(c=>c.refs.includes(first)||c.refs.includes(second)).length,2);
  assert.equal(createReleasePlan(root).version,'1.0.1');
  git(root,['tag','v1.0.1']);
  const result=spawnSync(process.execPath,[cli,'generate','--root',root],{encoding:'utf8'}); assert.equal(result.status,0,result.stderr);
  const notes=JSON.parse(readFileSync(join(root,'changelog-players.json'))).releases;
  assert.equal(notes[0].categories[2].entries.length,1); assert.equal(notes[1].categories[2].entries.length,1);
});
test('independent branch equivalents retain stable-patch deduplication', () => {
  const root=repository(); writeFileSync(join(root,'config.txt'),'base\n'); const base=commit(root,'chore: base');
  git(root,['checkout','-b','feature']); writeFileSync(join(root,'config.txt'),'active\n'); commit(root,'fix: update config','Feature branch counterpart.');
  git(root,['checkout','main']); writeFileSync(join(root,'config.txt'),'active\n'); commit(root,'fix: update config','Main branch counterpart.');
  git(root,['-c','commit.gpgsign=false','merge','--no-ff','feature','-m','Merge independent equivalents']);
  const versions=developerVersions([{version:'Unreleased',commits:readCommits(root,'HEAD')}]); assert.equal(versions[0].entries.length,2);
  assert.equal(versions[0].entries.find(c=>c.type==='fix').refs.length,2);
});
test('strict release notes retain empty-release diagnostics for merge tags', () => {
  const root=repository(); commit(root,'chore: base'); git(root,['checkout','-b','feature']); commit(root,'docs: internal');
  git(root,['checkout','main']); commit(root,'chore: internal'); git(root,['-c','commit.gpgsign=false','merge','--no-ff','feature','-m','Merge internal history']); git(root,['tag','v1.0.0']);
  const result=spawnSync(process.execPath,[cli,'release-notes','--root',root,'--strict','--tag','v1.0.0'],{encoding:'utf8'});
  assert.equal(result.status,1); assert.match(result.stderr,/no visible player notes/); assert.equal(result.stdout,'');
});
test('developer writer checkout includes every generated archive path', () => {
  const workflow=readFileSync(new URL('../../../.github/workflows/changelog.yml',import.meta.url),'utf8');
  const writer=workflow.split('  update-developer-changelog:')[1];
  assert.match(writer,/sparse-checkout:\s*\|\s+tools\/changelog\s+docs\/changelog/);
});

test('a branch equivalent cannot transitively collapse two ancestry-related executions', () => {
  const make=char=>parseCommit({sha:char.repeat(40),subject:'fix: config',body:'',date:'2026-01-01T00:00:00Z',patchId:'f'.repeat(40)});
  const first=make('a'), repeated=make('b'), independent=make('c');
  first.patchRelatedShas=[repeated.sha]; repeated.patchRelatedShas=[first.sha];
  for(const commits of [[independent,repeated,first],[repeated,independent,first],[repeated,first,independent]]){
    const entries=developerVersions([{version:'Unreleased',commits}])[0].entries;
    assert.equal(entries.length,2); assert.ok(!entries.some(entry=>entry.refs.includes(first.sha)&&entry.refs.includes(repeated.sha)));
  }
});

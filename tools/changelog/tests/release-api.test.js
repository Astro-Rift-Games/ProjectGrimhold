import { test } from 'node:test';
import assert from 'node:assert/strict';
import { GitHubReleases } from '../scripts/weekly-release.js';
const notes = 'Release notes\n';
const bytes = '{"schemaVersion":1}\n';
const sha = 'a'.repeat(40);
function transport(t, responses) {
  const original = globalThis.fetch;
  const calls = [];
  globalThis.fetch = async (url, options = {}) => {
    calls.push({ url, method: options.method ?? 'GET', body: options.body });
    const reply = responses.shift();
    if (!reply) throw Error('Unexpected request');
    if (reply instanceof Error) throw reply;
    return new Response(typeof reply.body === 'string' ? reply.body : JSON.stringify(reply.body), { status: reply.status ?? 200 });
  };
  t.after(() => { globalThis.fetch = original; });
  return calls;
}
function release(draft = false) { return { id: 1, tag_name: 'v0.1.0', body: notes, draft, prerelease: false }; }
test('existing published release and identical asset cause no writes', async t => {
  const calls = transport(t, [{ body: release() }, { body: [{ id: 2, name: 'changelog-players.json' }] }, { body: bytes }]);
  await new GitHubReleases('fake-token').publish('v0.1.0', sha, notes, bytes);
  assert.equal(calls.filter(c => c.method !== 'GET').length, 0);
});
test('existing published release with different notes is never overwritten', async t => {
  const calls = transport(t, [{ body: { ...release(), body: 'different' } }]);
  await assert.rejects(new GitHubReleases('fake').publish('v0.1.0', sha, notes, bytes), /differs/);
  assert.equal(calls.length, 1);
});
test('existing release asset mismatch is never clobbered', async t => {
  const calls = transport(t, [{ body: release() }, { body: [{ id: 2, name: 'changelog-players.json' }] }, { body: 'wrong' }]);
  await assert.rejects(new GitHubReleases('fake').publish('v0.1.0', sha, notes, bytes), /differs/);
  assert.equal(calls.every(c => c.method === 'GET'), true);
});
test('draft resumes by checking its existing asset before publication', async t => {
  let guards = 0;
  const calls = transport(t, [{ body: release(true) }, { body: [{ id: 2, name: 'changelog-players.json' }] }, { body: bytes }, { body: release() }]);
  await new GitHubReleases('fake').publish('v0.1.0', sha, notes, bytes, () => { guards++; });
  assert.equal(guards, 1); assert.equal(calls.at(-1).method, 'PATCH');
});
test('new release is a draft until upload content is verified', async t => {
  let guards = 0;
  const calls = transport(t, [{ status: 404, body: {} }, { body: release(true) }, { body: [] }, { body: { id: 2 } }, { body: bytes }, { body: release() }]);
  await new GitHubReleases('fake').publish('v0.1.0', sha, notes, bytes, () => { guards++; });
  assert.equal(JSON.parse(calls[1].body).draft, true);
  assert.equal(guards, 3); assert.equal(calls.at(-1).method, 'PATCH');
});
test('failed upload leaves the draft unpublished', async t => {
  const calls = transport(t, [{ status: 404, body: {} }, { body: release(true) }, { body: [] }, { status: 403, body: {} }]);
  await assert.rejects(new GitHubReleases('fake').publish('v0.1.0', sha, notes, bytes), /upload failed/);
  assert.equal(calls.some(c => c.method === 'PATCH'), false);
});
test('unverified upload is not published', async t => {
  const calls = transport(t, [{ status: 404, body: {} }, { body: release(true) }, { body: [] }, { body: { id: 2 } }, { body: 'wrong' }]);
  await assert.rejects(new GitHubReleases('fake').publish('v0.1.0', sha, notes, bytes), /could not be verified/);
  assert.equal(calls.some(c => c.method === 'PATCH'), false);
});
test('permission errors are not treated as absence', async t => {
  const calls = transport(t, [{ status: 403, body: {} }]);
  await assert.rejects(new GitHubReleases('fake').publish('v0.1.0', sha, notes, bytes), /HTTP 403/);
  assert.equal(calls.length, 1);
});
test('unknown create outcome causes no blind second mutation', async t => {
  const calls = transport(t, [{ status: 404, body: {} }, Error('network outcome unknown')]);
  await assert.rejects(new GitHubReleases('fake').publish('v0.1.0', sha, notes, bytes), /unknown/);
  assert.equal(calls.filter(c => c.method === 'POST').length, 1);
});
test('branch guard failure prevents release creation', async t => {
  const calls = transport(t, [{ status: 404, body: {} }]);
  await assert.rejects(new GitHubReleases('fake').publish('v0.1.0', sha, notes, bytes, () => { throw Error('branch changed'); }), /changed/);
  assert.equal(calls.length, 1);
});
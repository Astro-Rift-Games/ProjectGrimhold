'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const request = require('supertest');
const jwt = require('jsonwebtoken');
const { spawn } = require('node:child_process');
const path = require('node:path');
const db = require('./testDb');
const app = require('../../src/app');
const env = require('../../src/config/env');
const Character = require('../../src/models/Character');
const Result = require('../../src/models/AuthoritativeExtractionResult');
const Inventory = require('../../src/services/InventoryService');
const { createInstance } = require('../../src/services/ItemInstances');
const { migrateItemInstances } = require('../../scripts/migrate_item_instances');
const modifiers = () => ({ primary: { attribute: 'strength', grade: 'E' }, gemSocketCount: 2, gems: [null, null] });

test('v3 HTTP durable instance ownership and extraction', async t => {
  await db.connect();
  t.after(async () => { process.env.INVENTORY_INSTANCE_PROTOCOL_GATE = 'false'; await db.closeDatabase(); });
  let accountId, token, instance;
  t.beforeEach(async () => {
    await db.clearDatabase(); process.env.INVENTORY_INSTANCE_PROTOCOL_GATE = 'false';
    accountId = new mongoose.Types.ObjectId(); token = jwt.sign({ sub: accountId.toString() }, env.jwtSecret);
    instance = createInstance('arming_sword', modifiers());
    await Character.create({ accountId, name: 'V3', inventory: { inventorySchemaVersion: 3, currency: 500,
      stash: [{ slot: 9, lootId: 'arming_sword', amount: 1, instance }], loadout: [{ slot: 3, lootId: 'RES_RUBY_GEM', amount: 2 }] } });
  });
  const send = (method, route, body, protocol) => {
    const req = request(app)[method](`/character${route}`).set('Authorization', `Bearer ${token}`);
    if (protocol) req.set('X-Grimhold-Inventory-Protocol', protocol);
    return body === undefined ? req : req.send(body);
  };
  const install = () => ({ transactionId: 'install', weaponList: 'stash', weaponSlot: 9, instanceId: instance.instanceId,
    gemList: 'loadout', gemSlot: 3, gemLootId: 'RES_RUBY_GEM', expectedRevision: 0 });
  const get = async () => {
    const response = await send('get', '/me/inventory', undefined, '3'); assert.equal(response.status, 200, JSON.stringify(response.body)); return response.body;
  };
  async function reject(route, body, code, status = 409) {
    const before = await get(); const raw = await Character.findOne({ accountId }).lean();
    const response = await send('post', route, body);
    assert.equal(response.status, status, JSON.stringify(response.body)); assert.equal(response.body.error, code);
    assert.deepEqual(await get(), before); assert.deepEqual(await Character.findOne({ accountId }).lean(), raw);
  }
  await t.test('gem endpoint persists both lists, one revision and receipt; replay returns the stored outcome even after a move', async () => {
    const before = await get();
    const installed = await send('post', '/me/inventory/gems/install', install());
    assert.equal(installed.status, 200, JSON.stringify(installed.body));
    const expected = { stash: [{ ...before.stash[0], instance: { ...instance, modifiers: { ...modifiers(), gems: ['RES_RUBY_GEM', null] } } }],
      loadout: [{ slot: 3, lootId: 'RES_RUBY_GEM', amount: 1 }], revision: 1 };
    assert.deepEqual(installed.body, expected);
    assert.deepEqual(await get(), { ...before, ...expected });
    await Inventory.moveToLoadout(accountId, 'arming_sword', 1, 1, 9);
    const raw = await Character.findOne({ accountId }).lean();
    const replay = await send('post', '/me/inventory/gems/install', install());
    assert.equal(replay.status, 200); assert.deepEqual(replay.body, { ...expected, alreadySecured: true });
    assert.deepEqual(await Character.findOne({ accountId }).lean(), raw);
    await reject('/me/inventory/gems/install', { ...install(), instanceId: new mongoose.Types.ObjectId().toHexString() }, 'TRANSACTION_ID_REUSED');
  });
  for (const [patch, code] of [[{ weaponSlot: 8 }, 'INSTANCE_MISMATCH'], [{ gemSlot: 8 }, 'GEM_NOT_AVAILABLE'],
    [{ expectedRevision: 1 }, 'REVISION_CONFLICT']]) await t.test(`gem rejection ${code} leaves raw storage unchanged`, async () => {
    await reject('/me/inventory/gems/install', { ...install(), ...patch }, code);
  });
  await t.test('equip intention requires ID, cannot replace modifiers, and unequip preserves installed gems', async () => {
    const response = await send('put', '/me/inventory/prepared-equipment', { weaponSlot1: { lootId: 'arming_sword', instanceId: instance.instanceId }, expectedRevision: 0 });
    assert.equal(response.status, 200, JSON.stringify(response.body));
    assert.deepEqual(response.body.preparedEquipment.weaponSlot1, { lootId: 'arming_sword', instance });
    await reject('/me/inventory/gems/install', { ...install(), expectedRevision: 1 }, 'INSTANCE_MISMATCH');
    const unequipped = await send('put', '/me/inventory/prepared-equipment', { weaponSlot1: null, expectedRevision: 1 });
    assert.equal(unequipped.status, 200, JSON.stringify(unequipped.body));
    assert.deepEqual(unequipped.body.loadout, [{ slot: 0, lootId: 'arming_sword', amount: 1, instance }, { slot: 3, lootId: 'RES_RUBY_GEM', amount: 2 }]);
    assert.equal(unequipped.body.preparedEquipment.weaponSlot1, null);
  });
  await t.test('model saves reject invalid nested instances without modifying persisted ownership', async () => {
    const before = await get();
    const character = await Character.findOne({ accountId });
    character.inventory.stash[0].instance.modifiers.primary.grade = ['E'];
    character.markModified('inventory.stash');
    await assert.rejects(() => character.save(), /INVALID_INVENTORY/);
    assert.deepEqual(await get(), before);
  });
  await t.test('protocol 3 applies to creation and rejects an unmigrated character without writes', async () => {
    process.env.INVENTORY_INSTANCE_PROTOCOL_GATE = 'true';
    assert.equal((await send('get', '/me/inventory', undefined, '2')).status, 426);
    const other = new mongoose.Types.ObjectId(); token = jwt.sign({ sub: other.toString() }, env.jwtSecret);
    const created = await send('post', '/me', { name: 'NewV3' }, '3'); assert.equal(created.status, 201, JSON.stringify(created.body));
    assert.equal((await Character.findOne({ accountId: other })).inventory.inventorySchemaVersion, 3);
    const inventory = await get(); assert.equal(inventory.inventorySchemaVersion, 3); assert.equal(inventory.preparedEquipment.weaponSlot1, null);
    await Character.updateOne({ accountId: other }, { $set: { 'inventory.inventorySchemaVersion': 2 } });
    const response = await send('get', '/me/inventory', undefined, '3'); assert.equal(response.status, 409); assert.equal(response.body.error, 'INVENTORY_MIGRATION_REQUIRED');
  });
  await t.test('host publishes ID-free variants and equipment; commit reissues IDs and rejects invalid modifiers before consuming result', async () => {
    await Inventory.savePendingReservation(accountId, 'reserve');
    const published = await send('post', '/raid/extraction-result', { raidId: 'raid', resultSequence: 1, experienceGranted: 0,
      items: [{ lootId: 'arming_sword', amount: 1, modifiers: modifiers() }, { lootId: 'arming_sword', amount: 1 }],
      preparedEquipment: { weaponSlot1: { lootId: 'arming_sword', modifiers: modifiers() } } });
    assert.equal(published.status, 200, JSON.stringify(published.body));
    const storedResult = await Result.findOne({ raidId: 'raid' }).lean();
    assert.equal(storedResult.items[0].instance, undefined); assert.equal(storedResult.preparedEquipment.weaponSlot1.instance, undefined);
    const committed = await send('post', '/me/extraction/commit', { raidId: 'raid', resultSequence: 1 });
    assert.equal(committed.status, 201, JSON.stringify(committed.body)); assert.equal(committed.body.loadout.length, 2);
    const after = await get(); const ids = [after.preparedEquipment.weaponSlot1, ...after.loadout].map(item => item.instance.instanceId);
    assert.equal(new Set(ids).size, 3); assert.ok(!ids.includes(instance.instanceId));
    assert.deepEqual(after.loadout[0].instance.modifiers, modifiers());
    const replay = await send('post', '/me/extraction/commit', { raidId: 'raid', resultSequence: 1 });
    assert.equal(replay.status, 200); assert.deepEqual(replay.body, { ...committed.body, alreadySecured: true });
    assert.deepEqual(await get(), after);
  });
  await t.test('invalid extraction modifiers reject publication without creating an authoritative result', async () => {
    await Inventory.savePendingReservation(accountId, 'reserve');
    await reject('/raid/extraction-result', { raidId: 'bad', resultSequence: 1, experienceGranted: 0,
      items: [{ lootId: 'arming_sword', amount: 1, modifiers: { primary: { attribute: 'luck', grade: 'S' }, gemSocketCount: 0, gems: [] } }] }, 'INVALID_INVENTORY', 400);
    assert.equal(await Result.countDocuments({ raidId: 'bad' }), 0);
  });
});

function cli(uri, args = []) {
  const source = `require('dotenv').config = () => ({}); require('./scripts/migrate_item_instances').main(${JSON.stringify(args)}).then(code => { process.exitCode = code; });`;
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, ['-e', source], { cwd: path.resolve(__dirname, '../..'), env: { ...process.env, MONGODB_URI: uri }, windowsHide: true });
    let stdout = ''; let stderr = '';
    child.stdout.on('data', data => { stdout += data; }); child.stderr.on('data', data => { stderr += data; });
    child.on('error', reject); child.on('close', code => resolve({ code, stdout, stderr }));
  });
}
test('v3 migration CLI dry-run, ID-free results, revision and idempotence', async t => {
  await db.connect(); t.after(async () => { await db.closeDatabase(); });
  const characters = mongoose.connection.db.collection('characters'); const results = mongoose.connection.db.collection('authoritativeextractionresults');
  const accountId = new mongoose.Types.ObjectId();
  const character = { _id: new mongoose.Types.ObjectId(), accountId, name: 'Migrate', revision: 7,
    inventory: { inventorySchemaVersion: 2, currency: 123, stash: [{ slot: 8, lootId: 'arming_sword', amount: 3 }, { slot: 0, lootId: 'bone', amount: 9 }],
      loadout: [], preparedEquipment: { helmet: 'placeholder_helmet' }, pendingReservation: null } };
  const result = { _id: new mongoose.Types.ObjectId(), raidId: 'legacy', accountId: accountId.toString(), consumed: false,
    items: [{ lootId: 'arming_sword', amount: 2 }, { lootId: 'bone', amount: 4 }], preparedEquipment: { weaponSlot1: 'arming_sword' } };
  await characters.insertOne(character); await results.insertOne(result);
  const uri = `mongodb://${mongoose.connection.host}:${mongoose.connection.port}/${mongoose.connection.name}`;
  const dry = await cli(uri, ['--dry-run']);
  assert.equal(dry.code, 0, dry.stderr); assert.equal(dry.stderr, '');
  assert.deepEqual(await characters.findOne({ _id: character._id }), character); assert.deepEqual(await results.findOne({ _id: result._id }), result);
  assert.equal(JSON.parse(dry.stdout.trim().split('\n').at(-1)).planned, 1);
  const migrated = await cli(uri);
  assert.equal(migrated.code, 0, migrated.stderr); assert.equal(migrated.stderr, '');
  const after = await characters.findOne({ _id: character._id });
  assert.equal(after.revision, 8); assert.equal(after.inventory.inventorySchemaVersion, 3); assert.equal(after.inventory.currency, 123);
  assert.deepEqual(after.inventory.stash.map(i => [i.slot, i.amount]), [[8, 1], [0, 9], [1, 1], [2, 1]]);
  const resultAfter = await results.findOne({ _id: result._id });
  assert.deepEqual(resultAfter.items, [{ lootId: 'arming_sword', amount: 1, modifiers: { gemSocketCount: 0, gems: [] } },
    { lootId: 'arming_sword', amount: 1, modifiers: { gemSocketCount: 0, gems: [] } }, { lootId: 'bone', amount: 4 }]);
  assert.deepEqual(resultAfter.preparedEquipment.weaponSlot1, { lootId: 'arming_sword', modifiers: { gemSocketCount: 0, gems: [] } });
  const rerun = await cli(uri);
  assert.equal(rerun.code, 0); assert.equal(rerun.stderr, ''); assert.equal(JSON.parse(rerun.stdout.trim().split('\n').at(-1)).skipped, 1);
  assert.deepEqual(await characters.findOne({ _id: character._id }), after); assert.deepEqual(await results.findOne({ _id: result._id }), resultAfter);
  const usage = await cli(uri, ['--invalid']);
  assert.deepEqual(usage, { code: 2, stdout: '', stderr: 'Usage: node scripts/migrate_item_instances.js [--dry-run] [--help]\n' });
  assert.deepEqual(await characters.findOne({ _id: character._id }), after); assert.deepEqual(await results.findOne({ _id: result._id }), resultAfter);
});

test('v3 migration revision/result guards preserve concurrent and malformed data', async t => {
  await db.connect(); t.after(async () => { await db.closeDatabase(); });
  const characters = mongoose.connection.db.collection('characters'); const results = mongoose.connection.db.collection('authoritativeextractionresults');
  let character;
  t.beforeEach(async () => {
    await db.clearDatabase();
    character = { _id: new mongoose.Types.ObjectId(), accountId: new mongoose.Types.ObjectId(), name: 'Guarded', revision: 7,
      inventory: { inventorySchemaVersion: 2, currency: 123, stash: [{ slot: 8, lootId: 'shield', amount: 2 }], loadout: [] } };
    await characters.insertOne(character);
  });
  const wrapped = updateOne => ({ find: characters.find.bind(characters), findOne: characters.findOne.bind(characters), updateOne });
  await t.test('revision conflict re-reads and preserves the competing write before cutover', async () => {
    let attempts = 0;
    const summary = await migrateItemInstances(wrapped(async (filter, update) => {
      if (++attempts === 1) await characters.updateOne({ _id: character._id }, { $inc: { revision: 1, 'inventory.currency': 1 } });
      return characters.updateOne(filter, update);
    }), results, { report: () => {} });
    assert.equal(summary.migrated, 1); assert.equal(summary.conflicts, 1); assert.equal(summary.exitCode, 0); assert.equal(attempts, 2);
    const after = await characters.findOne({ _id: character._id });
    assert.equal(after.revision, 9); assert.equal(after.inventory.currency, 124); assert.equal(after.inventory.inventorySchemaVersion, 3);
    assert.deepEqual(after.inventory.stash.map(item => [item.slot, item.amount]), [[8, 1], [0, 1]]);
    assert.equal(new Set(after.inventory.stash.map(item => item.instance.instanceId)).size, 2);
  });
  await t.test('bounded conflicts abort without applying any migration data', async () => {
    let attempts = 0;
    const summary = await migrateItemInstances(wrapped(async () => { attempts++; return { matchedCount: 0 }; }), results, { report: () => {} });
    assert.equal(summary.aborted, 1); assert.equal(summary.conflicts, 4); assert.equal(summary.exitCode, 1); assert.equal(attempts, 4);
    assert.deepEqual(await characters.findOne({ _id: character._id }), character);
  });
  await t.test('result conflict prevents character cutover and retains the result', async () => {
    const result = { _id: new mongoose.Types.ObjectId(), accountId: String(character.accountId), items: [{ lootId: 'shield', amount: 2 }], preparedEquipment: {} };
    await results.insertOne(result);
    const guardedResults = { find: results.find.bind(results), updateOne: async () => ({ matchedCount: 0 }) };
    const summary = await migrateItemInstances(characters, guardedResults, { report: () => {} });
    assert.equal(summary.aborted, 1); assert.equal(summary.conflicts, 1); assert.equal(summary.exitCode, 1);
    assert.deepEqual(await characters.findOne({ _id: character._id }), character); assert.deepEqual(await results.findOne({ _id: result._id }), result);
  });
  await t.test('malformed result aborts before either collection is modified', async () => {
    const result = { _id: new mongoose.Types.ObjectId(), accountId: String(character.accountId), items: [{ lootId: 'shield', amount: 0 }] };
    await results.insertOne(result);
    const summary = await migrateItemInstances(characters, results, { report: () => {} });
    assert.equal(summary.aborted, 1); assert.equal(summary.exitCode, 1);
    assert.deepEqual(await characters.findOne({ _id: character._id }), character); assert.deepEqual(await results.findOne({ _id: result._id }), result);
  });
  await t.test('missing revision is guarded by absence and incremented from zero', async () => {
    await characters.updateOne({ _id: character._id }, { $unset: { revision: 1 } });
    const summary = await migrateItemInstances(characters, results, { report: () => {} });
    assert.equal(summary.migrated, 1); assert.equal(summary.exitCode, 0);
    const after = await characters.findOne({ _id: character._id }); assert.equal(after.revision, 1); assert.equal(after.inventory.inventorySchemaVersion, 3);
  });
});

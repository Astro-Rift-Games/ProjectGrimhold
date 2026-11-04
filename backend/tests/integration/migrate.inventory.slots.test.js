'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { MongoMemoryServer } = require('mongodb-memory-server');
const { spawn } = require('node:child_process');
const path = require('node:path');
const { migrateInventorySlots } = require('../../scripts/migrate_inventory_slots');

// Run the CLI entry point against only our disposable DB, never loading a local .env.
function cli(uri, args = []) {
  const source = `require('dotenv').config = () => ({});
    require('./scripts/migrate_inventory_slots').main(${JSON.stringify(args)})
      .then(code => { process.exitCode = code; });`;
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, ['-e', source], {
      cwd: path.resolve(__dirname, '../..'),
      env: { ...process.env, MONGODB_URI: uri }, windowsHide: true
    });
    let stdout = '';
    let stderr = '';
    child.stdout.on('data', data => { stdout += data; });
    child.stderr.on('data', data => { stderr += data; });
    child.on('error', reject);
    child.on('close', code => resolve({ code, stdout, stderr }));
  });
}

function legacy(_id, extra = {}) {
  return { _id, revision: 4, name: 'Unchanged', inventory: {
    currency: 19, stash: [
      { lootId: 'training_sword', amount: 2, extra: { preserve: true } },
      { lootId: 'bone', amount: 3 }, { lootId: 'arming_sword', amount: 4 }
    ], loadout: [{ lootId: 'unknown_item', amount: 7 }],
    preparedEquipment: { weaponSlot1: 'training_sword' },
    pendingReservation: { reservationId: 'r1', admittedAt: new Date(0),
      items: [{ lootId: 'bone', amount: 2, extra: 'reserved' }, { lootId: 'bone', amount: 3 }],
      preparedEquipment: { weaponSlot1: 'training_sword' } },
    appliedExtractionReceipts: [{ raidId: 'keep', resultSequence: 1 }]
  }, ...extra };
}

function expectedMigration(before) {
  return { ...before, revision: before.revision + 1, inventory: { ...before.inventory,
    inventorySchemaVersion: 2,
    stash: [
      { ...before.inventory.stash[0], lootId: 'arming_sword', amount: 6, slot: 0 },
      { ...before.inventory.stash[1], slot: 1 }
    ], loadout: [{ ...before.inventory.loadout[0], slot: 0 }],
    pendingReservation: { ...before.inventory.pendingReservation,
      items: [{ ...before.inventory.pendingReservation.items[0], amount: 5, slot: 0 }] }
  } };
}

test('inventory slot migration against MongoDB', async t => {
  const server = await MongoMemoryServer.create();
  t.after(async () => { await server.stop(); });
  const connection = await mongoose.createConnection(server.getUri()).asPromise();
  t.after(async () => { await connection.close(); });
  const db = connection.db;

  await t.test('CLI dry run writes nothing and reports each list and final counts', async () => {
    const collection = db.collection('characters');
    const before = legacy('cli');
    await collection.insertOne(before);
    const result = await cli(server.getUri(), ['--dry-run']);
    assert.equal(result.code, 0);
    assert.equal(result.stderr, '');
    const lines = result.stdout.trim().split('\n').map(line => JSON.parse(line));
    assert.equal(lines.length, 2);
    assert.equal(lines[0].characterId, 'cli');
    assert.equal(lines[0].status, 'planned');
    assert.equal(lines[0].lists.stash.duplicatesFolded, 1);
    assert.equal(lines[0].lists.stash.entriesBefore, 3);
    assert.equal(lines[0].lists.stash.entriesAfter, 2);
    assert.equal(lines[0].lists.stash.totalsCheck, true);
    assert.deepEqual(lines[0].lists.loadout.unknownIds, ['unknown_item']);
    assert.equal(lines[0].lists.loadout.overCapacity, false);
    assert.equal(lines[0].lists['pendingReservation.items'].duplicatesFolded, 1);
    assert.deepEqual(lines[1], { type: 'summary', dryRun: true, scanned: 1,
      planned: 1, migrated: 0, skipped: 0, aborted: 0, conflicts: 0, exitCode: 0 });
    assert.deepEqual(await collection.findOne({ _id: before._id }), before);

    const real = await cli(server.getUri());
    assert.equal(real.code, 0);
    assert.equal(real.stderr, '');
    assert.deepEqual(await collection.findOne({ _id: before._id }), expectedMigration(before));
    assert.equal(JSON.parse(real.stdout.trim().split('\n').at(-1)).migrated, 1);
    const after = await collection.findOne({ _id: before._id });
    const rerun = await cli(server.getUri());
    assert.equal(rerun.code, 0);
    assert.equal(rerun.stderr, '');
    assert.deepEqual(await collection.findOne({ _id: before._id }), after);
    assert.deepEqual(JSON.parse(rerun.stdout.trim().split('\n').at(-1)), {
      type: 'summary', dryRun: false, scanned: 1, planned: 0, migrated: 0,
      skipped: 1, aborted: 0, conflicts: 0, exitCode: 0
    });
  });

  await t.test('revision conflict rereads and recomputes from the concurrent writer', async () => {
    const collection = db.collection('conflict');
    const before = legacy('race');
    await collection.insertOne(before);
    let attempts = 0;
    const racingCollection = {
      find: (...args) => collection.find(...args),
      findOne: (...args) => collection.findOne(...args),
      async updateOne(filter, update) {
        attempts++;
        assert.equal(update.$inc.revision, 1);
        assert.equal(filter.revision, attempts === 1 ? 4 : 5);
        if (attempts === 1) {
          await collection.updateOne({ _id: 'race' }, { $inc: { revision: 1 },
            $push: { 'inventory.stash': { lootId: 'healthpotion', amount: 11, other: 'concurrent' } } });
        }
        return collection.updateOne(filter, update);
      }
    };
    const reports = [];
    const summary = await migrateInventorySlots(racingCollection, { report: record => reports.push(record) });
    assert.equal(attempts, 2);
    assert.equal(summary.conflicts, 1);
    assert.equal(summary.migrated, 1);
    assert.equal(summary.exitCode, 0);
    const expected = expectedMigration(before);
    expected.revision = 6;
    expected.inventory.stash.push({ lootId: 'healthpotion', amount: 11, other: 'concurrent', slot: 2 });
    assert.deepEqual(await collection.findOne({ _id: 'race' }), expected);
    assert.equal(reports[0].lists.stash.entriesBefore, 4);
  });

  await t.test('bounded revision retries abort without a migration write', async () => {
    const collection = db.collection('busy');
    const before = legacy('busy');
    await collection.insertOne(before);
    let attempts = 0;
    const busyCollection = {
      find: (...args) => collection.find(...args),
      findOne: (...args) => collection.findOne(...args),
      async updateOne(filter, update) {
        attempts++;
        await collection.updateOne({ _id: 'busy' }, { $inc: { revision: 1 } });
        return collection.updateOne(filter, update);
      }
    };
    const reports = [];
    const summary = await migrateInventorySlots(busyCollection, {
      maxRetries: 2, report: record => reports.push(record)
    });
    assert.equal(attempts, 3);
    assert.equal(summary.aborted, 1);
    assert.equal(summary.conflicts, 3);
    assert.equal(summary.migrated, 0);
    assert.equal(summary.exitCode, 1);
    assert.equal(reports[0].reason, 'REVISION_CONFLICT');
    assert.deepEqual(await collection.findOne({ _id: 'busy' }), { ...before, revision: 7 });
  });

  await t.test('CLI abort leaves all data unchanged, exits nonzero, and still prints summary', async () => {
    const badDb = connection.useDb('migration_abort');
    const collection = badDb.db.collection('characters');
    const before = legacy('bad');
    before.inventory.loadout = [
      { lootId: 'bone', amount: Number.MAX_SAFE_INTEGER }, { lootId: 'bone', amount: 2 }
    ];
    await collection.insertOne(before);
    for (const args of [[], ['--dry-run']]) {
      const result = await cli(server.getUri('migration_abort'), args);
      assert.equal(result.code, 1);
      assert.equal(result.stderr, '');
      const lines = result.stdout.trim().split('\n').map(line => JSON.parse(line));
      assert.equal(lines.length, 2);
      assert.equal(lines[0].status, 'aborted');
      assert.equal(lines[0].reason, 'TOTALS_MISMATCH');
      assert.equal(lines[0].lists.loadout.totalsCheck, false);
      assert.deepEqual(lines[1], { type: 'summary', dryRun: args.length > 0, scanned: 1,
        planned: 0, migrated: 0, skipped: 0, aborted: 1, conflicts: 0, exitCode: 1 });
      assert.deepEqual(await collection.findOne({ _id: 'bad' }), before);
    }
  });

  await t.test('missing revision is guarded by absence and incremented from zero', async () => {
    const collection = db.collection('no_revision');
    const before = legacy('no-revision');
    delete before.revision;
    before.inventory.inventorySchemaVersion = 1;
    await collection.insertOne(before);
    const summary = await migrateInventorySlots(collection, { report: () => {} });
    assert.equal(summary.migrated, 1);
    const expected = expectedMigration({ ...before, revision: 0 });
    assert.deepEqual(await collection.findOne({ _id: before._id }), expected);
  });
});

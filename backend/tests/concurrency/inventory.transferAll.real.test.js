'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { startMemoryMongo } = require('../support/memoryMongo');
const Character = require('../../src/models/Character');
const Inventory = require('../../src/services/InventoryService');

const entry = (slot, lootId, amount) => ({ slot, lootId, amount });

test('real concurrent transfer-all: revision-guarded single winner', async t => {
  const mongo = await startMemoryMongo();
  await mongoose.connect(mongo.getUri());
  t.after(async () => { await mongoose.disconnect(); await mongo.stop(); });
  for (const competitor of ['transferAll', 'moveToLoadout']) await t.test(`transferAll vs ${competitor} wins once and loses nothing`, async () => {
    const accountId = new mongoose.Types.ObjectId();
    await Character.create({ accountId, name: 'ConcurrentBulk', inventory: { inventorySchemaVersion: 2,
      stash: [entry(3, 'healthpotion', 2), entry(9, 'bone', 13)], loadout: [] } });
    const run = competitor === 'transferAll'
      ? () => Inventory.transferAll(accountId, 'toLoadout', 0)
      : () => Inventory.moveToLoadout(accountId, 'bone', 13, 0, 9);
    const results = await Promise.allSettled([Inventory.transferAll(accountId, 'toLoadout', 0), run()]);
    assert.equal(results.filter(r => r.status === 'fulfilled').length, 1);
    const failure = results.find(r => r.status === 'rejected').reason;
    assert.equal(failure.statusCode, 409); assert.equal(failure.errorCode, 'REVISION_CONFLICT');
    const after = await Inventory.getInventory(accountId);
    assert.equal(after.revision, 1);
    const total = [...after.stash, ...after.loadout].reduce((sum, i) => sum + i.amount, 0);
    assert.equal(total, 15);
  });
});

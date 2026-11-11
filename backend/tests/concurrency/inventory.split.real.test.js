'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { startMemoryMongo } = require('../support/memoryMongo');
const Character = require('../../src/models/Character');
const Inventory = require('../../src/services/InventoryService');

const entry = (slot, amount) => ({ slot, lootId: 'bone', amount });

test('real concurrent split: revision-guarded single winner', async t => {
  const flag = process.env.INVENTORY_SPLIT_ENABLED;
  process.env.INVENTORY_SPLIT_ENABLED = 'true';
  const mongo = await startMemoryMongo();
  await mongoose.connect(mongo.getUri());
  t.after(async () => {
    if (flag === undefined) delete process.env.INVENTORY_SPLIT_ENABLED;
    else process.env.INVENTORY_SPLIT_ENABLED = flag;
    await mongoose.disconnect();
    await mongo.stop();
  });
  for (const list of ['stash', 'loadout']) {
    for (const destinations of [[1, 1], [1, 2]]) await t.test(`${list} duplicate split into ${destinations} wins once, preserves total and unrelated entries`, async () => {
      const accountId = new mongoose.Types.ObjectId();
      await Character.create({ accountId, name: 'ConcurrentSplit', inventory: { inventorySchemaVersion: 2,
        [list]: [entry(9, 13), entry(3, 2)], currency: 500 } });
      const before = await Inventory.getInventory(accountId);
      const results = await Promise.allSettled(destinations.map(destination =>
        Inventory.splitSlot(accountId, list, 9, 8, destination, 'bone', 0)));
      const successes = results.filter(result => result.status === 'fulfilled');
      const failures = results.filter(result => result.status === 'rejected');
      assert.equal(successes.length, 1); assert.equal(failures.length, 1);
      assert.equal(failures[0].reason.statusCode, 409);
      assert.equal(failures[0].reason.errorCode, 'REVISION_CONFLICT');
      const winner = successes[0].value;
      const destination = winner[list].find(item => item.amount === 8).slot;
      assert.ok(destinations.includes(destination));
      assert.deepEqual(winner[list], [entry(destination, 8), entry(3, 2), entry(9, 5)]);
      assert.equal(winner.revision, 1);
      const after = await Inventory.getInventory(accountId);
      assert.deepEqual(after, { ...before, [list]: winner[list], revision: 1 });
      assert.equal(after[list].filter(item => item.slot !== 3).reduce((sum, item) => sum + item.amount, 0), 13);
    });
  }
});

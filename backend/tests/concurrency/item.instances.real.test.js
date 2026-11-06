'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { MongoMemoryServer } = require('mongodb-memory-server');
const Character = require('../../src/models/Character');
const Inventory = require('../../src/services/InventoryService');
const { createInstance } = require('../../src/services/ItemInstances');

test('real v3 gem installation races commit once with ownership and receipts intact', async t => {
  const mongo = await MongoMemoryServer.create(); await mongoose.connect(mongo.getUri());
  t.after(async () => { await mongoose.disconnect(); await mongo.stop(); });
  for (const contender of ['install-same-id', 'install-other-id', 'move', 'sell']) await t.test(contender, async () => {
    const accountId = new mongoose.Types.ObjectId();
    const instance = createInstance('arming_sword', { gemSocketCount: 2, gems: [null, null] });
    await Character.create({ accountId, name: 'Race', inventory: { inventorySchemaVersion: 3, currency: 500,
      loadout: [{ slot: 9, lootId: 'arming_sword', amount: 1, instance }, { slot: 3, lootId: 'RES_RUBY_GEM', amount: 2 }] } });
    const before = await Inventory.getInventory(accountId);
    const intent = { transactionId: 'install', weaponList: 'loadout', weaponSlot: 9, instanceId: instance.instanceId,
      gemList: 'loadout', gemSlot: 3, gemLootId: 'RES_RUBY_GEM', expectedRevision: 0 };
    const other = () => contender.startsWith('install')
      ? Inventory.installGem(accountId, { ...intent, transactionId: contender === 'install-same-id' ? 'install' : 'other' })
      : contender === 'move' ? Inventory.moveToStash(accountId, 'arming_sword', 1, 0, 9)
        : Inventory.shopSell(accountId, 'arming_sword', 1, 0, 0, 'sell', 9);
    const outcomes = await Promise.allSettled([Inventory.installGem(accountId, intent), other()]);
    const after = await Inventory.getInventory(accountId);
    assert.equal(after.revision, 1);
    const successes = outcomes.filter(outcome => outcome.status === 'fulfilled');
    const failures = outcomes.filter(outcome => outcome.status === 'rejected');
    // An identical request that reads after the winner is a receipt replay, not a second write.
    if (successes.length === 2) {
      assert.equal(contender, 'install-same-id'); assert.equal(successes.filter(outcome => outcome.value.alreadySecured).length, 1);
    } else {
      assert.equal(successes.length, 1); assert.equal(failures.length, 1);
      assert.equal(failures[0].reason.statusCode, 409); assert.equal(failures[0].reason.errorCode, 'REVISION_CONFLICT');
    }
    const weapon = [...after.stash, ...after.loadout].find(item => item.instance);
    const gems = after.loadout.find(item => item.lootId === 'RES_RUBY_GEM');
    const installed = weapon?.instance.modifiers.gems[0] === 'RES_RUBY_GEM';
    assert.equal(gems.amount, installed ? 1 : 2); assert.equal(after.currency, 500); assert.deepEqual(after.buyback, { entries: [] });
    if (weapon) {
      assert.equal(weapon.instance.instanceId, instance.instanceId); assert.equal(weapon.amount, 1);
      assert.deepEqual(weapon.instance.modifiers.gems, installed ? ['RES_RUBY_GEM', null] : [null, null]);
      assert.equal([...after.loadout, ...after.stash].filter(item => item.instance).length, 1);
    } else assert.equal(contender, 'sell');
    const stored = await Character.findOne({ accountId }).lean();
    assert.equal(stored.appliedShopReceipts.length, installed || !weapon ? 1 : 0);
    if (installed) {
      const transactionId = stored.appliedShopReceipts[0].transactionId;
      const replay = await Inventory.installGem(accountId, { ...intent, transactionId });
      assert.equal(replay.alreadySecured, true); assert.equal(replay.revision, 1);
      assert.deepEqual(await Character.findOne({ accountId }).lean(), stored);
    }
    assert.deepEqual(after.preparedEquipment, before.preparedEquipment); assert.equal(after.pendingReservation, null);
  });
});

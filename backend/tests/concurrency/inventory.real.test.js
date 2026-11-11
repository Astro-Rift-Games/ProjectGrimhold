const test = require('node:test');
const assert = require('node:assert');
const mongoose = require('mongoose');
const { startMemoryMongo } = require('../support/memoryMongo');

const Character = require('../../src/models/Character');
const InventoryService = require('../../src/services/InventoryService');

let mongoServer;

test('InventoryService - Concurrency and Revision', async (t) => {
  t.before(async () => {
    mongoServer = await startMemoryMongo();
    const uri = mongoServer.getUri();
    await mongoose.connect(uri);
  });

  t.after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  t.afterEach(async () => {
    await Character.deleteMany({});
  });

  await t.test('Missing revision field returns REVISION_CONFLICT', async () => {
    // Insert a character directly to bypass schema defaults, missing `revision` field
    const accountId = new mongoose.Types.ObjectId();
    await mongoose.connection.collection('characters').insertOne({
      _id: 'char_no_rev',
      accountId: accountId,
      name: 'NoRevName',
      inventory: { stash: [{ lootId: 'sword', amount: 5 }], loadout: [], preparedEquipment: {} },
      // no revision field
    });

    try {
      // The client thinks expectedRevision is 0 initially or fetches it and gets 0 via default
      await InventoryService.moveToLoadout(accountId.toString(), 'sword', 1, 0);
      assert.fail('Should have thrown REVISION_CONFLICT');
    } catch (err) {
      assert.strictEqual(err.statusCode, 409);
      assert.strictEqual(err.errorCode, 'REVISION_CONFLICT');
    }
  });

  await t.test('Concurrent updates conflict', async () => {
    const accountId = new mongoose.Types.ObjectId();
    const char = new Character({
      accountId: accountId,
      name: 'ConcurrentChar',
      revision: 0,
      inventory: { stash: [{ lootId: 'potion', amount: 10 }], loadout: [], preparedEquipment: {} }
    });
    await char.save();

    const expectedRevision = 0;

    // Ejecución simultánea real
    const results = await Promise.allSettled([
      InventoryService.moveToLoadout(accountId.toString(), 'potion', 1, expectedRevision),
      InventoryService.moveToLoadout(accountId.toString(), 'potion', 1, expectedRevision)
    ]);

    const fulfilled = results.filter(r => r.status === 'fulfilled');
    const rejected = results.filter(r => r.status === 'rejected');

    assert.strictEqual(fulfilled.length, 1, 'Exactly one should succeed');
    assert.strictEqual(rejected.length, 1, 'Exactly one should fail');
    
    assert.strictEqual(fulfilled[0].value.revision, 1);
    assert.strictEqual(rejected[0].reason.statusCode, 409);
    assert.strictEqual(rejected[0].reason.errorCode, 'REVISION_CONFLICT');

    const dbChar = await Character.findOne({ accountId });
    assert.strictEqual(dbChar.revision, 1);
    assert.strictEqual(dbChar.inventory.loadout.length, 1);
    assert.strictEqual(dbChar.inventory.loadout[0].amount, 1);
  });

  await t.test('v2 concurrent slot moves cannot consume the same physical source twice', async () => {
    const accountId = new mongoose.Types.ObjectId();
    await Character.create({ accountId, name: 'Slots', inventory: { inventorySchemaVersion: 2,
      stash: [{ slot: 9, lootId: 'bone', amount: 5 }, { slot: 3, lootId: 'bone', amount: 2 }] } });
    const results = await Promise.allSettled([
      InventoryService.moveSlot(accountId, 'stash', 9, 'loadout', 0, 'bone', 0),
      InventoryService.moveSlot(accountId, 'stash', 9, 'loadout', 1, 'bone', 0)
    ]);
    assert.strictEqual(results.filter(r => r.status === 'fulfilled').length, 1);
    const rejected = results.find(r => r.status === 'rejected');
    assert.strictEqual(rejected.reason.statusCode, 409);
    assert.strictEqual(rejected.reason.errorCode, 'REVISION_CONFLICT');
    const after = await InventoryService.getInventory(accountId);
    assert.strictEqual(after.revision, 1);
    assert.deepStrictEqual(after.stash, [{ slot: 3, lootId: 'bone', amount: 2 }]);
    assert.strictEqual(after.loadout.length, 1); assert.strictEqual(after.loadout[0].amount, 5);
  });

  await t.test('v2 concurrent purchases for the last slot debit and place exactly once', async () => {
    const accountId = new mongoose.Types.ObjectId();
    const loadout = Array.from({ length: 29 }, (_, slot) => ({ slot: slot + 1, lootId: `item_${slot}`, amount: 1 }));
    await Character.create({ accountId, name: 'LastSlot', inventory: { inventorySchemaVersion: 2, loadout, currency: 100 } });
    const results = await Promise.allSettled([
      InventoryService.shopBuy(accountId, 'bone', 1, 0, 0, 'buy-bone'),
      InventoryService.shopBuy(accountId, 'healthpotion', 1, 0, 0, 'buy-potion')
    ]);
    assert.strictEqual(results.filter(r => r.status === 'fulfilled').length, 1);
    assert.strictEqual(results.find(r => r.status === 'rejected').reason.errorCode, 'REVISION_CONFLICT');
    const after = await InventoryService.getInventory(accountId);
    assert.strictEqual(after.revision, 1); assert.strictEqual(after.loadout.length, 30);
    const bought = after.loadout[0];
    assert.strictEqual(bought.slot, 0); assert.strictEqual(bought.amount, 1);
    assert.strictEqual(after.currency, bought.lootId === 'bone' ? 95 : 60);
    assert.deepStrictEqual(after.loadout.slice(1), loadout);
  });

  await t.test('Move concurrent during savePendingReservation does not lose items', async () => {
    const accountId = new mongoose.Types.ObjectId();
    const char = new Character({
      accountId: accountId,
      name: 'ResChar',
      revision: 0,
      inventory: { stash: [{ lootId: 'potion', amount: 10 }], loadout: [{ lootId: 'sword', amount: 1 }], preparedEquipment: { weaponSlot1: { lootId: 'arming_sword', slot: 'weaponSlot1' } } }
    });
    await char.save();

    // The user asked to make sure a concurrent move during savePendingReservation does not lose items,
    // which happens if savePendingReservation recalculates items from the old read instead of throwing 409.
    // Since we removed the retry, savePendingReservation will throw 409 and not lose items.
    
    // We simulate a race condition where findOneAndUpdate fails because revision changed.
    const originalFindOneAndUpdate = Character.findOneAndUpdate;
    let updateCalled = false;
    
    Character.findOneAndUpdate = async function(filter, update, options) {
      if (!updateCalled && update.$set && update.$set['inventory.pendingReservation']) {
        updateCalled = true;
        // Concurrent move happens before the atomic update
        await InventoryService.moveToLoadout(accountId.toString(), 'potion', 1, 0);
      }
      return originalFindOneAndUpdate.call(this, filter, update, options);
    };

    try {
      await assert.rejects(
        () => InventoryService.savePendingReservation(accountId.toString(), 'res_123'),
        err => {
          assert.strictEqual(err.statusCode, 409);
          assert.strictEqual(err.errorCode, 'REVISION_CONFLICT');
          return true;
        }
      );
    } finally {
      Character.findOneAndUpdate = originalFindOneAndUpdate;
    }

    const dbChar = await Character.findOne({ accountId });
    assert.strictEqual(dbChar.revision, 1, 'Only moveToLoadout advanced the revision');
    assert.strictEqual(dbChar.inventory.loadout.length, 2, 'Loadout should have sword and potion');
    assert.strictEqual(dbChar.inventory.pendingReservation, null, 'Reservation failed');
  });
});

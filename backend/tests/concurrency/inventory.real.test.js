const test = require('node:test');
const assert = require('node:assert');
const mongoose = require('mongoose');
const { MongoMemoryServer } = require('mongodb-memory-server');

const Character = require('../../src/models/Character');
const InventoryService = require('../../src/services/InventoryService');

let mongoServer;

test('InventoryService - Concurrency and Revision', async (t) => {
  t.before(async () => {
    mongoServer = await MongoMemoryServer.create();
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

    // First one succeeds
    const res1 = await InventoryService.moveToLoadout(accountId.toString(), 'potion', 1, expectedRevision);
    assert.strictEqual(res1.revision, 1);

    // Second one with same expectedRevision fails
    try {
      await InventoryService.moveToLoadout(accountId.toString(), 'potion', 1, expectedRevision);
      assert.fail('Should have thrown REVISION_CONFLICT');
    } catch (err) {
      assert.strictEqual(err.statusCode, 409);
      assert.strictEqual(err.errorCode, 'REVISION_CONFLICT');
    }
  });
});

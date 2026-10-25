const test = require('node:test');
const assert = require('node:assert/strict');
const Character = require('../src/models/Character');
const InventoryService = require('../src/services/InventoryService');
const { validationResult } = require('express-validator');
const validators = require('../src/validators/inventory.validators');

test('inventory order', async t => {
  let character;
  let writes;
  const originalFind = Character.findOne;
  const originalUpdate = Character.findOneAndUpdate;
  t.after(() => {
    Character.findOne = originalFind;
    Character.findOneAndUpdate = originalUpdate;
  });
  t.beforeEach(() => {
    character = { revision: 3, inventory: {
      stash: [{ lootId: 'apple', amount: 4 }, { lootId: 'berry', amount: 2 }],
      loadout: [{ lootId: 'sword', amount: 1 }, { lootId: 'shield', amount: 3 }],
      preparedEquipment: {}, pendingReservation: null
    } };
    writes = 0;
    Character.findOne = async () => structuredClone(character);
    Character.findOneAndUpdate = async (filter, update) => {
      assert.equal(filter.accountId, 'account');
      assert.equal(filter.revision, 3);
      if (filter.revision !== character.revision) return null;
      writes++;
      assert.equal(Object.keys(update.$set).length, 1);
      for (const [key, value] of Object.entries(update.$set)) {
        character.inventory[key.split('.')[1]] = value;
      }
      character.revision += update.$inc.revision;
      return character;
    };
  });
  for (const list of ['stash', 'loadout']) {
    await t.test(`reorders ${list}, preserving amounts and other list`, async () => {
      const other = list === 'stash' ? 'loadout' : 'stash';
      const before = structuredClone(character.inventory);
      const expected = before[list].slice().reverse();
      const result = await InventoryService.updateOrder('account', list, expected.map(i => i.lootId), 3);
      assert.deepEqual(result[list], expected);
      assert.deepEqual(result[other], before[other]);
      assert.equal(result.revision, 4);
      assert.equal(writes, 1);
    });
  }
  for (const [name, ids] of [
    ['missing id', ['apple']], ['unknown id', ['apple', 'unknown']],
    ['duplicate id', ['apple', 'apple']], ['wrong length', ['apple', 'berry', 'extra']]
  ]) {
    await t.test(`rejects ${name}`, async () => {
      await assert.rejects(() => InventoryService.updateOrder('account', 'stash', ids, 3), { errorCode: 'VALIDATION_FAILED' });
      assert.equal(writes, 0);
    });
  }
  await t.test('rejects stale revision', async () => {
    await assert.rejects(() => InventoryService.updateOrder('account', 'stash', ['berry', 'apple'], 2), { errorCode: 'REVISION_CONFLICT' });
    assert.equal(writes, 0);
  });
  await t.test('rejects pending reservation', async () => {
    character.inventory.pendingReservation = { reservationId: 'raid' };
    await assert.rejects(() => InventoryService.updateOrder('account', 'stash', ['berry', 'apple'], 3), { errorCode: 'RESERVATION_PENDING' });
    assert.equal(writes, 0);
  });
  await t.test('rejects atomic revision race', async () => {
    Character.findOneAndUpdate = async () => null;
    await assert.rejects(() => InventoryService.updateOrder('account', 'stash', ['berry', 'apple'], 3), { errorCode: 'REVISION_CONFLICT' });
  });
  for (const body of [{}, { list: 'other', lootIds: [], expectedRevision: 0 },
    { list: 'stash', lootIds: [''], expectedRevision: 0 },
    { list: 'stash', lootIds: [12], expectedRevision: 0 },
    { list: 'stash', lootIds: 'apple', expectedRevision: 0 },
    { list: 'stash', lootIds: [], expectedRevision: -1 }]) {
    await t.test(`validates bad body ${JSON.stringify(body)}`, async () => {
      assert.ok(validators.inventoryOrderValidator);
      const req = { body };
      for (const chain of validators.inventoryOrderValidator.filter(c => c.run)) await chain.run(req);
      assert.equal(validationResult(req).isEmpty(), false);
    });
  }
});

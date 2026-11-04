'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const Character = require('../src/models/Character');
const Inventory = require('../src/services/InventoryService');
const env = require('../src/config/env');

const entry = (slot, lootId = 'bone', amount = 1) => ({ slot, lootId, amount });

test('split public service contract', async t => {
  let character, reads, writes, race;
  const flag = process.env.INVENTORY_SPLIT_ENABLED;
  const original = { find: Character.findOne, update: Character.findOneAndUpdate };
  t.after(() => {
    Character.findOne = original.find; Character.findOneAndUpdate = original.update;
    if (flag === undefined) delete process.env.INVENTORY_SPLIT_ENABLED;
    else process.env.INVENTORY_SPLIT_ENABLED = flag;
  });
  t.beforeEach(() => {
    process.env.INVENTORY_SPLIT_ENABLED = 'true';
    reads = 0; writes = 0; race = false;
    character = { accountId: 'account', revision: 4, inventory: { inventorySchemaVersion: 2,
      stash: [entry(9, 'bone', 13), entry(3, 'bone', 2)], loadout: [entry(8, 'healthpotion', 3)],
      currency: 500, preparedEquipment: {}, pendingReservation: null,
      appliedExtractionReceipts: [], buyback: { entries: [] } } };
    Character.findOne = async () => { reads++; return structuredClone(character); };
    Character.findOneAndUpdate = async (filter, update) => {
      if (race || filter.revision !== character.revision ||
          (Object.hasOwn(filter, 'inventory.pendingReservation') && character.inventory.pendingReservation)) return null;
      assert.equal(filter.accountId, 'account');
      assert.equal(filter['inventory.pendingReservation'], null);
      assert.deepEqual(update.$inc, { revision: 1 });
      writes++;
      for (const [path, items] of Object.entries(update.$set)) {
        character.inventory[path.split('.')[1]] = structuredClone(items);
      }
      character.revision++;
      return structuredClone(character);
    };
  });
  const split = (patch = {}) => {
    const dto = { list: 'stash', sourceSlot: 9, amount: 8, destinationSlot: 1, lootId: 'bone', expectedRevision: 4, ...patch };
    return Inventory.splitSlot('account', dto.list, dto.sourceSlot, dto.amount, dto.destinationSlot, dto.lootId, dto.expectedRevision);
  };
  async function rejected(patch, statusCode, errorCode) {
    const before = await Inventory.getInventory('account');
    const count = writes;
    await assert.rejects(() => split(patch), { statusCode, errorCode });
    assert.deepEqual(await Inventory.getInventory('account'), before);
    assert.equal(writes, count);
  }

  await t.test('flag defaults off and only true enables; disabled service performs no read or write', async () => {
    for (const value of [undefined, 'false', '1', 'TRUE']) {
      if (value === undefined) delete process.env.INVENTORY_SPLIT_ENABLED;
      else process.env.INVENTORY_SPLIT_ENABLED = value;
      assert.equal(env.inventorySplitEnabled, false);
      await assert.rejects(() => split(), { statusCode: 403, errorCode: 'FEATURE_DISABLED' });
      assert.equal(reads, 0); assert.equal(writes, 0);
    }
  });
  for (const list of ['stash', 'loadout']) {
    await t.test(`${list} INV-AC-03: 13 becomes 5 and 8, total 13, one revision, whole entry copied`, async () => {
      const source = { ...entry(9, 'bone', 13), futureDeclaredField: { quality: 'rare' } };
      character.inventory[list] = [source, entry(3, 'bone', 2)];
      const before = await Inventory.getInventory('account');
      const result = await split({ list });
      assert.deepEqual(result, { stash: list === 'stash' ? [
        { ...source, slot: 1, amount: 8 }, entry(3, 'bone', 2), { ...source, amount: 5 }
      ] : before.stash, loadout: list === 'loadout' ? [
        { ...source, slot: 1, amount: 8 }, entry(3, 'bone', 2), { ...source, amount: 5 }
      ] : before.loadout, revision: 5 });
      assert.equal(result[list].filter(i => [1, 9].includes(i.slot)).reduce((sum, i) => sum + i.amount, 0), 13);
      const after = await Inventory.getInventory('account');
      assert.deepEqual(after, { ...before, [list]: result[list], revision: 5 });
      assert.equal(writes, 1);
    });
    const capacity = list === 'stash' ? 60 : 30;
    for (const amount of [1, 12]) await t.test(`${list} accepts quantity boundary ${amount} at last valid slot`, async () => {
      character.inventory[list] = [entry(9, 'bone', 13)];
      const result = await split({ list, amount, destinationSlot: capacity - 1 });
      assert.deepEqual(result[list], [entry(9, 'bone', 13 - amount), entry(capacity - 1, 'bone', amount)]);
      assert.equal(result.revision, 5); assert.equal(writes, 1);
    });
    await t.test(`${list} overflow may split into a hole only below capacity and occupied limit`, async () => {
      character.inventory[list] = [entry(capacity + 5, 'bone', 13)];
      const result = await split({ list, sourceSlot: capacity + 5 });
      assert.deepEqual(result[list], [entry(1, 'bone', 8), entry(capacity + 5, 'bone', 5)]);
      assert.equal(result.revision, 5); assert.equal(writes, 1);
    });
    for (const occupied of [capacity - 1, capacity, capacity + 1]) await t.test(`${list} counts ${occupied} occupied entries including overflow and duplicate IDs`, async () => {
      character.inventory[list] = Array.from({ length: occupied }, (_, i) => entry(i + 1, 'bone', 13));
      if (occupied >= capacity) await rejected({ list, sourceSlot: occupied, destinationSlot: 0 }, 409, 'INVENTORY_FULL');
      else {
        const result = await split({ list, sourceSlot: occupied, destinationSlot: 0 });
        assert.equal(result[list].length, capacity); assert.equal(result[list][0].amount, 8);
        assert.equal(result.revision, 5); assert.equal(writes, 1);
      }
    });
    await t.test(`${list} rejects destination at or above capacity even with a hole`, async () => {
      character.inventory[list] = [entry(9, 'bone', 13)];
      for (const destinationSlot of [capacity, capacity + 5]) await rejected({ list, destinationSlot }, 409, 'INVENTORY_FULL');
    });
  }
  for (const amount of [0, -1, 13, 14, 1.5, '8', null, undefined, Number.MAX_SAFE_INTEGER + 1]) {
    await t.test(`invalid amount ${String(amount)} rejects atomically`, () => rejected({ amount }, 400, 'INVALID_QUANTITY'));
  }
  for (const field of ['sourceSlot', 'destinationSlot']) for (const value of [-1, 0.5, '1', null, undefined]) {
    await t.test(`invalid ${field} ${String(value)} rejects atomically`, () => rejected({ [field]: value }, 400, 'VALIDATION_FAILED'));
  }
  await t.test('same slot and invalid list reject atomically', async () => {
    await rejected({ destinationSlot: 9 }, 400, 'VALIDATION_FAILED');
    await rejected({ list: 'other' }, 400, 'VALIDATION_FAILED');
  });
  await t.test('missing or mismatched source rejects, never using another matching stack', async () => {
    await rejected({ sourceSlot: 7 }, 409, 'INSUFFICIENT_STASH_ITEMS');
    await rejected({ lootId: 'healthpotion' }, 409, 'INSUFFICIENT_STASH_ITEMS');
    await rejected({ list: 'loadout', sourceSlot: 7 }, 409, 'INSUFFICIENT_LOADOUT_ITEMS');
    await rejected({ list: 'loadout', sourceSlot: 8, lootId: 'bone' }, 409, 'INSUFFICIENT_LOADOUT_ITEMS');
  });
  for (const lootId of ['bone', 'healthpotion']) await t.test(`occupied ${lootId} destination rejects instead of merging/swapping`, async () => {
    character.inventory.stash.push(entry(1, lootId, 4));
    await rejected({}, 409, 'SLOT_OCCUPIED');
  });
  await t.test('legacy and absent schema reject without mutation', async () => {
    for (const version of [1, undefined]) {
      character.inventory.inventorySchemaVersion = version;
      await rejected({}, 409, 'INVENTORY_MIGRATION_REQUIRED');
    }
  });
  await t.test('reservation and stale revision reject without mutation', async () => {
    await rejected({ expectedRevision: 3 }, 409, 'REVISION_CONFLICT');
    character.inventory.pendingReservation = { reservationId: 'pending', items: [] };
    await rejected({}, 409, 'RESERVATION_PENDING');
  });
  await t.test('lost atomic write leaves lists and revision untouched', async () => {
    race = true;
    await rejected({}, 409, 'REVISION_CONFLICT');
  });
  await t.test('missing character returns 404 with no write', async () => {
    Character.findOne = async () => null;
    await assert.rejects(() => split(), { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND' });
    assert.equal(writes, 0);
  });
  await t.test('PRESERVE: slot move still moves the whole entry with split disabled', async () => {
    process.env.INVENTORY_SPLIT_ENABLED = 'false';
    const moved = await Inventory.moveSlot('account', 'stash', 9, 'loadout', 1, 'bone', 4);
    assert.deepEqual(moved, { stash: [entry(3, 'bone', 2)], loadout: [entry(1, 'bone', 13), entry(8, 'healthpotion', 3)], revision: 5 });
    assert.equal(writes, 1);
  });
});

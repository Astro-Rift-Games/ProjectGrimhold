'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const Character = require('../src/models/Character');
const Inventory = require('../src/services/InventoryService');
const { EQUIPMENT_SLOTS } = require('../src/config/equipmentSlots');

const entry = (slot, lootId = 'bone', amount = 1) => ({ slot, lootId, amount });
const id = n => n.toString(16).padStart(24, '0');
const unit = (slot, n) => ({ slot, amount: 1, lootId: 'arming_sword', instance: { instanceId: id(n), modifiers: { gemSocketCount: 0, gems: [] } } });
const emptyEquipment = () => Object.fromEntries(EQUIPMENT_SLOTS.map(slot => [slot, null]));
const filler = (count, from = 0) => Array.from({ length: count }, (_, i) => entry(from + i, `filler_${from + i}`, 1));

test('transfer-all public service contract', async t => {
  let character, writes, reads, race, lastFilter;
  const original = { find: Character.findOne, update: Character.findOneAndUpdate };
  t.after(() => { Character.findOne = original.find; Character.findOneAndUpdate = original.update; });
  t.beforeEach(() => {
    writes = 0; reads = 0; race = false;
    character = { accountId: 'account', revision: 4, inventory: { inventorySchemaVersion: 2,
      stash: [entry(9, 'bone', 13), entry(3, 'healthpotion', 2), entry(5, 'bone', 1)], loadout: [entry(8, 'bone', 3)],
      currency: 500, preparedEquipment: {}, pendingReservation: null, appliedExtractionReceipts: [], buyback: { entries: [] } } };
    Character.findOne = async () => { reads++; return structuredClone(character); };
    Character.findOneAndUpdate = async (filter, update) => {
      if (race || filter.revision !== character.revision ||
          (Object.hasOwn(filter, 'inventory.pendingReservation') && character.inventory.pendingReservation)) return null;
      assert.deepEqual(update.$inc, { revision: 1 });
      writes++; lastFilter = filter;
      for (const [path, items] of Object.entries(update.$set)) character.inventory[path.split('.')[1]] = structuredClone(items);
      character.revision++;
      return structuredClone(character);
    };
  });
  const unchanged = async (operation, statusCode, errorCode) => {
    const before = structuredClone(character);
    await assert.rejects(operation, { statusCode, errorCode });
    assert.deepEqual(character, before);
    assert.equal(writes, 0);
  };

  await t.test('S3 moves every stack in source slot order with one write and one revision (stash -> loadout)', async () => {
    const result = await Inventory.transferAll('account', 'toLoadout', 4);
    assert.equal(writes, 1);
    assert.equal(lastFilter['inventory.pendingReservation'], null);
    assert.deepEqual(result.stash, []);
    assert.deepEqual(result.loadout, [entry(0, 'healthpotion', 2), entry(8, 'bone', 17)]);
    assert.equal(result.revision, 5);
    assert.deepEqual(result.results, [
      { slot: 3, lootId: 'healthpotion', amount: 2, moved: true },
      { slot: 5, lootId: 'bone', amount: 1, moved: true },
      { slot: 9, lootId: 'bone', amount: 13, moved: true }]);
    assert.deepEqual(Object.keys(result).sort(), ['loadout', 'results', 'revision', 'stash']);
  });

  await t.test('loadout -> stash uses the same rules', async () => {
    const result = await Inventory.transferAll('account', 'toStash', 4);
    assert.equal(writes, 1);
    assert.deepEqual(result.loadout, []);
    assert.deepEqual(result.stash, [entry(3, 'healthpotion', 2), entry(5, 'bone', 4), entry(9, 'bone', 13)]);
    assert.deepEqual(result.results, [{ slot: 8, lootId: 'bone', amount: 3, moved: true }]);
  });

  await t.test('S3 refused stack is skipped and later stacks are still evaluated', async () => {
    character.inventory.loadout = filler(29);
    character.inventory.stash = [entry(1, 'bone', 1), entry(2, 'axe', 1), entry(3, 'filler_0', 5)];
    const result = await Inventory.transferAll('account', 'toLoadout', 4);
    assert.equal(writes, 1);
    assert.deepEqual(result.results, [
      { slot: 1, lootId: 'bone', amount: 1, moved: true },
      { slot: 2, lootId: 'axe', amount: 1, moved: false, errorCode: 'INVENTORY_FULL' },
      { slot: 3, lootId: 'filler_0', amount: 5, moved: true }]);
    assert.deepEqual(result.stash, [entry(2, 'axe', 1)]);
    assert.equal(result.loadout.length, 30);
    assert.equal(result.loadout.find(i => i.lootId === 'filler_0').amount, 6);
  });

  await t.test('S4 nothing moves: no write, unchanged revision, all refused', async () => {
    character.inventory.loadout = filler(30);
    character.inventory.stash = [entry(1, 'bone', 1)];
    const result = await Inventory.transferAll('account', 'toLoadout', 4);
    assert.equal(writes, 0);
    assert.equal(result.revision, 4);
    assert.deepEqual(result.results, [{ slot: 1, lootId: 'bone', amount: 1, moved: false, errorCode: 'INVENTORY_FULL' }]);
    assert.deepEqual(result.stash, [entry(1, 'bone', 1)]);
  });

  await t.test('empty source: no write, empty results', async () => {
    character.inventory.stash = [];
    const result = await Inventory.transferAll('account', 'toLoadout', 4);
    assert.equal(writes, 0); assert.deepEqual(result.results, []); assert.equal(result.revision, 4);
  });

  await t.test('v3 instances behave as single transfers: unique, never merged, refused when full', async () => {
    Object.assign(character.inventory, { inventorySchemaVersion: 3, preparedEquipment: emptyEquipment(),
      stash: [unit(2, 1), unit(4, 2)], loadout: [unit(0, 3)] });
    const result = await Inventory.transferAll('account', 'toLoadout', 4);
    assert.equal(writes, 1);
    assert.deepEqual(result.stash, []);
    assert.deepEqual(result.loadout, [unit(0, 3), unit(1, 1), unit(2, 2)]);
    assert.ok(result.results.every(r => r.moved && r.amount === 1 && r.lootId === 'arming_sword'));
  });

  await t.test('S4 revision conflict rejects and writes nothing', async () => {
    await unchanged(() => Inventory.transferAll('account', 'toLoadout', 3), 409, 'REVISION_CONFLICT');
  });

  await t.test('S4 write race loses with REVISION_CONFLICT and leaves data unchanged', async () => {
    race = true;
    await unchanged(() => Inventory.transferAll('account', 'toLoadout', 4), 409, 'REVISION_CONFLICT');
  });

  await t.test('S4 pending reservation rejects', async () => {
    character.inventory.pendingReservation = { reservationId: 'r', items: [] };
    await unchanged(() => Inventory.transferAll('account', 'toLoadout', 4), 409, 'RESERVATION_PENDING');
  });

  await t.test('S4 missing character rejects with 404', async () => {
    Character.findOne = async () => null;
    await assert.rejects(() => Inventory.transferAll('account', 'toLoadout', 4), { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND' });
    assert.equal(writes, 0);
  });

  await t.test('legacy v1 inventory requires migration', async () => {
    character.inventory.inventorySchemaVersion = 1;
    await unchanged(() => Inventory.transferAll('account', 'toLoadout', 4), 409, 'INVENTORY_MIGRATION_REQUIRED');
  });

  await t.test('invalid direction is rejected', async () => {
    await unchanged(() => Inventory.transferAll('account', 'sideways', 4), 400, 'VALIDATION_FAILED');
  });

  await t.test('PRESERVE single transfer is unchanged', async () => {
    const result = await Inventory.moveToLoadout('account', 'bone', 1, 4, 5);
    assert.deepEqual(result.stash, [entry(3, 'healthpotion', 2), entry(9, 'bone', 13)]);
    assert.equal(writes, 1);
  });
});

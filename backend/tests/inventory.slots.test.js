'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const Character = require('../src/models/Character');
const Result = require('../src/models/AuthoritativeExtractionResult');
const Inventory = require('../src/services/InventoryService');
const Extraction = require('../src/services/ExtractionCommitService');
const Session = require('../src/services/SessionTicketService');

const entry = (slot, lootId = 'bone', amount = 1) => ({ slot, lootId, amount });
const full = (capacity) => Array.from({ length: capacity }, (_, slot) => entry(slot, `item_${slot}`));

test('physical inventory public service contract', async t => {
  let character, writes, claims, result;
  const original = { find: Character.findOne, update: Character.findOneAndUpdate,
    result: Result.findOne, claim: Result.findOneAndUpdate, exists: Result.exists };
  t.after(() => {
    Character.findOne = original.find; Character.findOneAndUpdate = original.update;
    Result.findOne = original.result; Result.findOneAndUpdate = original.claim; Result.exists = original.exists;
  });
  t.beforeEach(() => {
    writes = 0; claims = 0;
    character = { _id: 'char', accountId: 'account', name: 'Hero', revision: 4, level: 1, experience: 0,
      characterAttributes: { availablePoints: 0 }, appliedShopReceipts: [], appliedProgressionReceipts: [],
      inventory: { inventorySchemaVersion: 2, currency: 50000, stash: [], loadout: [],
        preparedEquipment: { weaponSlot1: 'arming_sword' }, pendingReservation: null,
        buyback: { sessionEpoch: 'town', entries: [] }, appliedExtractionReceipts: [] } };
    result = { _id: 'result', raidId: 'raid', accountId: 'account', items: [], experienceGranted: 0, consumed: false };
    Character.findOne = async () => structuredClone(character);
    Character.findOneAndUpdate = async (filter, update) => {
      if (filter.revision !== character.revision) return null;
      writes++;
      const assign = (path, value) => {
        const keys = path.split('.'); let target = character;
        for (const key of keys.slice(0, -1)) target = target[key];
        target[keys.at(-1)] = structuredClone(value);
      };
      for (const [path, value] of Object.entries(update.$set || {})) assign(path, value);
      for (const path of Object.keys(update.$unset || {})) assign(path, null);
      for (const [path, value] of Object.entries(update.$inc || {})) {
        const current = path.split('.').reduce((obj, key) => obj[key], character);
        assign(path, current + value);
      }
      for (const [path, value] of Object.entries(update.$push || {})) {
        const current = path.split('.').reduce((obj, key) => obj[key], character);
        const next = current.concat(value.$each || [value]);
        assign(path, value.$slice ? next.slice(value.$slice) : next);
      }
      return structuredClone(character);
    };
    Result.findOne = async () => structuredClone(result);
    Result.exists = async () => false;
    Result.findOneAndUpdate = async () => { claims++; result.consumed = true; return structuredClone(result); };
  });
  const snapshot = () => Inventory.getInventory('account');
  async function rejectsUnchanged(operation, code, statusCode = 409) {
    const before = await snapshot(); const receipts = structuredClone(character.appliedShopReceipts);
    const counters = [writes, claims];
    await assert.rejects(operation, { statusCode, errorCode: code });
    assert.deepEqual(await snapshot(), before);
    assert.deepEqual(character.appliedShopReceipts, receipts);
    assert.deepEqual([writes, claims], counters);
  }

  for (const direction of ['moveToLoadout', 'moveToStash']) {
    const from = direction === 'moveToLoadout' ? 'stash' : 'loadout';
    const to = from === 'stash' ? 'loadout' : 'stash';
    const capacity = to === 'loadout' ? 30 : 60;
    await t.test(`${direction} addresses exactly one stack, receives lowest compatible, preserves holes`, async () => {
      character.inventory[from] = [entry(9, 'bone', 4), entry(3, 'bone', 8), entry(7, 'healthpotion', 2)];
      character.inventory[to] = [entry(8, 'bone', 2), entry(2, 'bone', 1)];
      const moved = await Inventory[direction]('account', 'bone', 4, 4, 9);
      assert.deepEqual(moved[from], [entry(3, 'bone', 8), entry(7, 'healthpotion', 2)]);
      assert.deepEqual(moved[to], [entry(2, 'bone', 5), entry(8, 'bone', 2)]);
      assert.equal(moved.revision, 5); assert.equal(writes, 1);
    });
    await t.test(`${direction} uses lowest hole, rejects full destination without source loss`, async () => {
      character.inventory[from] = [entry(9, 'bone', 4)];
      character.inventory[to] = [entry(2, 'healthpotion', 1)];
      const moved = await Inventory[direction]('account', 'bone', 1, 4, 9);
      assert.deepEqual(moved[from], [entry(9, 'bone', 3)]);
      assert.deepEqual(moved[to], [entry(0, 'bone'), entry(2, 'healthpotion')]);
      character.inventory[to] = full(capacity);
      await rejectsUnchanged(() => Inventory[direction]('account', 'bone', 1, 5, 9), 'INVENTORY_FULL');
    });
    await t.test(`${direction} requires v2 source slot and guards lootId`, async () => {
      character.inventory[from] = [entry(9, 'bone', 4)];
      await rejectsUnchanged(() => Inventory[direction]('account', 'bone', 1, 4), 'INVENTORY_SLOT_ADDRESS_REQUIRED');
      await rejectsUnchanged(() => Inventory[direction]('account', 'healthpotion', 1, 4, 9), from === 'stash' ? 'INSUFFICIENT_STASH_ITEMS' : 'INSUFFICIENT_LOADOUT_ITEMS');
      character.inventory.inventorySchemaVersion = 1;
      await rejectsUnchanged(() => Inventory[direction]('account', 'bone', 1, 4, 9), 'INVENTORY_MIGRATION_REQUIRED');
      const moved = await Inventory[direction]('account', 'bone', 1, 4);
      assert.equal(moved.revision, 5); // legacy lootId behavior still works
    });
  }

  await t.test('overflow is reducing-only: receive skips overflow matches and counts occupied slots', async () => {
    character.inventory.loadout = [entry(30, 'healthpotion', 5)];
    const bought = await Inventory.shopBuy('account', 'healthpotion', 1, 0, 4, 'buy');
    assert.deepEqual(bought.loadout, [entry(0, 'healthpotion'), entry(30, 'healthpotion', 5)]);
    character.inventory.loadout = full(30).map(i => ({ ...i, slot: i.slot + 1 }));
    await rejectsUnchanged(() => Inventory.shopBuy('account', 'healthpotion', 1, 0, 5, 'full'), 'INVENTORY_FULL');
    character.inventory.loadout[0] = entry(1, 'healthpotion', 2);
    const merged = await Inventory.shopBuy('account', 'healthpotion', 2, 0, 5, 'merge');
    assert.deepEqual(merged.loadout[0], entry(1, 'healthpotion', 4));
  });

  await t.test('v2 order is a slot permutation, compacts only explicitly and never merges', async () => {
    character.inventory.loadout = [entry(9, 'bone', 2), entry(3, 'bone', 4), entry(7, 'healthpotion')];
    const ordered = await Inventory.updateOrder('account', 'loadout', undefined, 4, [7, 9, 3]);
    assert.deepEqual(ordered.loadout, [entry(0, 'healthpotion'), entry(1, 'bone', 2), entry(2, 'bone', 4)]);
    assert.equal(ordered.revision, 5); assert.equal(writes, 1);
    for (const order of [[0, 1], [0, 1, 1], [0, 1, 8], [0, 1, 2.5]]) {
      await rejectsUnchanged(() => Inventory.updateOrder('account', 'loadout', undefined, 5, order), 'VALIDATION_FAILED', 400);
    }
    await rejectsUnchanged(() => Inventory.updateOrder('account', 'loadout', ['bone'], 5), 'INVENTORY_SLOT_ADDRESS_REQUIRED');
  });

  for (const [name, target, expected] of [
    ['empty', [], [entry(1, 'bone', 3)]],
    ['merge', [entry(1, 'bone', 4)], [entry(1, 'bone', 7)]],
    ['swap', [entry(1, 'healthpotion', 4)], [entry(1, 'bone', 3)]]
  ]) for (const toList of ['stash', 'loadout']) {
    await t.test(`slot move ${name} ${toList} is atomic and carries whole entries`, async () => {
      character.inventory.stash = [entry(8, 'bone', 3), ...(toList === 'stash' ? target : [])];
      if (toList === 'loadout') character.inventory.loadout = target;
      const moved = await Inventory.moveSlot('account', 'stash', 8, toList, 1, 'bone', 4);
      assert.deepEqual(moved[toList], name === 'swap' && toList === 'stash' ? [...expected, entry(8, 'healthpotion', 4)] : expected);
      if (toList === 'loadout') assert.deepEqual(moved.stash, name === 'swap' ? [entry(8, 'healthpotion', 4)] : []);
      assert.equal(moved.revision, 5); assert.equal(writes, 1);
    });
  }
  await t.test('slot move refuses overflow placement including swap-back, bad guard, stale revision and v1', async () => {
    character.inventory.stash = [entry(65, 'bone', 2)];
    character.inventory.loadout = [entry(0, 'healthpotion')];
    await rejectsUnchanged(() => Inventory.moveSlot('account', 'stash', 65, 'loadout', 30, 'bone', 4), 'INVENTORY_FULL');
    await rejectsUnchanged(() => Inventory.moveSlot('account', 'stash', 65, 'loadout', 0, 'bone', 4), 'INVENTORY_FULL');
    await rejectsUnchanged(() => Inventory.moveSlot('account', 'stash', 65, 'loadout', 1, 'other', 4), 'ITEM_NOT_FOUND', 422);
    await rejectsUnchanged(() => Inventory.moveSlot('account', 'stash', 65, 'loadout', 1, 'bone', 3), 'REVISION_CONFLICT');
    const reduced = await Inventory.moveSlot('account', 'stash', 65, 'loadout', 1, 'bone', 4);
    assert.deepEqual(reduced.stash, []); assert.deepEqual(reduced.loadout, [entry(0, 'healthpotion'), entry(1, 'bone', 2)]);
    character.inventory.inventorySchemaVersion = 1;
    await rejectsUnchanged(() => Inventory.moveSlot('account', 'loadout', 1, 'stash', 0, 'bone', 5), 'INVENTORY_MIGRATION_REQUIRED');
  });
  await t.test('slot move respects occupied capacity on both lists', async () => {
    character.inventory.stash = full(60).map(i => ({ ...i, slot: i.slot + 1 }));
    character.inventory.loadout = [entry(0, 'bone')];
    await rejectsUnchanged(() => Inventory.moveSlot('account', 'loadout', 0, 'stash', 0, 'bone', 4), 'INVENTORY_FULL');
    character.inventory.stash = [entry(0, 'bone')];
    character.inventory.loadout = full(30).map(i => ({ ...i, slot: i.slot + 1 }));
    await rejectsUnchanged(() => Inventory.moveSlot('account', 'stash', 0, 'loadout', 0, 'bone', 4), 'INVENTORY_FULL');
  });

  await t.test('sell requires sourceSlot and binds replay to it; removes only addressed stack', async () => {
    character.inventory.loadout = [entry(8, 'bone', 2), entry(3, 'bone', 4)];
    await rejectsUnchanged(() => Inventory.shopSell('account', 'bone', 2, 0, 4, 'sell'), 'INVENTORY_SLOT_ADDRESS_REQUIRED');
    const sold = await Inventory.shopSell('account', 'bone', 2, 0, 4, 'sell', 8);
    assert.deepEqual(sold.loadout, [entry(3, 'bone', 4)]); assert.deepEqual(sold.buyback, { entries: [] });
    assert.equal(sold.revision, 5); assert.equal(sold.currency, 50010);
    const replay = await Inventory.shopSell('account', 'bone', 2, 0, 4, 'sell', 8);
    assert.equal(replay.alreadySecured, true); assert.equal(writes, 1);
    await rejectsUnchanged(() => Inventory.shopSell('account', 'bone', 2, 0, 5, 'sell', 3), 'TRANSACTION_ID_REUSED');
    character.inventory.inventorySchemaVersion = 1;
    await rejectsUnchanged(() => Inventory.shopSell('account', 'bone', 1, 0, 5, 'legacy', 3), 'INVENTORY_MIGRATION_REQUIRED');
  });

  for (const version of [1, 2]) await t.test(`v${version} reservation restrictions and legacy move compatibility`, async () => {
    character.inventory.inventorySchemaVersion = version;
    character.inventory.stash = [entry(9, 'bone', 2)];
    character.inventory.pendingReservation = { reservationId: 'pending', items: [] };
    await rejectsUnchanged(() => Inventory.updateOrder('account', 'stash', ['bone'], 4, version === 2 ? [9] : undefined), 'RESERVATION_PENDING');
    await rejectsUnchanged(() => Inventory.shopBuy('account', 'healthpotion', 1, 0, 4, 'buy'), 'RESERVATION_PENDING');
    await rejectsUnchanged(() => Inventory.shopSell('account', 'bone', 1, 0, 4, 'sell', version === 2 ? 0 : undefined), 'RESERVATION_PENDING');
    await rejectsUnchanged(() => Inventory.shopBuyback('account', 'line', 1, 4, 'rebuy'), 'RESERVATION_PENDING');
    if (version === 2) await rejectsUnchanged(() => Inventory.moveSlot('account', 'stash', 9, 'loadout', 0, 'bone', 4), 'RESERVATION_PENDING');
    const moved = await Inventory.moveToLoadout('account', 'bone', 1, 4, version === 2 ? 9 : undefined);
    assert.equal(moved.revision, 5);
  });

  await t.test('reservation stores exact slots; ticket aggregates; rollback never merges occupied collisions', async () => {
    character.inventory.loadout = [entry(8, 'bone', 2), entry(3, 'bone', 4)];
    const reserved = await Inventory.savePendingReservation('account', 'res');
    assert.deepEqual(reserved.pendingReservation.items, [entry(3, 'bone', 4), entry(8, 'bone', 2)]);
    assert.deepEqual((await snapshot()).loadout, []);
    assert.deepEqual((await Session.issueRaidAdmissionTicket('account', 'ABC123', 'res')).snapshot.items, [{ lootId: 'bone', amount: 6 }]);
    character.inventory.loadout = [entry(3, 'bone', 10)];
    await Inventory.clearPendingReservation('account', { rollback: true });
    const restored = await snapshot();
    assert.deepEqual(restored.loadout, [entry(0, 'bone', 4), entry(3, 'bone', 10), entry(8, 'bone', 2)]);
    assert.equal(restored.pendingReservation, null); assert.equal(restored.revision, 6);
  });
  await t.test('rollback uses overflow if full, preserves free original positions, never drops amounts', async () => {
    character.inventory.loadout = full(30);
    character.inventory.pendingReservation = { reservationId: 'res', items: [entry(3, 'bone', 4), entry(50, 'bone', 2)], preparedEquipment: {} };
    await Inventory.clearPendingReservation('account', { rollback: true });
    const restored = await snapshot();
    assert.deepEqual(restored.loadout, [...full(30), entry(30, 'bone', 4), entry(50, 'bone', 2)]);
    assert.equal(restored.loadout.filter(i => i.lootId === 'bone').reduce((sum, i) => sum + i.amount, 0), 6);
  });
  await t.test('reservation rejects over-capacity Loadout without mutation', async () => {
    character.inventory.loadout = full(31);
    await rejectsUnchanged(() => Inventory.savePendingReservation('account', 'res'), 'INVENTORY_FULL');
  });

  await t.test('prepared equipment returns use receive rule and final capacity; equip leaves a hole', async () => {
    character.inventory.loadout = [entry(9, 'arming_sword', 2), entry(3, 'arming_sword', 3), entry(7, 'healthpotion')];
    const unequipped = await Inventory.updatePreparedEquipment('account', {}, 4);
    assert.deepEqual(unequipped.loadout, [entry(3, 'arming_sword', 4), entry(7, 'healthpotion'), entry(9, 'arming_sword', 2)]);
    character.inventory.loadout = full(30); character.inventory.loadout[5] = entry(5, 'placeholder_helmet');
    character.inventory.preparedEquipment = { weaponSlot1: 'arming_sword' };
    const exchanged = await Inventory.updatePreparedEquipment('account', { helmet: 'placeholder_helmet' }, 5);
    assert.deepEqual(exchanged.loadout.find(i => i.slot === 5), entry(5, 'arming_sword'));
    character.inventory.preparedEquipment = { weaponSlot1: 'arming_sword' };
    character.inventory.loadout = full(30);
    await rejectsUnchanged(() => Inventory.updatePreparedEquipment('account', {}, 6), 'INVENTORY_FULL');
  });

  for (const version of [1, 2]) await t.test(`v${version} equipment can change assignment without an extra inventory unit`, async () => {
    character.inventory.inventorySchemaVersion = version;
    character.inventory.loadout = full(30);
    const before = (await snapshot()).loadout;
    const moved = await Inventory.updatePreparedEquipment('account', { weaponSlot2: 'arming_sword' }, 4);
    assert.equal(moved.preparedEquipment.weaponSlot1, '');
    assert.equal(moved.preparedEquipment.weaponSlot2, 'arming_sword');
    assert.deepEqual(moved.loadout, before); assert.equal(moved.revision, 5);
  });
  await t.test('slot no-op and boundary placements do not renumber unrelated entries', async () => {
    character.inventory.stash = [entry(8, 'bone', 3), entry(5, 'healthpotion')];
    const before = await snapshot();
    const unchanged = await Inventory.moveSlot('account', 'stash', 8, 'stash', 8, 'bone', 4);
    assert.equal(unchanged.revision, 4); assert.equal(writes, 0); assert.deepEqual(await snapshot(), before);
    const boundary = await Inventory.moveSlot('account', 'stash', 8, 'loadout', 29, 'bone', 4);
    assert.deepEqual(boundary.loadout, [entry(29, 'bone', 3)]);
    assert.deepEqual(boundary.stash, [entry(5, 'healthpotion')]);
    const stashBoundary = await Inventory.moveSlot('account', 'loadout', 29, 'stash', 59, 'bone', 5);
    assert.deepEqual(stashBoundary.stash, [entry(5, 'healthpotion'), entry(59, 'bone', 3)]);
  });
  await t.test('v2 merge overflow rejects atomically', async () => {
    character.inventory.stash = [entry(0, 'bone')];
    character.inventory.loadout = [entry(0, 'bone', Number.MAX_SAFE_INTEGER)];
    await rejectsUnchanged(() => Inventory.moveToLoadout('account', 'bone', 1, 4, 0), 'INTEGER_OVERFLOW', 400);
    await rejectsUnchanged(() => Inventory.moveSlot('account', 'stash', 0, 'loadout', 0, 'bone', 4), 'INTEGER_OVERFLOW', 400);
  });
  await t.test('rollback reserves free original positions before collision fallback', async () => {
    character.inventory.loadout = [entry(0, 'healthpotion')];
    character.inventory.pendingReservation = { reservationId: 'res', items: [entry(0, 'bone', 2), entry(1, 'bone', 4)], preparedEquipment: {} };
    await Inventory.clearPendingReservation('account', { rollback: true });
    assert.deepEqual((await snapshot()).loadout, [entry(0, 'healthpotion'), entry(1, 'bone', 4), entry(2, 'bone', 2)]);
  });
  await t.test('v2 reservation replay is read-only and admitted rollback cannot restore ownership', async () => {
    character.inventory.loadout = [entry(9, 'bone', 2)];
    const reserved = await Inventory.savePendingReservation('account', 'res');
    const replay = await Inventory.savePendingReservation('account', 'res');
    assert.deepEqual(replay, reserved); assert.equal(writes, 1);
    character.inventory.pendingReservation.admittedAt = new Date();
    await rejectsUnchanged(() => Inventory.clearPendingReservation('account', { rollback: true }), 'RESERVATION_ADMITTED');
  });

  await t.test('authoritative extraction folds aggregate payload and places sequential slots without touching Stash', async () => {
    character.inventory.stash = [entry(10, 'bone', 3), entry(2, 'bone')];
    result.items = [{ lootId: 'bone', amount: 2 }, { lootId: 'healthpotion', amount: 1 }, { lootId: 'bone', amount: 4 }];
    const committed = await Extraction.commit('account', { raidId: 'raid', resultSequence: 1 });
    assert.deepEqual(committed.loadout, [entry(0, 'bone', 6), entry(1, 'healthpotion')]);
    assert.deepEqual((await snapshot()).stash, [entry(2, 'bone'), entry(10, 'bone', 3)]);
    assert.equal(committed.revision, 5); assert.equal(claims, 1); assert.equal(writes, 1);
    const replay = await Extraction.commit('account', { raidId: 'raid', resultSequence: 1 });
    assert.equal(replay.alreadySecured, true); assert.equal(claims, 1); assert.equal(writes, 1);
  });
  await t.test('authoritative extraction rejects >30 distinct items before claiming result, never truncates', async () => {
    result.items = full(31).map(({ lootId, amount }) => ({ lootId, amount }));
    await rejectsUnchanged(() => Extraction.commit('account', { raidId: 'raid', resultSequence: 1 }), 'INVENTORY_FULL');
    assert.equal(result.consumed, false);
  });
  await t.test('authoritative extraction accepts exactly 30 slots', async () => {
    result.items = full(30).map(({ lootId, amount }) => ({ lootId, amount }));
    const committed = await Extraction.commit('account', { raidId: 'raid', resultSequence: 1 });
    assert.deepEqual(committed.loadout, full(30));
  });
});

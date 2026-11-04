'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { planInventorySlots } = require('../scripts/migrate_inventory_slots');

function character(inventory = {}) {
  return { _id: 'character-1', revision: 7, inventory: { stash: [], loadout: [], ...inventory } };
}

function freeze(value) {
  if (value && typeof value === 'object') {
    Object.freeze(value);
    for (const child of Object.values(value)) freeze(child);
  }
  return value;
}

test('planner folds canonical aliases into the first entry in stored order without mutating input', () => {
  const first = { lootId: 'training_sword', amount: 2, slot: 99, detail: { quality: 4 } };
  const input = freeze(character({ stash: [first,
    { lootId: 'bone', amount: 3, note: 'preserved' },
    { lootId: 'arming_sword', amount: 5 },
    { lootId: 'recovery_sword', amount: 1 }
  ] }));
  const plan = planInventorySlots(input);
  assert.equal(plan.status, 'planned');
  assert.deepEqual(plan.set['inventory.stash'], [
    { ...first, lootId: 'arming_sword', amount: 8, slot: 0 },
    { lootId: 'bone', amount: 3, note: 'preserved', slot: 1 }
  ]);
  assert.equal(plan.set['inventory.inventorySchemaVersion'], 2);
  assert.deepEqual(plan.lists.stash.duplicates, [{ lootId: 'arming_sword', entries: 3 }]);
  assert.equal(plan.lists.stash.duplicatesFolded, 2);
  assert.equal(plan.lists.stash.entriesBefore, 4);
  assert.equal(plan.lists.stash.entriesAfter, 2);
  assert.deepEqual(plan.lists.stash.totalsBefore, { arming_sword: '8', bone: '3' });
  assert.deepEqual(plan.lists.stash.totalsAfter, plan.lists.stash.totalsBefore);
  assert.equal(plan.lists.stash.totalsCheck, true);
  assert.equal(input.revision, 7);
  assert.equal(first.slot, 99);
});

test('planner uses exactly the legacy normalizer alias rules (no trimming or case folding)', () => {
  const aliases = [
    ['greatsword', 'long_sword'], ['wand', 'magic_wand'], ['staff', 'magic_staff'],
    ['spellbook', 'magic_wand'], ['training_shield', 'shield'],
    ['heavy_armor_legplate', 'heavy_armor_leg_plate'], ['longsword', 'arming_sword']
  ];
  for (const [alias, canonical] of aliases) {
    const plan = planInventorySlots(character({ loadout: [
      { lootId: alias, amount: 1 }, { lootId: canonical, amount: 2 }
    ] }));
    assert.deepEqual(plan.set['inventory.loadout'], [{ lootId: canonical, amount: 3, slot: 0 }]);
  }
  const plan = planInventorySlots(character({ stash: [
    { lootId: ' bone ', amount: 1 }, { lootId: 'Bone', amount: 2 }, { lootId: 'bone', amount: 3 }
  ] }));
  assert.deepEqual(plan.set['inventory.stash'].map(item => item.lootId), [' bone ', 'Bone', 'bone']);
  assert.deepEqual(plan.lists.stash.unknownIds, [' bone ', 'Bone']);
});

test('planner retains all entries at capacity and in overflow, including unknown ids', () => {
  for (const [list, capacity] of [['loadout', 30], ['stash', 60]]) {
    for (const count of [capacity, capacity + 2]) {
      const items = Array.from({ length: count }, (_, i) => ({ lootId: `unknown_${i}`, amount: i + 1 }));
      const plan = planInventorySlots(character({ [list]: items }));
      assert.equal(plan.status, 'planned');
      assert.deepEqual(plan.set[`inventory.${list}`], items.map((item, slot) => ({ ...item, slot })));
      assert.equal(plan.lists[list].overCapacity, count > capacity);
      assert.equal(plan.lists[list].overflowEntries, Math.max(0, count - capacity));
      assert.deepEqual(plan.lists[list].unknownIds, items.map(item => item.lootId));
      assert.equal(plan.lists[list].totalsCheck, true);
    }
  }
});

test('planner migrates reservation items independently and preserves whole first subdocuments', () => {
  const pendingReservation = { reservationId: 'r1', admittedAt: new Date(0),
    preparedEquipment: { weaponSlot1: 'training_sword' }, items: [
      { lootId: 'bone', amount: 2, instance: { id: 'legacy' }, origin: 'old' },
      { lootId: 'bone', amount: 4 }, { lootId: 'unknown', amount: 3, marker: [1, 2] }
    ] };
  const input = freeze(character({ inventorySchemaVersion: 1, pendingReservation,
    stash: [{ lootId: 'bone', amount: 10 }] }));
  const plan = planInventorySlots(input);
  assert.deepEqual(plan.set['inventory.pendingReservation.items'], [
    { ...pendingReservation.items[0], amount: 6, slot: 0 },
    { ...pendingReservation.items[2], slot: 1 }
  ]);
  assert.equal(plan.set['inventory.stash'][0].amount, 10);
  assert.equal(plan.lists['pendingReservation.items'].duplicatesFolded, 1);
  assert.deepEqual(plan.lists['pendingReservation.items'].unknownIds, ['unknown']);
  assert.equal(plan.lists['pendingReservation.items'].totalsCheck, true);
  assert.equal(Object.hasOwn(plan.set, 'inventory.pendingReservation'), false);
});

test('planner aborts the entire character on exact per-lootId totals mismatch', () => {
  const input = freeze(character({ stash: [
    { lootId: 'bone', amount: Number.MAX_SAFE_INTEGER }, { lootId: 'bone', amount: 2 }
  ], loadout: [{ lootId: 'healthpotion', amount: 1 }] }));
  const plan = planInventorySlots(input);
  assert.equal(plan.status, 'aborted');
  assert.equal(plan.reason, 'TOTALS_MISMATCH');
  assert.equal(plan.set, undefined);
  assert.deepEqual(plan.lists.stash.totalsBefore, { bone: '9007199254740993' });
  assert.deepEqual(plan.lists.stash.totalsAfter, { bone: '9007199254740992' });
  assert.equal(plan.lists.stash.totalsCheck, false);
  assert.equal(input.revision, 7);
});

test('planner fails closed on malformed legacy quantities and lists without a write plan', () => {
  for (const amount of [0, -1, 0.5, NaN, Infinity, '2', Number.MAX_SAFE_INTEGER + 1]) {
    const plan = planInventorySlots(character({ stash: [{ lootId: 'bone', amount }] }));
    assert.equal(plan.status, 'aborted');
    assert.equal(plan.reason, 'INVALID_ENTRY');
    assert.equal(plan.set, undefined);
  }
  const plan = planInventorySlots(character({ loadout: 'not an array' }));
  assert.equal(plan.status, 'aborted');
  assert.equal(plan.reason, 'INVALID_LIST');
});

test('planner skips v2 (including split stacks and holes) and unsupported versions untouched', () => {
  for (const inventorySchemaVersion of [2, 3, null]) {
    const input = freeze(character({ inventorySchemaVersion, stash: [
      { lootId: 'bone', amount: 1, slot: 5 }, { lootId: 'bone', amount: 2, slot: 9 }
    ] }));
    const plan = planInventorySlots(input);
    assert.equal(plan.status, 'skipped');
    assert.equal(plan.reason, inventorySchemaVersion === 2 ? 'ALREADY_V2' : 'UNSUPPORTED_VERSION');
    assert.equal(plan.set, undefined);
  }
});

test('planner handles absent lists without manufacturing a reservation', () => {
  const plan = planInventorySlots({ _id: 'empty', revision: 0 });
  assert.equal(plan.status, 'planned');
  assert.deepEqual(plan.set, {
    'inventory.stash': [], 'inventory.loadout': [], 'inventory.inventorySchemaVersion': 2
  });
  assert.deepEqual(Object.keys(plan.lists), ['stash', 'loadout']);
});

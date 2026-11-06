'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { EQUIPMENT_SLOTS } = require('../src/config/equipmentSlots');
const base = inventory => ({ _id: 'character', revision: 7, inventory: { inventorySchemaVersion: 2, stash: [], loadout: [], ...inventory } });
function checkInstance(item, lootId) {
  assert.equal(item.lootId, lootId); assert.match(item.instance.instanceId, /^[a-f0-9]{24}$/);
  if (lootId === 'arming_sword') assert.deepEqual(item.instance.modifiers, { gemSocketCount: 0, gems: [] });
  else assert.deepEqual(Object.keys(item.instance), ['instanceId']);
}

test('v2 -> v3 migration plans equipment stacks, original slots, overflow and reservation without losing units', () => {
  const { planItemInstances } = require('../scripts/migrate_item_instances');
  for (const [list, capacity] of [['stash', 60], ['loadout', 30]]) {
    const entries = Array.from({ length: capacity }, (_, slot) => ({ slot, lootId: slot === 8 ? 'arming_sword' : 'bone', amount: slot === 8 ? 3 : 1 }));
    const input = base({ [list]: entries, preparedEquipment: { helmet: 'placeholder_helmet' },
      pendingReservation: { reservationId: 'r', admittedAt: null, items: [{ slot: 9, lootId: 'shield', amount: 2 }], preparedEquipment: { weaponSlot1: 'arming_sword' } } });
    const before = structuredClone(input); const plan = planItemInstances(input);
    assert.equal(plan.status, 'planned'); assert.equal(plan.set['inventory.inventorySchemaVersion'], 3);
    const migrated = plan.set[`inventory.${list}`]; assert.equal(migrated.length, capacity + 2);
    assert.deepEqual(migrated.filter(i => i.lootId === 'arming_sword').map(i => [i.slot, i.amount]), [[8, 1], [capacity, 1], [capacity + 1, 1]]);
    const units = migrated.filter(i => i.lootId === 'arming_sword'); units.forEach(item => checkInstance(item, 'arming_sword'));
    assert.equal(new Set(units.map(item => item.instance.instanceId)).size, 3);
    const eq = plan.set['inventory.preparedEquipment']; checkInstance(eq.helmet, 'placeholder_helmet');
    for (const slot of EQUIPMENT_SLOTS.filter(slot => slot !== 'helmet')) assert.equal(eq[slot], null);
    const reservation = plan.set['inventory.pendingReservation'];
    assert.equal(reservation.reservationId, 'r'); assert.equal(reservation.admittedAt, null);
    assert.deepEqual(reservation.items.map(i => [i.slot, i.amount]), [[9, 1], [0, 1]]);
    reservation.items.forEach(item => checkInstance(item, 'shield')); checkInstance(reservation.preparedEquipment.weaponSlot1, 'arming_sword');
    assert.deepEqual(input, before);
    assert.deepEqual(planItemInstances({ ...input, inventory: { ...input.inventory, inventorySchemaVersion: 3 } }), { status: 'skipped', reason: 'ALREADY_V3' });
  }
});

test('migration reserves all original slots before placing extra equipment in the lowest holes', () => {
  const { planItemInstances } = require('../scripts/migrate_item_instances');
  const input = base({ stash: [{ slot: 8, lootId: 'arming_sword', amount: 3 }, { slot: 0, lootId: 'bone', amount: 5 }, { slot: 1, lootId: 'shield', amount: 2 }] });
  const entries = planItemInstances(input).set['inventory.stash'];
  assert.deepEqual(entries.map(item => [item.slot, item.lootId, item.amount]), [
    [8, 'arming_sword', 1], [0, 'bone', 5], [1, 'shield', 1], [2, 'arming_sword', 1], [3, 'arming_sword', 1], [4, 'shield', 1] ]);
});

test('migration rejects malformed entries/slots/revisions and skips v1 without rewriting data', async () => {
  const { planItemInstances, migrateItemInstances } = require('../scripts/migrate_item_instances');
  for (const item of [{ slot: 0, lootId: 'shield', amount: 0 }, { slot: 0, lootId: 'shield', amount: 1.5 }, { lootId: 'shield', amount: 1 }]) {
    assert.equal(planItemInstances(base({ stash: [item] })).status, 'aborted');
  }
  const input = { ...base({}), revision: -1 }; let writes = 0;
  const collection = { async *find() { yield input; }, updateOne: async () => { writes++; } };
  const summary = await migrateItemInstances(collection, null, { report: () => {} });
  assert.equal(summary.aborted, 1); assert.equal(summary.exitCode, 1); assert.equal(writes, 0);
  assert.equal(planItemInstances(base({ inventorySchemaVersion: 1 })).status, 'skipped');
});

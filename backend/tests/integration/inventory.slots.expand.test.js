'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const request = require('supertest');
const jwt = require('jsonwebtoken');
const app = require('../../src/app');
const env = require('../../src/config/env');
const db = require('./testDb');
const Character = require('../../src/models/Character');
const Inventory = require('../../src/services/InventoryService');
const Ability = require('../../src/services/AbilityService');
const Progression = require('../../src/services/ProgressionService');
const Session = require('../../src/services/SessionTicketService');
const Currency = require('../../src/services/CurrencyService');

function entries() {
  return [{ slot: 8, lootId: 'bone', amount: 2 }, { slot: 2, lootId: 'bone', amount: 3 }];
}

test('B1 persistence and writer audit', async t => {
  await db.connect();
  t.after(async () => { process.env.INVENTORY_PROTOCOL_GATE = 'false'; await db.closeDatabase(); });
  t.afterEach(async () => { process.env.INVENTORY_PROTOCOL_GATE = 'false'; await db.clearDatabase(); });

  for (const version of [undefined, 1, 2]) {
    await t.test(`GET preserves raw persisted document version ${version}`, async () => {
      const accountId = new mongoose.Types.ObjectId();
      const raw = { _id: new mongoose.Types.ObjectId(), accountId, name: 'Legacy', revision: 12,
        inventory: { ...(version === undefined ? {} : { inventorySchemaVersion: version }),
          stash: entries(), loadout: [{ lootId: 'training_sword', amount: 1, ...(version === 2 ? { slot: 6 } : {}) }],
          pendingReservation: { reservationId: 'res', items: entries(), preparedEquipment: { weaponSlot1: 'recovery_sword' } } } };
      await Character.collection.insertOne(raw);
      const before = await Character.collection.findOne({ accountId });
      const token = jwt.sign({ sub: accountId.toString() }, env.jwtSecret);
      const response = await request(app).get('/character/me/inventory').set('Authorization', `Bearer ${token}`);
      assert.equal(response.status, 200);
      assert.equal(response.body.inventorySchemaVersion, version || 1);
      assert.deepEqual(response.body.stash, version === 2 ? entries().reverse() : entries());
      assert.deepEqual(response.body.pendingReservation.items, version === 2 ? entries().reverse() : entries());
      assert.deepEqual(response.body.loadout, raw.inventory.loadout);
      assert.equal(response.body.revision, 12);
      assert.deepEqual(await Character.collection.findOne({ accountId }), before);
    });
  }

  for (const [name, operation] of [
    ['AbilityService purchase', id => Ability.purchaseAbility(id, { transactionId: 'tx', abilityId: 'arcane_projectile', expectedRevision: 0 })],
    ['ProgressionService commit', id => Progression.commitProgression(id, { attribute: 'vitality', expectedRevision: 0 })],
    ['SessionTicketService Town epoch reset', id => Session.issueTownTicket(id)],
    ['CharacterService profile save', async id => {
      const token = jwt.sign({ sub: id.toString() }, env.jwtSecret);
      const res = await request(app).patch('/character/me/profile').set('Authorization', `Bearer ${token}`).send({ customNote: 'new' });
      assert.equal(res.status, 200);
    }],
    ['CurrencyService read', id => Currency.getBalance(id)]
  ]) {
    await t.test(`v2 ${name} preserves slots, reservation items and version`, async () => {
      process.env.INVENTORY_PROTOCOL_GATE = 'true';
      const accountId = new mongoose.Types.ObjectId();
      await Character.create({ accountId, name: 'Writer', inventory: { inventorySchemaVersion: 2,
        currency: 500, stash: entries(), loadout: entries(),
        ...(name.startsWith('AbilityService') ? {} : {
          pendingReservation: { reservationId: 'res', items: entries() }
        }) } });
      const before = await Inventory.getInventory(accountId);
      await operation(accountId);
      const after = await Inventory.getInventory(accountId);
      assert.equal(after.inventorySchemaVersion, 2);
      assert.deepEqual(after.stash, before.stash); assert.deepEqual(after.loadout, before.loadout);
      assert.deepEqual(after.pendingReservation, before.pendingReservation);
      // Also prove schema-aware save() round trips declared slot fields in a reservation.
      const doc = await Character.findOne({ accountId });
      doc.inventory.pendingReservation = { reservationId: 'res', items: entries() };
      await doc.save();
      assert.deepEqual((await Inventory.getInventory(accountId)).pendingReservation.items, entries().reverse());
    });
  }

  await t.test('v2 admission and loss clear preserve unrelated arrays and schema version', async () => {
    const accountId = new mongoose.Types.ObjectId();
    await Character.create({ accountId, name: 'Reserved', inventory: { inventorySchemaVersion: 2,
      stash: entries(), loadout: entries(), pendingReservation: { reservationId: 'res', items: entries() } } });
    const before = await Inventory.getInventory(accountId);
    const admitted = await Inventory.admitPendingReservation(accountId, 'res');
    assert.deepEqual(admitted.pendingReservation.items, entries().reverse());
    assert.equal(admitted.revision, 1);
    const replay = await Inventory.admitPendingReservation(accountId, 'res');
    assert.equal(replay.revision, 1);
    await Inventory.clearPendingReservation(accountId);
    const after = await Inventory.getInventory(accountId);
    assert.equal(after.revision, 2); assert.equal(after.pendingReservation, null);
    assert.equal(after.inventorySchemaVersion, 2);
    assert.deepEqual(after.stash, before.stash); assert.deepEqual(after.loadout, before.loadout);
  });

  for (const direction of ['moveToLoadout', 'moveToStash']) {
    await t.test(`v1 ${direction} moves the complete entry and does not repair unrelated duplicates`, async () => {
      const accountId = new mongoose.Types.ObjectId();
      const source = direction === 'moveToLoadout' ? 'stash' : 'loadout';
      const destination = direction === 'moveToLoadout' ? 'loadout' : 'stash';
      await Character.create({ accountId, name: 'Legacy', inventory: { [source]: [
        { slot: 7, lootId: 'healthpotion', amount: 2 }, ...entries()
      ] } });
      await Inventory[direction](accountId, 'healthpotion', 2, 0);
      const after = await Inventory.getInventory(accountId);
      assert.deepEqual(after[source], entries());
      assert.deepEqual(after[destination], [{ slot: 7, lootId: 'healthpotion', amount: 2 }]);
      assert.equal(after.revision, 1); assert.equal(after.inventorySchemaVersion, 1);
    });
  }

  await t.test('v1 order and reservation/rollback carry complete subdocuments', async () => {
    const accountId = new mongoose.Types.ObjectId();
    const loadout = [{ slot: 8, lootId: 'bone', amount: 2 }, { slot: 2, lootId: 'healthpotion', amount: 3 }];
    await Character.create({ accountId, name: 'Legacy', inventory: { loadout,
      preparedEquipment: { weaponSlot1: 'arming_sword' } } });
    const ordered = await Inventory.updateOrder(accountId, 'loadout', ['healthpotion', 'bone'], 0);
    assert.deepEqual(ordered.loadout, loadout.slice().reverse());
    const reserved = await Inventory.savePendingReservation(accountId, 'res');
    assert.deepEqual(reserved.pendingReservation.items, loadout.slice().reverse());
    await Inventory.clearPendingReservation(accountId, { rollback: true });
    assert.deepEqual((await Inventory.getInventory(accountId)).loadout, loadout.slice().reverse());
  });
});

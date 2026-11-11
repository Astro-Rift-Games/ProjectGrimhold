'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const request = require('supertest');
const jwt = require('jsonwebtoken');
const app = require('../src/app');
const env = require('../src/config/env');
const Character = require('../src/models/Character');
const Revoked = require('../src/models/RevokedToken');
const { createEquipment } = require('../src/services/ItemInstances');

const accountId = 'bbbbbbbbbbbbbbbbbbbbbbbb';
const token = jwt.sign({ sub: accountId }, env.jwtSecret);
const slots = ['weaponSlot1', 'weaponSlot2', 'offHand1', 'offHand2', 'helmet', 'armor', 'gloves', 'boots'];
const trainingIds = ['training_short_sword', 'training_dagger', 'training_wand'];
const copy = value => JSON.parse(JSON.stringify(value));

function equipment(lootId, instanceId) {
  const item = createEquipment(lootId);
  item.instance.instanceId = instanceId;
  return item;
}

function character(version = 3) {
  return {
    _id: accountId, accountId, name: 'Verifier', schemaVersion: 11, revision: 2,
    inventory: {
      inventorySchemaVersion: version, currency: 123, stash: [], loadout: [],
      preparedEquipment: Object.fromEntries(slots.map(slot => [slot, version === 3 ? null : ''])),
      pendingReservation: null
    }
  };
}

// Each read returns a detached document: mutations before the guarded write cannot
// accidentally make a rejected request appear to have persisted successfully.
function fixture(t, version = 3) {
  const originals = [Character.findOne, Character.findOneAndUpdate, Revoked.exists];
  const state = { stored: character(version), writes: 0, saves: 0 };
  function document() {
    const doc = copy(state.stored);
    doc.markModified = () => {};
    doc.save = async () => { state.saves++; state.stored = copy(doc); return document(); };
    return doc;
  }
  Character.findOne = async () => document();
  Character.findOneAndUpdate = async (filter, update) => {
    state.writes++;
    if (filter.revision !== undefined && filter.revision !== state.stored.revision) return null;
    if (Object.hasOwn(filter, 'inventory.pendingReservation') && state.stored.inventory.pendingReservation) return null;
    const next = copy(state.stored);
    for (const [path, value] of Object.entries(update.$set || {})) {
      const parts = path.split('.');
      const key = parts.pop();
      let target = next;
      for (const part of parts) target = target[part] ??= {};
      target[key] = copy(value);
    }
    for (const [path, value] of Object.entries(update.$inc || {})) {
      const parts = path.split('.');
      const key = parts.pop();
      let target = next;
      for (const part of parts) target = target[part];
      target[key] = (target[key] || 0) + value;
    }
    state.stored = next;
    return document();
  };
  Revoked.exists = async () => false;
  t.after(() => { [Character.findOne, Character.findOneAndUpdate, Revoked.exists] = originals; });
  state.post = (route, body) => request(app).post(`/character/me/inventory/${route}`)
    .set('Authorization', `Bearer ${token}`).send(body);
  state.grant = body => state.post('training-weapon', { weaponId: 'training_dagger', expectedRevision: state.stored.revision, ...body });
  state.unchanged = () => {
    const before = JSON.stringify(state.stored);
    const counters = [state.writes, state.saves];
    return () => {
      assert.equal(JSON.stringify(state.stored), before);
      assert.deepEqual([state.writes, state.saves], counters);
    };
  };
  return state;
}

for (const version of [2, 3]) test(`verifier v${version}: five mixed destinations retain exactly one training weapon`, async t => {
  const state = fixture(t, version);
  for (const [index, destinationSlot] of ['weaponSlot1', 'weaponSlot2', '', 'weaponSlot1', ''].entries()) {
    const weaponId = trainingIds[index % trainingIds.length];
    const response = await state.grant({ weaponId, destinationSlot });
    assert.equal(response.status, 200);
    assert.deepEqual(Object.keys(response.body).sort(), ['loadout', 'preparedEquipment', 'revision', 'stash']);
    assert.equal(response.body.revision, 3 + index);
    const all = [...response.body.stash, ...response.body.loadout,
      ...Object.values(response.body.preparedEquipment).filter(Boolean).map(item => typeof item === 'string' ? { lootId: item, amount: 1 } : { ...item, amount: 1 })];
    const training = all.filter(item => trainingIds.includes(item.lootId));
    assert.equal(training.reduce((sum, item) => sum + item.amount, 0), 1);
    assert.equal(training[0].lootId, weaponId);
    if (version === 3) assert.match(training[0].instance.instanceId, /^[0-9a-f]{24}$/);
    if (destinationSlot) assert.deepEqual(response.body.preparedEquipment[destinationSlot], version === 3
      ? { lootId: weaponId, instance: training[0].instance } : weaponId);
    else assert.equal(response.body.loadout.filter(item => item.lootId === weaponId).length, 1);
    assert.deepEqual(response.body.stash, state.stored.inventory.stash);
    assert.deepEqual(response.body.loadout, state.stored.inventory.loadout);
    assert.deepEqual(response.body.preparedEquipment, state.stored.inventory.preparedEquipment);
    assert.equal(state.writes, index + 1);
    assert.equal(state.saves, 0);
  }
});

test('verifier: string expectedRevision rejects without writes', async t => {
  const state = fixture(t);
  const unchanged = state.unchanged();
  const response = await state.grant({ expectedRevision: '2' });
  assert.equal(response.status, 400);
  assert.equal(response.body.error, 'VALIDATION_FAILED');
  assert.ok(response.body.message);
  assert.ok(Array.isArray(response.body.details));
  unchanged();
});

test('verifier: full Loadout rejects displaced long_sword atomically', async t => {
  const state = fixture(t);
  state.stored.inventory.preparedEquipment.weaponSlot1 = equipment('long_sword', '111111111111111111111111');
  state.stored.inventory.loadout = Array.from({ length: 30 }, (_, slot) => ({ slot, lootId: 'bone', amount: 1 }));
  const unchanged = state.unchanged();
  const response = await state.grant({ destinationSlot: 'weaponSlot1' });
  assert.equal(response.status, 409);
  assert.equal(response.body.error, 'INVENTORY_FULL');
  assert.ok(response.body.message);
  unchanged();
});

test('verifier: ordinary long_sword in Stash is untouched by training grant', async t => {
  const state = fixture(t);
  const sword = { slot: 7, amount: 1, ...equipment('long_sword', '222222222222222222222222') };
  state.stored.inventory.stash = [copy(sword)];
  const response = await state.grant({ destinationSlot: 'weaponSlot1' });
  assert.equal(response.status, 200);
  assert.equal(response.body.revision, 3);
  assert.deepEqual(response.body.stash, [sword]);
  assert.deepEqual(state.stored.inventory.stash, [sword]);
  assert.equal(response.body.preparedEquipment.weaponSlot1.lootId, 'training_dagger');
  assert.match(response.body.preparedEquipment.weaponSlot1.instance.instanceId, /^[0-9a-f]{24}$/);
  assert.deepEqual(response.body.loadout, []);
  assert.equal(state.writes, 1);
  assert.equal(state.saves, 0);
});

test('verifier: weaponless reservation rejects with exact Trainer guidance without writes', async t => {
  const state = fixture(t);
  const unchanged = state.unchanged();
  const response = await state.post('reservation', { reservationId: 'weaponless' });
  assert.equal(response.status, 409);
  assert.deepEqual(response.body, {
    error: 'PREPARED_WEAPON_REQUIRED',
    message: 'Equip a weapon in a Weapon Set or ask the Trainer before preparing an expedition.'
  });
  unchanged();
});

test('verifier: reservation preserves granted identity and blocks replacement without writes', async t => {
  const state = fixture(t);
  const granted = await state.grant({ destinationSlot: 'weaponSlot1' });
  assert.equal(granted.status, 200);
  assert.equal(granted.body.revision, 3);
  const weapon = granted.body.preparedEquipment.weaponSlot1;
  assert.equal(weapon.lootId, 'training_dagger');
  assert.match(weapon.instance.instanceId, /^[0-9a-f]{24}$/);
  const reserved = await state.post('reservation', { reservationId: 'training-verifier' });
  assert.equal(reserved.status, 201);
  assert.equal(reserved.body.revision, 4);
  assert.equal(reserved.body.pendingReservation.reservationId, 'training-verifier');
  assert.deepEqual(reserved.body.pendingReservation.preparedEquipment.weaponSlot1, weapon);
  assert.deepEqual(reserved.body.pendingReservation.items, []);
  assert.deepEqual(state.stored.inventory.pendingReservation, {
    ...reserved.body.pendingReservation, preparedAbilities: { slot1: '', slot2: '' }
  });
  assert.equal(state.writes, 2);
  assert.equal(state.saves, 0);
  const unchanged = state.unchanged();
  const rejected = await state.grant({ weaponId: 'training_wand', destinationSlot: 'weaponSlot2' });
  assert.equal(rejected.status, 409);
  assert.deepEqual(rejected.body, {
    error: 'RESERVATION_PENDING', message: 'Cannot request a training weapon while a raid reservation is pending.'
  });
  unchanged();
});

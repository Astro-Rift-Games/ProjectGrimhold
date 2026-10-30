'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const request = require('supertest');
const jwt = require('jsonwebtoken');
const app = require('../src/app');
const env = require('../src/config/env');
const Character = require('../src/models/Character');
const RevokedToken = require('../src/models/RevokedToken');
const AuthoritativeExtractionResult = require('../src/models/AuthoritativeExtractionResult');
const { EQUIPMENT_SLOTS } = require('../src/config/equipmentSlots');

const equipment = (values = {}) => Object.fromEntries(EQUIPMENT_SLOTS.map(slot => [slot, values[slot] || '']));
const admittedAt = new Date('2026-10-29T12:00:00.000Z');
const base = '/character/me/inventory';

test('reservation admission and loss lifecycle', async t => {
  let stored;
  let results;
  let writes;
  const token = jwt.sign({ sub: 'account' }, env.jwtSecret);
  const originals = {
    findOne: Character.findOne,
    findOneAndUpdate: Character.findOneAndUpdate,
    revokedExists: RevokedToken.exists,
    resultExists: AuthoritativeExtractionResult.exists
  };
  t.after(() => {
    Character.findOne = originals.findOne;
    Character.findOneAndUpdate = originals.findOneAndUpdate;
    RevokedToken.exists = originals.revokedExists;
    AuthoritativeExtractionResult.exists = originals.resultExists;
  });
  const call = (method, path, body) => {
    const req = request(app)[method](path).set('Authorization', `Bearer ${token}`);
    return body === undefined ? req : req.send(body);
  };
  const snapshot = async () => {
    const response = await call('get', base);
    assert.equal(response.status, 200);
    return response.body;
  };
  const rejectUnchanged = async (method, path, body, status, error) => {
    const before = await snapshot();
    const beforeResults = structuredClone(results);
    const beforeWrites = writes;
    const response = await call(method, path, body);
    assert.equal(response.status, status);
    assert.equal(response.body.error, error);
    assert.equal(typeof response.body.message, 'string');
    assert.deepEqual(await snapshot(), before);
    assert.deepEqual(results, beforeResults);
    assert.equal(writes, beforeWrites);
    return response;
  };
  t.beforeEach(() => {
    stored = {
      _id: 'character', accountId: 'account', revision: 7,
      inventory: {
        currency: 123,
        stash: [{ lootId: 'iron_ore', amount: 3 }], loadout: [],
        preparedEquipment: equipment(),
        pendingReservation: {
          reservationId: 'reservation', admittedAt: null,
          items: [{ lootId: 'health_potion', amount: 4 }],
          preparedEquipment: equipment({ weaponSlot1: 'long_sword', helmet: 'iron_helm', offHand2: 'shield' })
        },
        appliedExtractionReceipts: []
      }
    };
    results = [];
    writes = 0;
    RevokedToken.exists = async () => null;
    Character.findOne = async query => {
      assert.equal(query.accountId, 'account');
      if (!stored) return null;
      return Object.assign(structuredClone(stored), {
        markModified() {},
        async save() { throw new Error('Canonical fixture must not need migration'); }
      });
    };
    Character.findOneAndUpdate = async (query, update) => {
      assert.equal(query.accountId, 'account');
      assert.equal(query.revision, stored.revision);
      assert.deepEqual(update.$inc, { revision: 1 });
      for (const [path, value] of Object.entries(query)) {
        if (path.startsWith('inventory.')) {
          const actual = path.split('.').reduce((value, key) => value?.[key], stored);
          assert.equal(actual ?? null, value);
        }
      }
      for (const [path, value] of Object.entries(update.$set || {})) {
        const keys = path.split('.');
        const field = keys.pop();
        const owner = keys.reduce((value, key) => value[key], stored);
        owner[field] = path === 'inventory.preparedEquipment' ? equipment(value) : structuredClone(value);
      }
      stored.revision++;
      writes++;
      return structuredClone(stored);
    };
    AuthoritativeExtractionResult.exists = async query => {
      // Results bind to the character's accountId, not its character _id or reservationId.
      assert.deepEqual(query, { accountId: 'account', consumed: { $ne: true } });
      return results.find(result => result.accountId === query.accountId && result.consumed !== true) || null;
    };
  });

  await t.test('schema defaults admittedAt to null and retains a Date when admitted', () => {
    const doc = new Character({ inventory: { pendingReservation: { reservationId: 'r' } } });
    assert.equal(doc.inventory.pendingReservation.admittedAt, null);
    doc.inventory.pendingReservation.admittedAt = admittedAt;
    assert.ok(doc.inventory.pendingReservation.toObject().admittedAt instanceof Date);
  });

  for (const [name, value] of [['legacy', undefined], ['not admitted', null], ['admitted', admittedAt]]) {
    await t.test(`inventory serializes ${name} admittedAt as ISO or null`, async () => {
      stored.inventory.pendingReservation.admittedAt = value;
      const inventory = await snapshot();
      assert.equal(inventory.pendingReservation.admittedAt, value ? value.toISOString() : null);
      assert.equal(writes, 0);
    });
  }

  await t.test('admit preserves all ownership, marks admission once and increments revision once', async () => {
    const before = await snapshot();
    const response = await call('post', `${base}/reservation/admit`, { reservationId: ' reservation ' });
    assert.equal(response.status, 200);
    const timestamp = response.body.pendingReservation.admittedAt;
    assert.equal(new Date(timestamp).toISOString(), timestamp);
    assert.ok(stored.inventory.pendingReservation.admittedAt instanceof Date);
    assert.deepEqual(response.body, {
      pendingReservation: { ...before.pendingReservation, admittedAt: timestamp }, revision: 8
    });
    assert.deepEqual(await snapshot(), {
      ...before, pendingReservation: response.body.pendingReservation, revision: 8
    });
    assert.equal(writes, 1);
  });

  await t.test('same admitted id succeeds unchanged even if the reservation has no weapon', async () => {
    stored.inventory.pendingReservation.admittedAt = admittedAt;
    stored.inventory.pendingReservation.preparedEquipment = equipment();
    const before = await snapshot();
    const response = await call('post', `${base}/reservation/admit`, { reservationId: 'reservation' });
    assert.equal(response.status, 200);
    assert.deepEqual(response.body, { pendingReservation: before.pendingReservation, revision: 7 });
    assert.deepEqual(await snapshot(), before);
    assert.equal(writes, 0);
  });

  for (const state of ['missing', 'different', 'different admitted']) {
    await t.test(`admit rejects ${state} reservation unchanged`, async () => {
      if (state === 'missing') stored.inventory.pendingReservation = null;
      if (state === 'different admitted') stored.inventory.pendingReservation.admittedAt = admittedAt;
      await rejectUnchanged('post', `${base}/reservation/admit`, { reservationId: 'other' }, 422, 'NO_ACTIVE_RESERVATION');
    });
  }

  for (const body of [{}, { reservationId: null }, { reservationId: '' }, { reservationId: '   ' },
    { reservationId: 42 }, { reservationId: {} }, { reservationId: ['reservation'] }]) {
    await t.test(`admit validates ${JSON.stringify(body)} without mutation`, async () => {
      const response = await rejectUnchanged('post', `${base}/reservation/admit`, body, 400, 'VALIDATION_FAILED');
      assert.ok(response.body.details.some(detail => detail.field === 'reservationId'));
    });
  }

  await t.test('admit requires authentication', async () => {
    const before = await snapshot();
    const response = await request(app).post(`${base}/reservation/admit`).send({ reservationId: 'reservation' });
    assert.equal(response.status, 401);
    assert.equal(response.body.error, 'UNAUTHORIZED');
    assert.deepEqual(await snapshot(), before);
    assert.equal(writes, 0);
  });

  await t.test('admit rejects a revision race without mutation', async () => {
    Character.findOneAndUpdate = async query => {
      assert.equal(query.revision, 7);
      return null;
    };
    await rejectUnchanged('post', `${base}/reservation/admit`, { reservationId: 'reservation' }, 409, 'REVISION_CONFLICT');
  });

  await t.test('admit returns character not found', async () => {
    stored = null;
    const response = await call('post', `${base}/reservation/admit`, { reservationId: 'reservation' });
    assert.equal(response.status, 404);
    assert.equal(response.body.error, 'CHARACTER_NOT_FOUND');
    assert.equal(writes, 0);
  });

  for (const hasResult of [false, true]) {
    await t.test(`admitted rollback is rejected unchanged (pending result: ${hasResult})`, async () => {
      stored.inventory.pendingReservation.admittedAt = admittedAt;
      if (hasResult) results.push({ accountId: 'account', consumed: false });
      await rejectUnchanged('delete', `${base}/reservation?rollback=true`, undefined, 409, 'RESERVATION_ADMITTED');
    });
  }

  for (const consumed of [false, undefined]) {
    for (const operation of ['clear', 'replace empty', 'replace populated']) {
      await t.test(`${operation} blocks any unconsumed result targeting admitted character (${consumed})`, async () => {
        stored.inventory.pendingReservation.admittedAt = admittedAt;
        results.push({ accountId: 'account', raidId: 'unrelated-raid-name', consumed });
        if (operation === 'replace populated') {
          stored.inventory.loadout = [{ lootId: 'iron_ore', amount: 2 }];
          stored.inventory.preparedEquipment = equipment({ weaponSlot1: 'arming_sword' });
        }
        await rejectUnchanged(operation === 'clear' ? 'delete' : 'post', `${base}/reservation`,
          operation === 'clear' ? undefined : { reservationId: 'new-reservation' }, 409, 'EXTRACTION_RESULT_PENDING');
      });
    }
  }

  await t.test('loss clear discards admitted items and all Equipment without changing active ownership', async () => {
    stored.inventory.pendingReservation.admittedAt = admittedAt;
    results.push({ accountId: 'other-account', consumed: false }, { accountId: 'account', consumed: true });
    const before = await snapshot();
    const response = await call('delete', `${base}/reservation`);
    assert.equal(response.status, 200);
    assert.deepEqual(response.body, { pendingReservation: null, revision: 8 });
    assert.deepEqual(await snapshot(), { ...before, pendingReservation: null, revision: 8 });
    assert.equal(writes, 1);
  });

  await t.test('replacement of admitted reservation never recovers its items or Equipment', async () => {
    stored.inventory.pendingReservation.admittedAt = admittedAt;
    results.push({ accountId: 'other-account', consumed: false }, { accountId: 'account', consumed: true });
    const before = await snapshot();
    const response = await call('post', `${base}/reservation`, { reservationId: 'new-reservation' });
    assert.equal(response.status, 201);
    assert.deepEqual(response.body, {
      pendingReservation: { reservationId: 'new-reservation', admittedAt: null, items: [],
        preparedEquipment: equipment({ weaponSlot1: 'arming_sword' }) }, revision: 8
    });
    assert.deepEqual(await snapshot(), { ...before, ...response.body });
    assert.equal(writes, 1);
  });

  await t.test('same-id save of admitted reservation remains idempotent with a pending extraction', async () => {
    stored.inventory.pendingReservation.admittedAt = admittedAt;
    results.push({ accountId: 'account', consumed: false });
    const before = await snapshot();
    const response = await call('post', `${base}/reservation`, { reservationId: 'reservation' });
    assert.equal(response.status, 201);
    assert.deepEqual(response.body, { pendingReservation: before.pendingReservation, revision: 7 });
    assert.deepEqual(await snapshot(), before);
    assert.equal(writes, 0);
  });

  await t.test('pre-admission replacement still recovers items and all Equipment', async () => {
    const before = await snapshot();
    const response = await call('post', `${base}/reservation`, { reservationId: 'new-reservation' });
    assert.equal(response.status, 201);
    assert.deepEqual(response.body, {
      pendingReservation: { ...before.pendingReservation, reservationId: 'new-reservation' }, revision: 8
    });
    assert.deepEqual(await snapshot(), { ...before, ...response.body });
    assert.equal(writes, 1);
  });

  await t.test('pre-admission rollback still restores items and all Equipment', async () => {
    const before = await snapshot();
    const response = await call('delete', `${base}/reservation?rollback=true`);
    assert.equal(response.status, 200);
    assert.deepEqual(response.body, { pendingReservation: null, revision: 8 });
    assert.deepEqual(await snapshot(), { ...before, ...response.body,
      loadout: before.pendingReservation.items, preparedEquipment: before.pendingReservation.preparedEquipment });
    assert.equal(writes, 1);
  });

  await t.test('pre-admission clear still discards a reservation without restoring it', async () => {
    const before = await snapshot();
    const response = await call('delete', `${base}/reservation`);
    assert.equal(response.status, 200);
    assert.deepEqual(response.body, { pendingReservation: null, revision: 8 });
    assert.deepEqual(await snapshot(), { ...before, ...response.body });
    assert.equal(writes, 1);
  });
});

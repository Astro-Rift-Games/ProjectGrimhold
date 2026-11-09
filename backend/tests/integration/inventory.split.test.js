'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const jwt = require('jsonwebtoken');
const request = require('supertest');
const app = require('../../src/app');
const env = require('../../src/config/env');
const db = require('./testDb');
const Character = require('../../src/models/Character');

const path = '/character/me/inventory/slots/split';
const entry = (slot, lootId = 'bone', amount = 1) => ({ slot, lootId, amount });

test('split HTTP and atomic Mongo persistence', async t => {
  await db.connect();
  const previous = { INVENTORY_PROTOCOL_GATE: process.env.INVENTORY_PROTOCOL_GATE,
    INVENTORY_SPLIT_ENABLED: process.env.INVENTORY_SPLIT_ENABLED };
  t.after(async () => {
    for (const [key, value] of Object.entries(previous)) {
      if (value === undefined) delete process.env[key]; else process.env[key] = value;
    }
    await db.closeDatabase();
  });
  let accountId, token;
  const body = { list: 'stash', sourceSlot: 9, amount: 8, destinationSlot: 1, lootId: 'bone', expectedRevision: 0 };
  t.beforeEach(async () => {
    process.env.INVENTORY_PROTOCOL_GATE = 'false';
    process.env.INVENTORY_SPLIT_ENABLED = 'true';
    await db.clearDatabase();
    accountId = new mongoose.Types.ObjectId();
    token = jwt.sign({ sub: accountId.toString() }, env.jwtSecret);
    await Character.create({ accountId, name: 'Split', inventory: { inventorySchemaVersion: 2, currency: 500,
      stash: [entry(9, 'bone', 13), entry(3, 'bone', 2)], loadout: [entry(8, 'healthpotion', 3)] } });
  });
  const send = (dto = body, header) => {
    const req = request(app).post(path).set('Authorization', `Bearer ${token}`);
    if (header !== undefined) req.set('X-Grimhold-Inventory-Protocol', header);
    return req.send(dto);
  };
  const stored = () => Character.findOne({ accountId }).lean();
  const get = async () => {
    const res = await request(app).get('/character/me/inventory').set('Authorization', `Bearer ${token}`)
      .set('X-Grimhold-Inventory-Protocol', '2');
    assert.equal(res.status, 200, JSON.stringify(res.body));
    return res.body;
  };
  async function rejected(patch, status, error, header) {
    const before = await stored();
    const res = await send({ ...body, ...patch }, header);
    assert.equal(res.status, status, JSON.stringify(res.body));
    assert.equal(res.body.error, error);
    assert.deepEqual(await stored(), before, 'entire stored document including revision must be unchanged');
    return res;
  }

  for (const list of ['stash', 'loadout']) await t.test(`${list} INV-AC-03: 13 -> 5 + 8 with one revision and slots`, async () => {
    await Character.updateOne({ accountId }, { $set: { [`inventory.${list}`]: [entry(9, 'bone', 13), entry(3, 'bone', 2)] } });
    const before = await stored();
    const res = await send({ ...body, list });
    assert.equal(res.status, 200, JSON.stringify(res.body));
    const expected = [entry(1, 'bone', 8), entry(3, 'bone', 2), entry(9, 'bone', 5)];
    assert.deepEqual(Object.keys(res.body).sort(), ['loadout', 'revision', 'stash']);
    assert.deepEqual(res.body[list], expected); assert.equal(res.body.revision, 1);
    assert.equal(res.body[list].filter(i => i.slot !== 3).reduce((sum, i) => sum + i.amount, 0), 13);
    const after = await stored();
    // Only responses are sorted by contract; persistence array order is not a slot address.
    assert.deepEqual(after.inventory[list].slice().sort((a, b) => a.slot - b.slot), expected);
    assert.deepEqual({ ...after, inventory: { ...after.inventory, [list]: before.inventory[list] } }, { ...before, revision: 1 });
    assert.deepEqual((await get())[list], expected);
  });
  await t.test('authentication is required', async () => {
    const before = await stored();
    const res = await request(app).post(path).send(body);
    assert.equal(res.status, 401); assert.equal(res.body.error, 'UNAUTHORIZED');
    assert.deepEqual(await stored(), before);
  });
  await t.test('flag false rejects before inventory reads even with protocol gate on', async () => {
    process.env.INVENTORY_SPLIT_ENABLED = 'false';
    process.env.INVENTORY_PROTOCOL_GATE = 'true';
    const before = await stored();
    const original = Character.findOne;
    let reads = 0;
    Character.findOne = () => { reads++; throw new Error('disabled split must not read character'); };
    try {
      const res = await send();
      assert.equal(res.status, 403);
      assert.deepEqual(res.body, { error: 'FEATURE_DISABLED', message: 'Inventory split is disabled.' });
      assert.equal(reads, 0);
    } finally { Character.findOne = original; }
    assert.deepEqual(await stored(), before);
    process.env.INVENTORY_PROTOCOL_GATE = 'false';
    process.env.INVENTORY_SPLIT_ENABLED = 'false';
    await rejected({}, 403, 'FEATURE_DISABLED');
  });
  await t.test('enabled split uses protocol gate: 426 before body validation, v1 409, v2 succeeds', async () => {
    process.env.INVENTORY_PROTOCOL_GATE = 'true';
    for (const header of [undefined, '1', '4']) await rejected({ amount: 0 }, 426, 'CLIENT_UPGRADE_REQUIRED', header);
    await Character.updateOne({ accountId }, { $set: { 'inventory.inventorySchemaVersion': 1 } });
    await rejected({}, 409, 'INVENTORY_MIGRATION_REQUIRED', '2');
    await Character.updateOne({ accountId }, { $set: { 'inventory.inventorySchemaVersion': 2 } });
    const res = await send(body, '2');
    assert.equal(res.status, 200, JSON.stringify(res.body)); assert.equal(res.body.revision, 1);
  });
  await t.test('v1 and missing schema reject with gate off', async () => {
    await Character.updateOne({ accountId }, { $set: { 'inventory.inventorySchemaVersion': 1 } });
    await rejected({}, 409, 'INVENTORY_MIGRATION_REQUIRED');
    await Character.updateOne({ accountId }, { $unset: { 'inventory.inventorySchemaVersion': 1 } });
    await rejected({}, 409, 'INVENTORY_MIGRATION_REQUIRED');
  });
  for (const amount of [0, -1, 13, 14, 0.5, '8', null, undefined, Number.MAX_SAFE_INTEGER + 1]) {
    await t.test(`invalid quantity ${String(amount)} is atomic`, () => rejected({ amount }, 400, 'INVALID_QUANTITY'));
  }
  for (const key of ['sourceSlot', 'destinationSlot']) for (const value of [-1, 0.5, '1', true, null, undefined]) {
    await t.test(`invalid ${key} ${String(value)} is atomic`, () => rejected({ [key]: value }, 400, 'VALIDATION_FAILED'));
  }
  for (const patch of [{ list: 'other' }, { list: null }, { lootId: '' }, { lootId: null },
    { destinationSlot: 9 }, { expectedRevision: -1 }, { expectedRevision: 0.5 }, { expectedRevision: '0' }, { expectedRevision: null }, { expectedRevision: undefined }]) {
    await t.test(`invalid DTO ${JSON.stringify(patch)} is atomic`, () => rejected(patch, 400, 'VALIDATION_FAILED'));
  }
  await t.test('missing/mismatched source never consumes another stack with that ID', async () => {
    await rejected({ sourceSlot: 7 }, 409, 'INSUFFICIENT_STASH_ITEMS');
    await rejected({ lootId: 'healthpotion' }, 409, 'INSUFFICIENT_STASH_ITEMS');
    await rejected({ list: 'loadout', sourceSlot: 7 }, 409, 'INSUFFICIENT_LOADOUT_ITEMS');
    await rejected({ list: 'loadout', sourceSlot: 8 }, 409, 'INSUFFICIENT_LOADOUT_ITEMS');
  });
  for (const lootId of ['bone', 'healthpotion']) await t.test(`occupied destination ${lootId} rejects without merge/swap`, async () => {
    await Character.updateOne({ accountId }, { $push: { 'inventory.stash': entry(1, lootId, 4) } });
    await rejected({}, 409, 'SLOT_OCCUPIED');
  });
  for (const list of ['stash', 'loadout']) {
    const capacity = list === 'stash' ? 60 : 30;
    await t.test(`${list} placement boundary, full/overfull holes and overflow are atomic`, async () => {
      await Character.updateOne({ accountId }, { $set: { [`inventory.${list}`]: [entry(capacity + 5, 'bone', 13)] } });
      await rejected({ list, sourceSlot: capacity + 5, destinationSlot: capacity }, 409, 'INVENTORY_FULL');
      await rejected({ list, sourceSlot: capacity + 5, destinationSlot: capacity + 1 }, 409, 'INVENTORY_FULL');
      const res = await send({ ...body, list, sourceSlot: capacity + 5, destinationSlot: capacity - 1 });
      assert.equal(res.status, 200, JSON.stringify(res.body));
      assert.deepEqual(res.body[list], [entry(capacity - 1, 'bone', 8), entry(capacity + 5, 'bone', 5)]);
      assert.equal(res.body.revision, 1);
      for (const occupied of [capacity, capacity + 1]) {
        const items = Array.from({ length: occupied }, (_, i) => entry(i + 1, 'bone', 13));
        await Character.updateOne({ accountId }, { $set: { [`inventory.${list}`]: items } });
        await rejected({ list, sourceSlot: occupied, destinationSlot: 0, expectedRevision: 1 }, 409, 'INVENTORY_FULL');
      }
    });
  }
  await t.test('pending reservation and revision conflict are atomic', async () => {
    await rejected({ expectedRevision: 1 }, 409, 'REVISION_CONFLICT');
    await Character.updateOne({ accountId }, { $set: { 'inventory.pendingReservation': { reservationId: 'pending', items: [entry(5, 'bone', 2)] } } });
    await rejected({}, 409, 'RESERVATION_PENDING');
  });
  await t.test('revision race rejects without a partial split; only competing writer advances revision', async () => {
    const original = Character.findOneAndUpdate;
    const before = await stored();
    Character.findOneAndUpdate = async function(filter, update, options) {
      await Character.updateOne({ accountId }, { $inc: { revision: 1 } });
      return original.call(this, filter, update, options);
    };
    try {
      const res = await send();
      assert.equal(res.status, 409); assert.equal(res.body.error, 'REVISION_CONFLICT');
      assert.deepEqual(await stored(), { ...before, revision: 1 });
    } finally { Character.findOneAndUpdate = original; }
  });
  await t.test('PRESERVE: existing slot move stays enabled when split is off', async () => {
    process.env.INVENTORY_SPLIT_ENABLED = 'false';
    const res = await request(app).post('/character/me/inventory/slots/move').set('Authorization', `Bearer ${token}`)
      .send({ fromList: 'stash', fromSlot: 9, toList: 'loadout', toSlot: 1, lootId: 'bone', expectedRevision: 0 });
    assert.equal(res.status, 200, JSON.stringify(res.body));
    assert.deepEqual(res.body, { stash: [entry(3, 'bone', 2)], loadout: [entry(1, 'bone', 13), entry(8, 'healthpotion', 3)], revision: 1 });
  });
});

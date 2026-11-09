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

const path = '/character/me/inventory/transfer-all';
const entry = (slot, lootId = 'bone', amount = 1) => ({ slot, lootId, amount });

test('transfer-all HTTP and atomic Mongo persistence', async t => {
  await db.connect();
  const previous = process.env.INVENTORY_PROTOCOL_GATE;
  t.after(async () => {
    if (previous === undefined) delete process.env.INVENTORY_PROTOCOL_GATE; else process.env.INVENTORY_PROTOCOL_GATE = previous;
    await db.closeDatabase();
  });
  let accountId, token;
  const body = { direction: 'toLoadout', expectedRevision: 0 };
  t.beforeEach(async () => {
    process.env.INVENTORY_PROTOCOL_GATE = 'false';
    await db.clearDatabase();
    accountId = new mongoose.Types.ObjectId();
    token = jwt.sign({ sub: accountId.toString() }, env.jwtSecret);
    await Character.create({ accountId, name: 'Bulk', inventory: { inventorySchemaVersion: 2, currency: 500,
      stash: [entry(9, 'bone', 13), entry(3, 'healthpotion', 2)], loadout: [entry(8, 'bone', 3)] } });
  });
  const send = (dto = body, header) => {
    const req = request(app).post(path).set('Authorization', `Bearer ${token}`);
    if (header !== undefined) req.set('X-Grimhold-Inventory-Protocol', header);
    return req.send(dto);
  };
  const stored = () => Character.findOne({ accountId }).lean();
  async function rejected(patch, status, error, header) {
    const before = await stored();
    const res = await send({ ...body, ...patch }, header);
    assert.equal(res.status, status, JSON.stringify(res.body));
    assert.equal(res.body.error, error);
    assert.deepEqual(await stored(), before, 'stored document including revision must be unchanged');
  }

  await t.test('S5 moves all stacks with one revision increment and returns per-stack results', async () => {
    const res = await send();
    assert.equal(res.status, 200, JSON.stringify(res.body));
    assert.deepEqual(Object.keys(res.body).sort(), ['loadout', 'results', 'revision', 'stash']);
    assert.deepEqual(res.body.stash, []);
    assert.deepEqual(res.body.loadout, [entry(0, 'healthpotion', 2), entry(8, 'bone', 16)]);
    assert.deepEqual(res.body.results, [
      { slot: 3, lootId: 'healthpotion', amount: 2, moved: true },
      { slot: 9, lootId: 'bone', amount: 13, moved: true }]);
    assert.equal(res.body.revision, 1);
    const after = await stored();
    assert.equal(after.revision, 1);
    assert.deepEqual(after.inventory.stash, []);
  });
  await t.test('skips refused stacks, persists the rest in one write', async () => {
    const loadout = Array.from({ length: 29 }, (_, i) => entry(i, `filler_${i}`));
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': loadout } });
    const res = await send();
    assert.equal(res.status, 200, JSON.stringify(res.body));
    assert.deepEqual(res.body.results.map(r => [r.slot, r.moved, r.errorCode]),
      [[3, true, undefined], [9, false, 'INVENTORY_FULL']]);
    assert.equal(res.body.revision, 1);
    assert.deepEqual(res.body.stash, [entry(9, 'bone', 13)]);
  });
  await t.test('nothing moves: no write, revision unchanged', async () => {
    await Character.updateOne({ accountId }, { $set: { 'inventory.stash': [] } });
    const before = await stored();
    const res = await send();
    assert.equal(res.status, 200); assert.equal(res.body.revision, 0); assert.deepEqual(res.body.results, []);
    assert.deepEqual(await stored(), before);
  });
  await t.test('authentication is required', async () => {
    const res = await request(app).post(path).send(body);
    assert.equal(res.status, 401);
  });
  await t.test('revision conflict and pending reservation write nothing', async () => {
    await rejected({ expectedRevision: 7 }, 409, 'REVISION_CONFLICT');
    await Character.updateOne({ accountId }, { $set: { 'inventory.pendingReservation': { reservationId: 'r', items: [] } } });
    await rejected({}, 409, 'RESERVATION_PENDING');
  });
  await t.test('protocol gate: 426 without header, v1 inventory 409', async () => {
    process.env.INVENTORY_PROTOCOL_GATE = 'true';
    await rejected({}, 426, 'CLIENT_UPGRADE_REQUIRED');
    await Character.updateOne({ accountId }, { $set: { 'inventory.inventorySchemaVersion': 1 } });
    await rejected({}, 409, 'INVENTORY_MIGRATION_REQUIRED', '2');
  });
  for (const patch of [{ direction: 'x' }, { direction: null }, { direction: undefined }, { expectedRevision: -1 },
    { expectedRevision: 0.5 }, { expectedRevision: undefined }]) {
    await t.test(`invalid DTO ${JSON.stringify(patch)} is atomic`, () => rejected(patch, 400, 'VALIDATION_FAILED'));
  }
});

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
const Result = require('../../src/models/AuthoritativeExtractionResult');
const entry = (slot, lootId = 'bone', amount = 1) => ({ slot, lootId, amount });
const full = n => Array.from({ length: n }, (_, slot) => entry(slot, `item_${slot}`));

test('B2 physical slots HTTP and atomic persistence', async t => {
  await db.connect();
  const gate = process.env.INVENTORY_PROTOCOL_GATE;
  t.after(async () => {
    if (gate === undefined) delete process.env.INVENTORY_PROTOCOL_GATE; else process.env.INVENTORY_PROTOCOL_GATE = gate;
    await db.closeDatabase();
  });
  let accountId, token;
  t.beforeEach(async () => {
    process.env.INVENTORY_PROTOCOL_GATE = 'false';
    await db.clearDatabase();
    accountId = new mongoose.Types.ObjectId();
    token = jwt.sign({ sub: accountId.toString() }, env.jwtSecret);
    await Character.create({ accountId, name: 'Slots', inventory: { inventorySchemaVersion: 2, currency: 500,
      preparedEquipment: { weaponSlot1: 'arming_sword' },
      stash: [entry(9, 'bone', 4), entry(3, 'bone', 8)], loadout: [entry(8, 'healthpotion', 2)] } });
  });
  const send = (method, path, body, header) => {
    const req = request(app)[method](`/character${path}`).set('Authorization', `Bearer ${token}`);
    if (header !== undefined) req.set('X-Grimhold-Inventory-Protocol', header);
    return body === undefined ? req : req.send(body);
  };
  const get = async () => {
    const res = await send('get', '/me/inventory', undefined, '2');
    assert.equal(res.status, 200); return res.body;
  };
  async function rejected(method, path, body, status, code) {
    const before = await get();
    const res = await send(method, path, body, '2');
    assert.equal(res.status, status, JSON.stringify(res.body)); assert.equal(res.body.error, code);
    assert.deepEqual(await get(), before);
    return res;
  }
  await t.test('sourceSlot transfers and slot order are persisted with holes and duplicate IDs', async () => {
    const moved = await send('post', '/me/inventory/stash/move-to-loadout', { lootId: 'bone', amount: 4, sourceSlot: 9, expectedRevision: 0 });
    assert.equal(moved.status, 200, JSON.stringify(moved.body));
    assert.deepEqual(moved.body.stash, [entry(3, 'bone', 8)]);
    assert.deepEqual(moved.body.loadout, [entry(0, 'bone', 4), entry(8, 'healthpotion', 2)]);
    const ordered = await send('put', '/me/inventory/order', { list: 'loadout', slotOrder: [8, 0], expectedRevision: 1 });
    assert.equal(ordered.status, 200, JSON.stringify(ordered.body));
    assert.deepEqual(ordered.body.loadout, [entry(0, 'healthpotion', 2), entry(1, 'bone', 4)]);
    assert.equal((await get()).revision, 2);
  });
  await t.test('source addressing v2/v1 error contracts and malformed slot validation', async () => {
    for (const path of ['/me/inventory/stash/move-to-loadout', '/me/inventory/loadout/move-to-stash', '/me/inventory/shop/sell']) {
      const body = { transactionId: 'sale', lootId: 'bone', amount: 1, expectedRevision: 0 };
      const response = await rejected('post', path, body, 409, 'INVENTORY_SLOT_ADDRESS_REQUIRED');
      assert.deepEqual(response.body, { error: 'INVENTORY_SLOT_ADDRESS_REQUIRED', message: 'A source slot is required for this inventory operation.' });
      for (const sourceSlot of [-1, 0.5, null, '3', true]) await rejected('post', path, { ...body, sourceSlot }, 400, 'VALIDATION_FAILED');
      await Character.updateOne({ accountId }, { $set: { 'inventory.inventorySchemaVersion': 1 } });
      await rejected('post', path, { ...body, sourceSlot: 3 }, 409, 'INVENTORY_MIGRATION_REQUIRED');
      await Character.updateOne({ accountId }, { $set: { 'inventory.inventorySchemaVersion': 2 } });
    }
    await rejected('put', '/me/inventory/order', { list: 'stash', lootIds: ['bone', 'bone'], expectedRevision: 0 }, 409, 'INVENTORY_SLOT_ADDRESS_REQUIRED');
    for (const slotOrder of [[3], [3, 3], [3, 4], [3, -1], ['3', 9]]) {
      await rejected('put', '/me/inventory/order', { list: 'stash', slotOrder, expectedRevision: 0 }, 400, 'VALIDATION_FAILED');
    }
  });
  await t.test('slot move route: authentication, gate, v1, validation, swap, merge and stale revision', async () => {
    const path = '/me/inventory/slots/move';
    const body = { fromList: 'stash', fromSlot: 9, toList: 'loadout', toSlot: 8, lootId: 'bone', expectedRevision: 0 };
    assert.equal((await request(app).post(`/character${path}`).send(body)).status, 401);
    process.env.INVENTORY_PROTOCOL_GATE = 'true';
    const before = await get();
    for (const header of [undefined, '1']) {
      const gated = await send('post', path, body, header);
      assert.equal(gated.status, 426); assert.equal(gated.body.error, 'CLIENT_UPGRADE_REQUIRED');
      assert.deepEqual(await get(), before);
    }
    process.env.INVENTORY_PROTOCOL_GATE = 'false';
    for (const patch of [{ fromList: 'other' }, { toList: 'other' }, { fromSlot: -1 }, { toSlot: 0.5 }, { fromSlot: '9' }, { lootId: '' }, { expectedRevision: -1 }]) {
      await rejected('post', path, { ...body, ...patch }, 400, 'VALIDATION_FAILED');
    }
    await Character.updateOne({ accountId }, { $set: { 'inventory.inventorySchemaVersion': 1 } });
    await rejected('post', path, body, 409, 'INVENTORY_MIGRATION_REQUIRED');
    await Character.updateOne({ accountId }, { $set: { 'inventory.inventorySchemaVersion': 2 } });
    const swapped = await send('post', path, body);
    assert.equal(swapped.status, 200, JSON.stringify(swapped.body));
    assert.deepEqual(swapped.body.stash, [entry(3, 'bone', 8), entry(9, 'healthpotion', 2)]);
    assert.deepEqual(swapped.body.loadout, [entry(8, 'bone', 4)]);
    await rejected('post', path, body, 409, 'REVISION_CONFLICT');
    const merged = await send('post', path, { ...body, fromSlot: 3, expectedRevision: 1 });
    assert.equal(merged.status, 200, JSON.stringify(merged.body));
    assert.deepEqual(merged.body.stash, [entry(9, 'healthpotion', 2)]);
    assert.deepEqual(merged.body.loadout, [entry(8, 'bone', 12)]);
    assert.equal((await get()).revision, 2);
  });
  await t.test('sell and buyback keep line DTO slot-free; capacity failure creates no receipt or debit', async () => {
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': [entry(12, 'placeholder_helmet', 2), entry(4, 'placeholder_helmet', 3)] } });
    const body = { lootId: 'placeholder_helmet', amount: 2, sourceSlot: 12, transactionId: 'sale', expectedRevision: 0 };
    const sale = await send('post', '/me/inventory/shop/sell', body);
    assert.equal(sale.status, 200, JSON.stringify(sale.body));
    assert.deepEqual(sale.body.loadout, [entry(4, 'placeholder_helmet', 3)]);
    const line = sale.body.buyback.entries[0];
    assert.deepEqual(Object.keys(line).sort(), ['amount', 'entryId', 'lootId', 'unitPrice']);
    assert.deepEqual({ ...line, entryId: 'id' }, { entryId: 'id', lootId: 'placeholder_helmet', amount: 2, unitPrice: 8 });
    const rebound = await rejected('post', '/me/inventory/shop/sell', { ...body, sourceSlot: 4 }, 409, 'TRANSACTION_ID_REUSED');
    assert.equal(rebound.body.error, 'TRANSACTION_ID_REUSED');
    const rebuy = { transactionId: 'rebuy', entryId: line.entryId, amount: 1, expectedRevision: 1 };
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': full(30) } });
    await rejected('post', '/me/inventory/shop/buyback', rebuy, 409, 'INVENTORY_FULL');
    await rejected('post', '/me/inventory/shop/buy', { transactionId: 'buy', lootId: 'healthpotion', amount: 1, expectedRevision: 1 }, 409, 'INVENTORY_FULL');
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': [entry(8, line.lootId, 2), entry(3, line.lootId, 1)] } });
    const bought = await send('post', '/me/inventory/shop/buyback', rebuy);
    assert.equal(bought.status, 200, JSON.stringify(bought.body));
    assert.deepEqual(bought.body.loadout, [entry(3, line.lootId, 2), entry(8, line.lootId, 2)]);
    assert.equal(bought.body.currency, 502); assert.equal(bought.body.buyback.entries[0].amount, 1);
    const replay = await send('post', '/me/inventory/shop/buyback', rebuy);
    assert.equal(replay.body.alreadySecured, true); assert.equal(replay.body.currency, 502);
    const boughtAll = await send('post', '/me/inventory/shop/buyback', { ...rebuy, amount: 1, transactionId: 'rest', expectedRevision: 2 });
    assert.equal(boughtAll.status, 200); assert.deepEqual(boughtAll.body.buyback.entries, []);
    assert.equal(boughtAll.body.currency, 494);
  });
  await t.test('reservation exact snapshot and rollback collision survive Mongo persistence; admission aggregates', async () => {
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': [entry(9, 'bone', 2), entry(3, 'bone', 4)] } });
    const reserved = await send('post', '/me/inventory/reservation', { reservationId: 'res' });
    assert.equal(reserved.status, 201, JSON.stringify(reserved.body));
    assert.deepEqual(reserved.body.pendingReservation.items, [entry(3, 'bone', 4), entry(9, 'bone', 2)]);
    const ticket = await send('post', '/me/raid/admission-ticket', { raidCode: 'ABC123', reservationId: 'res' });
    assert.equal(ticket.status, 200, JSON.stringify(ticket.body));
    assert.deepEqual(ticket.body.snapshot.items, [{ lootId: 'bone', amount: 6 }]);
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': full(30) } });
    const restored = await send('delete', '/me/inventory/reservation?rollback=true');
    assert.equal(restored.status, 200, JSON.stringify(restored.body));
    assert.deepEqual((await get()).loadout, [...full(30), entry(30, 'bone', 4), entry(31, 'bone', 2)]);
  });
  await t.test('revision race rejects slot move without partial ownership mutation', async () => {
    const original = Character.findOneAndUpdate;
    Character.findOneAndUpdate = async function(filter, update, options) {
      await Character.updateOne({ accountId }, { $inc: { revision: 1 } });
      return original.call(this, filter, update, options);
    };
    const before = await get();
    try {
      const response = await send('post', '/me/inventory/slots/move', { fromList: 'stash', fromSlot: 9, toList: 'loadout', toSlot: 0, lootId: 'bone', expectedRevision: 0 });
      assert.equal(response.status, 409); assert.equal(response.body.error, 'REVISION_CONFLICT');
      assert.deepEqual(await get(), { ...before, revision: 1 });
    } finally { Character.findOneAndUpdate = original; }
  });
  await t.test('v2 unified and legacy extraction allocate slots; defensive overflow leaves authoritative result unclaimed', async () => {
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': [] } });
    const payload = { raidId: 'raid', resultSequence: 1 };
    const items = [{ lootId: 'bone', amount: 2 }, { lootId: 'bone', amount: 4 }, { lootId: 'healthpotion', amount: 1 }];
    await Result.create({ accountId, raidId: 'raid', resultId: 'result', items, experienceGranted: 0 });
    const result = await send('post', '/me/extraction/commit', payload);
    assert.equal(result.status, 201, JSON.stringify(result.body));
    assert.deepEqual(result.body.loadout, [entry(0, 'bone', 6), entry(1, 'healthpotion')]);
    assert.equal((await send('post', '/me/extraction/commit', payload)).body.alreadySecured, true);
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': [] } });
    const legacy = await send('post', '/me/inventory/extraction', { ...payload, raidId: 'legacy', items });
    assert.equal(legacy.status, 200, JSON.stringify(legacy.body));
    assert.deepEqual(legacy.body.loadout, [entry(0, 'bone', 6), entry(1, 'healthpotion')]);
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': [] } });
    await Result.create({ accountId, raidId: 'overflow', resultId: 'overflow', items: full(31).map(({ lootId, amount }) => ({ lootId, amount })), experienceGranted: 0 });
    await rejected('post', '/me/extraction/commit', { ...payload, raidId: 'overflow' }, 409, 'INVENTORY_FULL');
    assert.equal((await Result.findOne({ raidId: 'overflow' })).consumed, false);
  });
});

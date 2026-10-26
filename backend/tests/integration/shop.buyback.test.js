'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const request = require('supertest');
const app = require('../../src/app');
const testDb = require('./testDb');
const Character = require('../../src/models/Character');
const InventoryService = require('../../src/services/InventoryService');
const SessionTicketService = require('../../src/services/SessionTicketService');

test('merchant buyback atomic persistence and HTTP contract', async t => {
  await testDb.connect();
  t.after(async () => testDb.closeDatabase());
  const registered = await request(app).post('/auth/register').send({ username: 'buyback123', password: 'password123' });
  const token = registered.body.token;
  const created = await request(app).post('/character/me').set('Authorization', `Bearer ${token}`).send({ name: 'Seller' });
  assert.equal(created.status, 201);
  const character = await Character.findOne({ name: 'Seller' });
  const accountId = character.accountId;
  const post = (operation, body) => request(app).post(`/character/me/inventory/shop/${operation}`).set('Authorization', `Bearer ${token}`).send(body);
  const reset = async () => Character.updateOne({ accountId }, { $set: {
    revision: 0, appliedShopReceipts: [], 'inventory.currency': 100,
    'inventory.pendingReservation': null, 'inventory.loadout': [{ lootId: 'placeholder_helmet', amount: 30 }],
    'inventory.buyback': { sessionEpoch: 'epoch', entries: [] }
  } });
  await t.test('eligible sale freezes ceil price; partial and full rebuy merge loadout and replay once', async () => {
    await reset();
    const sale = await post('sell', { transactionId: 'sale', lootId: 'placeholder_helmet', amount: 2, expectedRevision: 0 });
    assert.equal(sale.status, 200);
    const entry = sale.body.buyback.entries[0];
    assert.equal(entry.unitPrice, 8);
    assert.equal(entry.amount, 2);
    const replaySale = await post('sell', { transactionId: 'sale', lootId: 'placeholder_helmet', amount: 2, expectedRevision: 0 });
    assert.equal(replaySale.body.buyback.entries.length, 1);
    const body = { transactionId: 'rebuy', entryId: entry.entryId, amount: 1, expectedRevision: 1 };
    const buy = await post('buyback', body);
    assert.equal(buy.status, 200);
    assert.equal(buy.body.currency, 102);
    assert.equal(buy.body.loadout.find(i => i.lootId === entry.lootId).amount, 29);
    assert.equal(buy.body.buyback.entries[0].amount, 1);
    const replay = await post('buyback', body);
    assert.equal(replay.body.alreadySecured, true);
    assert.equal(replay.body.currency, 102);
    const full = await post('buyback', { ...body, transactionId: 'rest', expectedRevision: 2 });
    assert.equal(full.body.currency, 94);
    assert.deepEqual(full.body.buyback.entries, []);
    const saved = await Character.findOne({ accountId });
    assert.equal(saved.appliedShopReceipts.at(-1).type, 'buyback');
    const snapshot = await request(app).get('/character/me/inventory').set('Authorization', `Bearer ${token}`);
    assert.deepEqual(snapshot.body.buyback, { entries: [] });
  });
  for (const lootId of ['coins', 'bone', 'healthpotion']) await t.test(`${lootId} sale has no buyback`, async () => {
    await reset();
    await Character.updateOne({ accountId }, { $set: { 'inventory.loadout': [{ lootId, amount: 1 }] } });
    const sale = await InventoryService.shopSell(accountId, lootId, 1, 0, 0, `sell-${lootId}`);
    assert.deepEqual(sale.buyback.entries, []);
  });
  await t.test('FIFO cap, account isolation and Town epoch expiry', async () => {
    await reset();
    const soldIds = [];
    for (let i = 0; i < 17; i++) {
      const sale = await InventoryService.shopSell(accountId, 'placeholder_helmet', 1, 0, i, `fifo-${i}`);
      soldIds.push(sale.buyback.entries.at(-1).entryId);
    }
    const saved = await Character.findOne({ accountId });
    assert.equal(saved.inventory.buyback.entries.length, 16);
    assert.deepEqual(saved.inventory.buyback.entries.map(e => e.entryId), soldIds.slice(1));
    const entryId = saved.inventory.buyback.entries[0].entryId;
    const other = await Character.create({ accountId: new (require('mongoose').Types.ObjectId)(), name: 'Other' });
    await assert.rejects(InventoryService.shopBuyback(other.accountId, entryId, 1, 0, 'foreign'), e => e.errorCode === 'BUYBACK_ENTRY_NOT_FOUND');
    await SessionTicketService.issueTownTicket(accountId);
    const fresh = await Character.findOne({ accountId });
    assert.notEqual(fresh.inventory.buyback.sessionEpoch, 'epoch');
    assert.equal(fresh.revision, 17);
    await assert.rejects(InventoryService.shopBuyback(accountId, entryId, 1, 17, 'expired'), e => e.errorCode === 'BUYBACK_ENTRY_NOT_FOUND');
  });
  await t.test('reservation created between read and write blocks debit atomically', async () => {
    await reset();
    const sale = await InventoryService.shopSell(accountId, 'placeholder_helmet', 1, 0, 0, 'race-sale');
    const original = Character.findOneAndUpdate;
    let reserved;
    Character.findOneAndUpdate = async function(filter, update, options) {
      await Character.updateOne({ accountId }, { $set: { 'inventory.pendingReservation': { reservationId: 'race' } } });
      reserved = await Character.findOne({ accountId }).lean();
      return original.call(this, filter, update, options);
    };
    try {
      await assert.rejects(InventoryService.shopBuyback(accountId, sale.buyback.entries[0].entryId, 1, 1, 'race'), e => e.errorCode === 'RESERVATION_PENDING');
      assert.deepEqual(await Character.findOne({ accountId }).lean(), reserved);
    } finally { Character.findOneAndUpdate = original; }
  });
  await t.test('funds, units, reservation and revision rejection leave persisted state unchanged', async () => {
    for (const [patch, amount, revision, code] of [
      [{ 'inventory.currency': 0 }, 1, 1, 'INSUFFICIENT_FUNDS'],
      [{}, 2, 1, 'INSUFFICIENT_BUYBACK_UNITS'],
      [{ 'inventory.pendingReservation': { reservationId: 'pending' } }, 1, 1, 'RESERVATION_PENDING'],
      [{}, 1, 0, 'REVISION_CONFLICT']
    ]) {
      await reset();
      const sale = await InventoryService.shopSell(accountId, 'placeholder_helmet', 1, 0, 0, 'negative-sale');
      if (Object.keys(patch).length) await Character.updateOne({ accountId }, { $set: patch });
      const before = await Character.findOne({ accountId }).lean();
      await assert.rejects(InventoryService.shopBuyback(accountId, sale.buyback.entries[0].entryId, amount, revision, 'negative'), e => e.errorCode === code);
      assert.deepEqual(await Character.findOne({ accountId }).lean(), before);
    }
  });
  await t.test('epoch reset between sale read and write cannot repopulate the new session', async () => {
    await reset();
    const original = Character.findOneAndUpdate;
    let fresh;
    Character.findOneAndUpdate = async function(filter, update, options) {
      await Character.updateOne({ accountId }, { $set: { 'inventory.buyback': { sessionEpoch: 'new-epoch', entries: [] } } });
      fresh = await Character.findOne({ accountId }).lean();
      return original.call(this, filter, update, options);
    };
    try {
      await assert.rejects(InventoryService.shopSell(accountId, 'placeholder_helmet', 1, 0, 0, 'epoch-race'), e => e.errorCode === 'REVISION_CONFLICT');
      assert.deepEqual(await Character.findOne({ accountId }).lean(), fresh);
    } finally { Character.findOneAndUpdate = original; }
  });
  await t.test('buyback HTTP validation and authentication', async () => {
    assert.equal((await request(app).post('/character/me/inventory/shop/buyback').send({})).status, 401);
    for (const patch of [{ transactionId: 'x'.repeat(129) }, { transactionId: '' }, { entryId: '' }, { amount: 0 }, { amount: 1000 }, { expectedRevision: -1 }]) {
      const response = await post('buyback', { transactionId: 'valid', entryId: 'entry', amount: 1, expectedRevision: 0, ...patch });
      assert.equal(response.status, 400);
      assert.equal(response.body.error, 'VALIDATION_FAILED');
    }
  });
});

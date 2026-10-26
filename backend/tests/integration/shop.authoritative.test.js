'use strict';

const test = require('node:test');
const assert = require('node:assert');
const request = require('supertest');
const app = require('../../src/app');
const testDb = require('./testDb');

const Account = require('../../src/models/Account');
const Character = require('../../src/models/Character');

test('E2E Shop Authoritative Validation (US-57 / SEC-57.4)', async (t) => {
  await testDb.connect();

  t.after(async () => {
    await testDb.closeDatabase();
  });

  let token;
  let accountId;
  let currentRevision = 0;

  await t.test('1. Setup Account and Character', async () => {
    const regRes = await request(app).post('/auth/register').send({ username: 'shopkeep123', password: 'password123' });
    assert.strictEqual(regRes.status, 201);
    token = regRes.body.token;

    const charRes = await request(app).post('/character/me').set('Authorization', `Bearer ${token}`).send({ name: 'Trader' });
    assert.strictEqual(charRes.status, 201);

    const account = await Account.findOne({ username: 'shopkeep123' });
    accountId = account._id;

    // Set initial currency to 500 gold
    await Character.findOneAndUpdate({ accountId }, { 'inventory.currency': 500, revision: 0 });
    currentRevision = 0;
  });

  for (const operation of ['buy', 'sell']) {
    await t.test(`${operation}: reservation blocks new transaction but permits receipt replay`, async () => {
      await Character.findOneAndUpdate({ accountId }, { $set: {
        'inventory.currency': 500,
        'inventory.loadout': [{ lootId: 'bone', amount: 2 }],
        'inventory.pendingReservation': { raidId: 'raid-shop', items: [{ lootId: 'bone', amount: 3 }] },
        appliedShopReceipts: [{ transactionId: `replay-${operation}`, type: operation, lootId: 'bone', amount: 1, totalAmount: 5 }],
        revision: 0
      } });
      try {
        const before = await Character.findOne({ accountId }).lean();
        const replay = await request(app).post(`/character/me/inventory/shop/${operation}`)
          .set('Authorization', `Bearer ${token}`)
          .send({ transactionId: `replay-${operation}`, lootId: 'bone', amount: 1, expectedRevision: 99 });
        assert.strictEqual(replay.status, 200);
        assert.strictEqual(replay.body.alreadySecured, true);
        const res = await request(app).post(`/character/me/inventory/shop/${operation}`)
          .set('Authorization', `Bearer ${token}`)
          .send({ transactionId: `blocked-${operation}`, lootId: 'bone', amount: 1, expectedRevision: 0 });
        assert.strictEqual(res.status, 409);
        assert.strictEqual(res.body.error, 'RESERVATION_PENDING');
        const after = await Character.findOne({ accountId }).lean();
        assert.deepStrictEqual(after, before, 'Reservation rejection and replay must leave all persisted state unchanged');
      } finally {
        await Character.findOneAndUpdate({ accountId }, { $set: {
          'inventory.currency': 500, 'inventory.loadout': [], 'inventory.pendingReservation': null,
          appliedShopReceipts: [], revision: 0
        } });
      }
    });
  }

  for (const operation of ['buy', 'sell']) {
    await t.test(`${operation}: reservation created between read and atomic write blocks trade`, async () => {
      await Character.findOneAndUpdate({ accountId }, { $set: {
        'inventory.currency': 500, 'inventory.loadout': [{ lootId: 'bone', amount: 2 }], revision: 0
      } });
      const originalUpdate = Character.findOneAndUpdate;
      let reservedState;
      Character.findOneAndUpdate = async function (filter, update, options) {
        // Keep revision unchanged to prove the reservation predicate itself protects the write.
        await originalUpdate.call(this, { accountId }, { $set: {
          'inventory.pendingReservation': { raidId: 'race-raid', items: [{ lootId: 'bone', amount: 3 }] }
        } });
        reservedState = await Character.findOne({ accountId }).lean();
        return originalUpdate.call(this, filter, update, options);
      };
      try {
        const res = await request(app).post(`/character/me/inventory/shop/${operation}`)
          .set('Authorization', `Bearer ${token}`)
          .send({ transactionId: `race-${operation}`, lootId: 'bone', amount: 1, expectedRevision: 0 });
        assert.strictEqual(res.status, 409);
        assert.strictEqual(res.body.error, 'RESERVATION_PENDING');
        assert.deepStrictEqual(await Character.findOne({ accountId }).lean(), reservedState);
      } finally {
        Character.findOneAndUpdate = originalUpdate;
        await Character.findOneAndUpdate({ accountId }, { $set: {
          'inventory.currency': 500, 'inventory.loadout': [], 'inventory.pendingReservation': null,
          appliedShopReceipts: [], revision: 0
        } });
      }
    });
  }

  await t.test('2. Legitimate Purchase: Deducts authoritative cost from currency and credits loadout', async () => {
    // Buy 2 bones. Server unit buy price is 5 gold -> Total cost: 10 gold.
    const res = await request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-buy-bones-1',
        lootId: 'bone',
        amount: 2,
        expectedRevision: currentRevision
      });

    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.currency, 490, '500 - (2 * 5) = 490');
    assert.strictEqual(res.body.revision, 1);
    const boneItem = res.body.loadout.find(i => i.lootId === 'bone');
    assert.ok(boneItem);
    assert.strictEqual(boneItem.amount, 2);

    currentRevision = res.body.revision;
  });

  await t.test('3. Legitimate Sale: Removes item from loadout and credits authoritative value', async () => {
    // Sell 1 bone. Server unit sell value is 5 gold -> Total revenue: 5 gold.
    const res = await request(app).post('/character/me/inventory/shop/sell')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-sell-bone-1',
        lootId: 'bone',
        amount: 1,
        expectedRevision: currentRevision
      });

    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.currency, 495, '490 + 5 = 495');
    assert.strictEqual(res.body.revision, 2);
    const boneItem = res.body.loadout.find(i => i.lootId === 'bone');
    assert.strictEqual(boneItem.amount, 1);

    currentRevision = res.body.revision;
  });

  await t.test('4. Exploit Defense: Sending declaredPrice = 0 does NOT bypass authoritative server price', async () => {
    // Attacker tries to buy 5 health potions claiming declaredPrice = 0
    // Server price is 20 gold per potion -> 5 * 20 = 100 gold
    const res = await request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-exploit-buy-zero',
        lootId: 'healthpotion',
        amount: 5,
        declaredPrice: 0, // Client tries to exploit
        expectedRevision: currentRevision
      });

    assert.strictEqual(res.status, 200);
    // Server must deduct 100 gold, NOT 0 gold!
    assert.strictEqual(res.body.currency, 395, '495 - 100 = 395');
    assert.strictEqual(res.body.revision, 3);
    const potionItem = res.body.loadout.find(i => i.lootId === 'healthpotion');
    assert.strictEqual(potionItem.amount, 5);

    currentRevision = res.body.revision;
  });

  await t.test('5. Exploit Defense: Sending declaredSellValue = 1,000,000,000 does NOT grant arbitrary gold', async () => {
    // Attacker tries to sell 1 potion claiming 1 billion gold
    // Server sell value is 10 gold
    const res = await request(app).post('/character/me/inventory/shop/sell')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-exploit-sell-billion',
        lootId: 'healthpotion',
        amount: 1,
        declaredSellValue: 1000000000, // Client tries to exploit
        expectedRevision: currentRevision
      });

    assert.strictEqual(res.status, 200);
    // Server must credit 10 gold, NOT 1 billion!
    assert.strictEqual(res.body.currency, 405, '395 + 10 = 405');
    assert.strictEqual(res.body.revision, 4);

    currentRevision = res.body.revision;
  });

  await t.test('6. Rejection: Insufficient funds rejects purchase without state mutation', async () => {
    // Set currency to 15 gold (not enough for 1 health potion costing 20)
    await Character.findOneAndUpdate({ accountId }, { 'inventory.currency': 15 });

    const res = await request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-insufficient-funds',
        lootId: 'healthpotion',
        amount: 1,
        expectedRevision: currentRevision
      });

    assert.strictEqual(res.status, 409);
    assert.strictEqual(res.body.error, 'INSUFFICIENT_FUNDS');

    // Verify DB state unmodified
    const char = await Character.findOne({ accountId });
    assert.strictEqual(char.inventory.currency, 15);
    assert.strictEqual(char.revision, currentRevision);
  });

  await t.test('7. Rejection: Selling more items than owned is rejected without state mutation', async () => {
    // Player currently has 4 health potions. Attempts to sell 10.
    const res = await request(app).post('/character/me/inventory/shop/sell')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-sell-excessive-owned',
        lootId: 'healthpotion',
        amount: 10,
        expectedRevision: currentRevision
      });

    assert.strictEqual(res.status, 409);
    assert.strictEqual(res.body.error, 'INSUFFICIENT_ITEMS');

    const char = await Character.findOne({ accountId });
    assert.strictEqual(char.revision, currentRevision);
  });

  await t.test('8. Rejection: Unlisted items cannot be bought or sold', async () => {
    const buyRes = await request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-buy-unlisted',
        lootId: 'cheat_infinite_sword',
        amount: 1,
        expectedRevision: currentRevision
      });
    assert.strictEqual(buyRes.status, 404);
    assert.strictEqual(buyRes.body.error, 'ITEM_NOT_OFFERED');

    const sellRes = await request(app).post('/character/me/inventory/shop/sell')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-sell-unlisted',
        lootId: 'quest_item_unsellable',
        amount: 1,
        expectedRevision: currentRevision
      });
    assert.strictEqual(sellRes.status, 404);
    assert.strictEqual(sellRes.body.error, 'ITEM_NOT_SELLABLE');
  });

  await t.test('9. Rejection: buy amount 1000 returns VALIDATION_FAILED without mutation', async () => {
    const before = await Character.findOne({ accountId }).lean();
    const res = await request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-overflow-amount',
        lootId: 'bone',
        amount: 1000,
        expectedRevision: currentRevision
      });
    assert.strictEqual(res.status, 400);
    assert.strictEqual(res.body.error, 'VALIDATION_FAILED');
    assert.deepStrictEqual(await Character.findOne({ accountId }).lean(), before);
  });

  await t.test('10. Idempotency: Exact retry returns alreadySecured without duplicate charge', async () => {
    // Give enough currency for 1 bone (5 gold)
    await Character.findOneAndUpdate({ accountId }, { 'inventory.currency': 100 });

    const reqData = {
      transactionId: 'tx-idempotent-repeat',
      lootId: 'bone',
      amount: 1,
      expectedRevision: currentRevision
    };

    // First attempt
    const firstRes = await request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send(reqData);
    assert.strictEqual(firstRes.status, 200);
    assert.strictEqual(firstRes.body.currency, 95);
    currentRevision = firstRes.body.revision;

    // Retry with identical transactionId and payload
    const retryRes = await request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({ ...reqData, expectedRevision: currentRevision });

    assert.strictEqual(retryRes.status, 200);
    assert.strictEqual(retryRes.body.alreadySecured, true);
    assert.strictEqual(retryRes.body.currency, 95, 'Must not charge twice');
  });

  await t.test('11. Idempotency: Reusing transactionId with different payload is rejected', async () => {
    // Attempt to reuse tx-idempotent-repeat to buy healthpotion instead of bone
    const reuseRes = await request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-idempotent-repeat',
        lootId: 'healthpotion',
        amount: 1,
        expectedRevision: currentRevision
      });

    assert.strictEqual(reuseRes.status, 409);
    assert.strictEqual(reuseRes.body.error, 'TRANSACTION_ID_REUSED');
  });

  await t.test('12. Concurrency: Two simultaneous purchases competing for limited currency', async () => {
    // Set currency to 30 gold. Health potion costs 20 gold.
    // Buying 2 potions simultaneously costs 40 gold > 30 gold.
    await Character.findOneAndUpdate({ accountId }, { 'inventory.currency': 30 });

    const reqA = request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-race-a',
        lootId: 'healthpotion',
        amount: 1,
        expectedRevision: currentRevision
      });

    const reqB = request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-race-b',
        lootId: 'healthpotion',
        amount: 1,
        expectedRevision: currentRevision
      });

    const [resA, resB] = await Promise.all([reqA, reqB]);

    const statuses = [resA.status, resB.status].sort();
    assert.deepStrictEqual(statuses, [200, 409], 'Exactly one purchase must succeed and one must fail');

    const char = await Character.findOne({ accountId });
    assert.strictEqual(char.inventory.currency, 10, '30 - 20 = 10 gold remaining');
  });
});

const test = require('node:test');
const assert = require('node:assert');
const request = require('supertest');
const app = require('../../src/app');
const testDb = require('./testDb');

const Account = require('../../src/models/Account');
const Character = require('../../src/models/Character');

test('E2E Concurrency & Idempotency (US-45)', async (t) => {
  await testDb.connect();

  t.after(async () => {
    await testDb.closeDatabase();
  });

  let token;
  let accountId;
  let currentRevision = 0;

  await t.test('Setup Account', async () => {
    const res = await request(app).post('/auth/register').send({ username: 'racer', password: 'password123' });
    assert.strictEqual(res.status, 201, 'Register should succeed');
    token = res.body.token;
    
    const charRes = await request(app).post('/character/me').set('Authorization', `Bearer ${token}`).send({ name: 'Racer' });
    assert.strictEqual(charRes.status, 201, 'Character creation should succeed');
    
    const account = await Account.findOne({ username: 'racer' });
    accountId = account._id;
  });

  await t.test('Concurrent Mutations (Stale Revisions)', async () => {
    // 1. Give character some items
    await Character.findOneAndUpdate(
      { accountId },
      { 'inventory.stash': [{ lootId: 'apple', amount: 10 }] }
    );
    currentRevision = 0; // Initial revision from creation

    // 2. Client A reads revision 0 and tries to move 2 apples
    const reqA = request(app).post('/character/me/inventory/stash/move-to-loadout')
      .set('Authorization', `Bearer ${token}`)
      .send({ lootId: 'apple', amount: 2, expectedRevision: 0 });

    // 3. Client B reads revision 0 and tries to move 3 apples concurrently
    const reqB = request(app).post('/character/me/inventory/stash/move-to-loadout')
      .set('Authorization', `Bearer ${token}`)
      .send({ lootId: 'apple', amount: 3, expectedRevision: 0 });

    const [resA, resB] = await Promise.all([reqA, reqB]);

    // One must succeed and increment revision to 1. The other must fail with 409 REVISION_CONFLICT.
    const statuses = [resA.status, resB.status].sort();
    assert.deepStrictEqual(statuses, [200, 409], 'One request should succeed, the other should fail with 409');

    // Fetch final state
    const invRes = await request(app).get('/character/me/inventory').set('Authorization', `Bearer ${token}`);
    assert.strictEqual(invRes.body.revision, 1);
    
    // Only one amount should have moved (either 2 or 3, but NOT 5)
    const loadoutAmount = invRes.body.loadout.find(i => i.lootId === 'apple').amount;
    assert.ok(loadoutAmount === 2 || loadoutAmount === 3, 'Only one transaction should have applied');
    
    currentRevision = 1;
  });

  await t.test('Duplicate Receipts (Shop Idempotency)', async () => {
    // Give currency
    await Character.findOneAndUpdate({ accountId }, { 'inventory.currency': 500 });
    
    const req1 = request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({ transactionId: 'tx-buy-1', lootId: 'health_potion', amount: 1, declaredPrice: 50, expectedRevision: currentRevision });

    const req2 = request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token}`)
      .send({ transactionId: 'tx-buy-1', lootId: 'health_potion', amount: 1, declaredPrice: 50, expectedRevision: currentRevision });

    const [res1, res2] = await Promise.all([req1, req2]);
    
    const statuses = [res1.status, res2.status].sort();
    assert.deepStrictEqual(statuses, [200, 409], 'Duplicate shop transaction should be rejected or conflict');
    
    // Currency should only decrease by 50 once
    const char = await Character.findOne({ accountId });
    assert.strictEqual(char.inventory.currency, 450);
  });

  await t.test('Duplicate Extraction Commits', async () => {
    // Clear loadout so extraction commit can succeed without 409 LOADOUT_NOT_EMPTY
    await Character.findOneAndUpdate({ accountId }, { 'inventory.loadout': [] });

    // Verify it wasn't applied twice
    // (If mock result fails with 401 or something, we'll see it in the status)
    const mockRes = await request(app).post('/character/debug/mock-fusion-result')
      .set('Authorization', `Bearer ${token}`)
      .send({ raidId: 'raid-concurrent', items: [{ lootId: 'sword', amount: 1 }], experienceGranted: 100 });
    assert.strictEqual(mockRes.status, 200, 'Mock fusion should succeed');

    const req1 = request(app).post('/character/me/extraction/commit')
      .set('Authorization', `Bearer ${token}`)
      .send({ raidId: 'raid-concurrent', resultSequence: 1 });

    const req2 = request(app).post('/character/me/extraction/commit')
      .set('Authorization', `Bearer ${token}`)
      .send({ raidId: 'raid-concurrent', resultSequence: 1 });

    const [res1, res2] = await Promise.all([req1, req2]);
    
    // One should be 201 Created (alreadySecured: false)
    // The other should be 200 OK (alreadySecured: true) or 409 Conflict
    const statuses = [res1.status, res2.status].sort();
    assert.deepStrictEqual(statuses, [200, 201], `Expected 200 and 201, got ${statuses}`);
    
    const responses = [res1.body.alreadySecured, res2.body.alreadySecured].sort();
    assert.deepStrictEqual(responses, [false, true], 'Exactly one extraction should secure the loot');
    
    // Verify it wasn't applied twice
    const char = await Character.findOne({ accountId });
    const swords = char.inventory.loadout.find(i => i.lootId === 'sword');
    assert.strictEqual(swords.amount, 1, 'Loot should not be duplicated');
  });

});

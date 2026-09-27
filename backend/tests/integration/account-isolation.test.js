const test = require('node:test');
const assert = require('node:assert');
const request = require('supertest');
const app = require('../../src/app');
const testDb = require('./testDb');

test('E2E Account Isolation (US-45)', async (t) => {
  await testDb.connect();

  t.after(async () => {
    await testDb.closeDatabase();
  });

  let tokenA;
  let tokenB;

  await t.test('Setup Two Accounts', async () => {
    const resA = await request(app).post('/auth/register').send({ username: 'userA', password: 'password123' });
    assert.strictEqual(resA.status, 201, 'Register A should succeed');
    tokenA = resA.body.token;
    const charA = await request(app).post('/character/me').set('Authorization', `Bearer ${tokenA}`).send({ name: 'User A' });
    assert.strictEqual(charA.status, 201, 'Create char A should succeed');

    const resB = await request(app).post('/auth/register').send({ username: 'userB', password: 'password123' });
    assert.strictEqual(resB.status, 201, 'Register B should succeed');
    tokenB = resB.body.token;
    const charB = await request(app).post('/character/me').set('Authorization', `Bearer ${tokenB}`).send({ name: 'User B' });
    assert.strictEqual(charB.status, 201, 'Create char B should succeed');
  });

  await t.test('User A cannot read User B inventory', async () => {
    // The route is /character/me/inventory and uses the JWT token's subject.
    // There is no endpoint to fetch another user's inventory by ID.
    // So by design, the API enforces isolation. We just verify the JWT routes to the correct character.
    
    // Give User A an item
    const mockRes = await request(app).post('/character/debug/mock-fusion-result')
      .set('Authorization', `Bearer ${tokenA}`)
      .send({ raidId: 'raid-A', items: [{ lootId: 'apple', amount: 1 }] });
    assert.strictEqual(mockRes.status, 200, 'Mock fusion should succeed');
      
    const extRes = await request(app).post('/character/me/extraction/commit')
      .set('Authorization', `Bearer ${tokenA}`)
      .send({ raidId: 'raid-A', resultSequence: 1 });
    assert.strictEqual(extRes.status, 201, 'Extraction commit should succeed');

    // Fetch A's inventory
    const invA = await request(app).get('/character/me/inventory').set('Authorization', `Bearer ${tokenA}`);
    assert.strictEqual(invA.status, 200, 'Fetch inventory A should succeed');
    assert.strictEqual(invA.body.loadout.length, 1, 'User A should have 1 item');

    // Fetch B's inventory
    const invB = await request(app).get('/character/me/inventory').set('Authorization', `Bearer ${tokenB}`);
    assert.strictEqual(invB.status, 200, 'Fetch inventory B should succeed');
    assert.strictEqual(invB.body.loadout.length, 0, 'User B should have 0 items (isolated from A)');
  });

  await t.test('No unauthorized access without valid token', async () => {
    const res = await request(app).get('/character/me/inventory');
    assert.strictEqual(res.status, 401, 'Should reject missing token');

    const resBad = await request(app).get('/character/me/inventory').set('Authorization', 'Bearer invalid.token.here');
    assert.strictEqual(resBad.status, 401, 'Should reject invalid token');
  });
});

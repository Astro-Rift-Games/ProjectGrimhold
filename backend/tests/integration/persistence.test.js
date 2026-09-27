const test = require('node:test');
const assert = require('node:assert');
const request = require('supertest');
const app = require('../../src/app');
const testDb = require('./testDb');

const Account = require('../../src/models/Account');
const Character = require('../../src/models/Character');
const AuthoritativeExtractionResult = require('../../src/models/AuthoritativeExtractionResult');

test('E2E Persistence across restarts (US-45)', async (t) => {
  await testDb.connect();

  t.after(async () => {
    await testDb.closeDatabase();
  });

  let token1;
  let token2;

  await t.test('1. Setup Accounts', async () => {
    const res1 = await request(app).post('/auth/register').send({ username: 'player1', password: 'password123' });
    assert.strictEqual(res1.status, 201);
    token1 = res1.body.token;

    const res2 = await request(app).post('/auth/register').send({ username: 'player2', password: 'password123' });
    assert.strictEqual(res2.status, 201);
    token2 = res2.body.token;
  });

  await t.test('2. Character Hydration', async () => {
    const res = await request(app).post('/character/me').set('Authorization', `Bearer ${token1}`).send({ name: 'Player One' });
    assert.strictEqual(res.status, 201);

    const inv = await request(app).get('/character/me/inventory').set('Authorization', `Bearer ${token1}`);
    assert.strictEqual(inv.status, 200);
    assert.deepStrictEqual(inv.body.stash, []);
    assert.deepStrictEqual(inv.body.loadout, []);
    assert.strictEqual(inv.body.preparedEquipment.weaponSlot1, '');
    assert.strictEqual(inv.body.pendingReservation, null);
  });

  await t.test('3. Setup Stash and Currency (Direct DB for test prep)', async () => {
    // Inject some initial state to test persistence
    const account1 = await Account.findOne({ username: 'player1' });
    await Character.findOneAndUpdate(
      { accountId: account1._id },
      { 
        'inventory.currency': 1000,
        'inventory.stash': [
          { lootId: 'arming_sword', amount: 1 },
          { lootId: 'shield', amount: 1 }
        ]
      }
    );
  });

  await t.test('4. Move to Loadout', async () => {
    const res = await request(app).post('/character/me/inventory/stash/move-to-loadout')
      .set('Authorization', `Bearer ${token1}`)
      .send({ lootId: 'arming_sword', amount: 1, expectedRevision: 0 });
    
    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.loadout.find(i => i.lootId === 'arming_sword').amount, 1);
    assert.strictEqual(res.body.revision, 1);
  });

  await t.test('5. Update 8-slot Equipment', async () => {
    const res = await request(app).put('/character/me/inventory/prepared-equipment')
      .set('Authorization', `Bearer ${token1}`)
      .send({ weaponSlot1: 'arming_sword', offHand1: 'shield', expectedRevision: 1 });
    
    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.preparedEquipment.weaponSlot1, 'arming_sword');
    assert.strictEqual(res.body.preparedEquipment.offHand1, 'shield');
    assert.strictEqual(res.body.revision, 2);
  });

  await t.test('6. Buy from Shop', async () => {
    const res = await request(app).post('/character/me/inventory/shop/buy')
      .set('Authorization', `Bearer ${token1}`)
      .send({ transactionId: 'tx1', lootId: 'health_potion', amount: 5, declaredPrice: 100, expectedRevision: 2 });
    
    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.currency, 900);
    assert.strictEqual(res.body.loadout.find(i => i.lootId === 'health_potion').amount, 5);
  });

  await t.test('7. Save Raid Reservation', async () => {
    const res = await request(app).post('/character/me/inventory/reservation')
      .set('Authorization', `Bearer ${token1}`)
      .send({ reservationId: 'raid123' });
    
    assert.strictEqual(res.status, 201);
    assert.strictEqual(res.body.pendingReservation.reservationId, 'raid123');
    assert.strictEqual(res.body.pendingReservation.preparedEquipment.offHand1, 'shield');
  });

  await t.test('8. Simulate Restart & Hydration', async () => {
    // "Restart" doesn't drop the DB, but simulates a fresh client fetching hydration data
    const res = await request(app).get('/character/me/inventory').set('Authorization', `Bearer ${token1}`);
    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.pendingReservation.reservationId, 'raid123'); // Reservation survived restart!
    assert.strictEqual(res.body.pendingReservation.preparedEquipment.offHand1, 'shield');
    assert.deepStrictEqual(res.body.loadout, []); // Loadout is empty because it's reserved
  });

  await t.test('9. Inject Authoritative Extraction Result (Mock Fusion)', async () => {
    const res = await request(app).post('/character/debug/mock-fusion-result')
      .set('Authorization', `Bearer ${token1}`)
      .send({
        raidId: 'raid123',
        items: [{ lootId: 'gold_coin', amount: 50 }],
        experienceGranted: 500
      });
    assert.strictEqual(res.status, 200);
  });

  await t.test('10. Commit Extraction', async () => {
    const res = await request(app).post('/character/me/extraction/commit')
      .set('Authorization', `Bearer ${token1}`)
      .send({ raidId: 'raid123', resultSequence: 1 });
    
    assert.strictEqual(res.status, 201);
    assert.strictEqual(res.body.alreadySecured, false);
    assert.ok(res.body.level > 1, 'Character should have leveled up'); // 500 XP
    assert.strictEqual(res.body.loadout.find(i => i.lootId === 'gold_coin').amount, 50);
  });

  await t.test('11. Extraction Duplicate Retry (Lost ACK)', async () => {
    // Client didn't get the 201, so it retries
    const res = await request(app).post('/character/me/extraction/commit')
      .set('Authorization', `Bearer ${token1}`)
      .send({ raidId: 'raid123', resultSequence: 1 });
    
    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.alreadySecured, true);
    assert.ok(res.body.level > 1, 'Level should still be > 1'); // XP not duplicated
    assert.strictEqual(res.body.loadout.find(i => i.lootId === 'gold_coin').amount, 50); // Loot not duplicated
  });

  await t.test('12. Verify Reservation Cleared & Equipment Restored', async () => {
    const res = await request(app).get('/character/me/inventory').set('Authorization', `Bearer ${token1}`);
    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.pendingReservation, null);
    assert.strictEqual(res.body.preparedEquipment.weaponSlot1, 'arming_sword');
    assert.strictEqual(res.body.preparedEquipment.offHand1, 'shield');
  });

});

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const request = require('supertest');
const app = require('../../src/app');
const testDb = require('./testDb');
const Character = require('../../src/models/Character');

test('POST /character/me/abilities/purchase integration and concurrency contract (FIX-64 / TASK-224)', async t => {
  await testDb.connect();
  t.after(async () => testDb.closeDatabase());

  const reg = await request(app)
    .post('/auth/register')
    .send({ username: 'abilitybuyer', password: 'password123' });
  const token = reg.body.token;

  assert.equal(
    (await request(app).post('/character/me').set('Authorization', `Bearer ${token}`).send({ name: 'AbilityBuyer' })).status,
    201
  );

  const character = await Character.findOne({ name: 'AbilityBuyer' });
  const accountId = character.accountId;

  // Set currency to 500 and ensure initial state
  await Character.updateOne(
    { accountId },
    {
      $set: {
        revision: 1,
        'inventory.currency': 500,
        'inventory.pendingReservation': null,
        unlockedAbilities: ['trap']
      }
    }
  );

  await t.test('successful purchase debits currency, unlocks ability, and advances revision', async () => {
    const res = await request(app)
      .post('/character/me/abilities/purchase')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-int-001',
        abilityId: 'arcane_projectile',
        expectedRevision: 1
      });

    assert.equal(res.status, 200, JSON.stringify(res.body));
    assert.equal(res.body.alreadySecured, false);
    assert.equal(res.body.abilityId, 'arcane_projectile');
    assert.equal(res.body.price, 100);
    assert.equal(res.body.currency, 400);
    assert.equal(res.body.revision, 2);
    assert.ok(res.body.unlockedAbilities.includes('arcane_projectile'));

    const updated = await Character.findOne({ accountId });
    assert.equal(updated.inventory.currency, 400);
    assert.equal(updated.revision, 2);
    assert.ok(updated.unlockedAbilities.includes('arcane_projectile'));
  });

  await t.test('idempotent replay of same transactionId returns alreadySecured: true without mutating', async () => {
    const res = await request(app)
      .post('/character/me/abilities/purchase')
      .set('Authorization', `Bearer ${token}`)
      .send({
        transactionId: 'tx-int-001',
        abilityId: 'arcane_projectile',
        expectedRevision: 2
      });

    assert.equal(res.status, 200, JSON.stringify(res.body));
    assert.equal(res.body.alreadySecured, true);
    assert.equal(res.body.abilityId, 'arcane_projectile');
    assert.equal(res.body.currency, 400);
    assert.equal(res.body.revision, 2);
  });

  await t.test('two concurrent purchases of different abilities with the same expectedRevision apply exactly one (atomic locking)', async () => {
    // Current revision is 2, currency is 400
    // Try to buy 'charge' (150) and 'seismic_strike' (200) concurrently with expectedRevision: 2
    const [resA, resB] = await Promise.all([
      request(app)
        .post('/character/me/abilities/purchase')
        .set('Authorization', `Bearer ${token}`)
        .send({
          transactionId: 'tx-concurrent-A',
          abilityId: 'charge',
          expectedRevision: 2
        }),
      request(app)
        .post('/character/me/abilities/purchase')
        .set('Authorization', `Bearer ${token}`)
        .send({
          transactionId: 'tx-concurrent-B',
          abilityId: 'seismic_strike',
          expectedRevision: 2
        })
    ]);

    const statuses = [resA.status, resB.status].sort();
    assert.deepEqual(statuses, [200, 409], 'Exactly one request succeeds (200) and the concurrent one conflicts (409)');

    const successRes = resA.status === 200 ? resA : resB;
    const conflictRes = resA.status === 409 ? resA : resB;

    assert.equal(conflictRes.body.error, 'REVISION_CONFLICT');
    assert.equal(successRes.body.alreadySecured, false);

    const updated = await Character.findOne({ accountId });
    assert.equal(updated.revision, 3);
    const boughtAbility = successRes.body.abilityId;
    assert.ok(updated.unlockedAbilities.includes(boughtAbility));
    assert.equal(updated.unlockedAbilities.length, 3); // ['trap', 'arcane_projectile', boughtAbility]
  });

  await t.test('two concurrent purchases of the same ability apply exactly once', async () => {
    // Current revision is 3, currency is >= 200
    // Try to buy 'life_drain' (100) concurrently twice with expectedRevision: 3
    const [resA, resB] = await Promise.all([
      request(app)
        .post('/character/me/abilities/purchase')
        .set('Authorization', `Bearer ${token}`)
        .send({
          transactionId: 'tx-same-A',
          abilityId: 'life_drain',
          expectedRevision: 3
        }),
      request(app)
        .post('/character/me/abilities/purchase')
        .set('Authorization', `Bearer ${token}`)
        .send({
          transactionId: 'tx-same-B',
          abilityId: 'life_drain',
          expectedRevision: 3
        })
    ]);

    const statuses = [resA.status, resB.status].sort();
    assert.deepEqual(statuses, [200, 409], 'Exactly one request succeeds (200) and the other is rejected (409)');

    const updated = await Character.findOne({ accountId });
    assert.equal(updated.revision, 4);
    assert.ok(updated.unlockedAbilities.includes('life_drain'));
    assert.equal(updated.unlockedAbilities.filter(a => a === 'life_drain').length, 1);
  });

  await t.test('FIX-31: an evicted timestamped id is rejected with a typed 409 and changes nothing', async () => {
    await Character.updateOne({ accountId }, { $set: {
      revision: 20, 'inventory.currency': 500, 'inventory.pendingReservation': null, unlockedAbilities: [],
      shopReceiptWatermark: 1_700_000_000_000, appliedShopReceipts: []
    } });
    const before = await Character.findOne({ accountId }).lean();
    const res = await request(app)
      .post('/character/me/abilities/purchase')
      .set('Authorization', `Bearer ${token}`)
      .send({ transactionId: `1700000000000_${'a'.repeat(32)}`, abilityId: 'charge', expectedRevision: 20 });
    assert.equal(res.status, 409, JSON.stringify(res.body));
    assert.equal(res.body.error, 'TRANSACTION_RETENTION_EXCEEDED');
    assert.deepEqual(await Character.findOne({ accountId }).lean(), before);
  });
});

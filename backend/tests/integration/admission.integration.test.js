// tests/integration/admission.integration.test.js
const test = require('node:test');
const assert = require('node:assert');
const request = require('supertest');
const app = require('../../src/app');
const testDb = require('./testDb');
const Account = require('../../src/models/Account');
const Character = require('../../src/models/Character');

test('E2E Town Identity and Raid Admission Integration (US-59 / SEC-59.1)', async (t) => {
  await testDb.connect();

  t.after(async () => {
    await testDb.closeDatabase();
  });

  let token;
  let characterId;
  let townTicket;
  let raidTicket;
  const testReservationId = 'res_integ_98765';
  const testRaidCode = '456789';

  await t.test('1. Register and Create Character', async () => {
    const regRes = await request(app)
      .post('/auth/register')
      .send({ username: 'admissionHero', password: 'password123' });
    assert.strictEqual(regRes.status, 201);
    token = regRes.body.token;

    const charRes = await request(app)
      .post('/character/me')
      .set('Authorization', `Bearer ${token}`)
      .send({ name: 'SirAdmission' });
    assert.strictEqual(charRes.status, 201);
    characterId = charRes.body.characterId;
  });

  await t.test('2. Issue and Verify Town Session Ticket', async () => {
    const issueRes = await request(app)
      .post('/character/me/session-ticket/town')
      .set('Authorization', `Bearer ${token}`);
    assert.strictEqual(issueRes.status, 200);
    assert.ok(issueRes.body.ticket);
    assert.strictEqual(issueRes.body.characterId, characterId);
    assert.strictEqual(issueRes.body.characterName, 'SirAdmission');
    townTicket = issueRes.body.ticket;

    const verifyRes = await request(app)
      .post('/character/verify-session-ticket')
      .set('Authorization', `Bearer ${token}`)
      .send({ ticket: townTicket });
    assert.strictEqual(verifyRes.status, 200);
    assert.strictEqual(verifyRes.body.valid, true);
    assert.strictEqual(verifyRes.body.characterId, characterId);
  });

  await t.test('3. Reject Tampered Town Session Ticket', async () => {
    const badTicket = townTicket.slice(0, -6) + 'abcdef';
    const verifyRes = await request(app)
      .post('/character/verify-session-ticket')
      .set('Authorization', `Bearer ${token}`)
      .send({ ticket: badTicket });
    assert.strictEqual(verifyRes.status, 401);
    assert.strictEqual(verifyRes.body.valid, false);
  });

  await t.test('4. Setup Loadout & Create Raid Reservation', async () => {
    const account = await Account.findOne({ username: 'admissionHero' });
    await Character.findOneAndUpdate(
      { accountId: account._id },
      {
        'inventory.loadout': [
          { lootId: 'iron_shield', amount: 1 },
          { lootId: 'health_potion', amount: 2 }
        ],
        'inventory.preparedEquipment': {
          weaponSlot1: 'iron_shield'
        }
      }
    );

    const resRes = await request(app)
      .post('/character/me/inventory/reservation')
      .set('Authorization', `Bearer ${token}`)
      .send({ reservationId: testReservationId });
    assert.strictEqual(resRes.status, 201);
  });

  await t.test('5. Issue and Verify Authoritative Raid Admission Ticket', async () => {
    const issueRes = await request(app)
      .post('/character/me/raid/admission-ticket')
      .set('Authorization', `Bearer ${token}`)
      .send({ raidCode: testRaidCode, reservationId: testReservationId });
    assert.strictEqual(issueRes.status, 200);
    assert.ok(issueRes.body.ticket);
    assert.strictEqual(issueRes.body.characterId, characterId);
    assert.strictEqual(issueRes.body.snapshot.items.length, 2);
    assert.strictEqual(issueRes.body.snapshot.preparedEquipment.weaponSlot1, 'iron_shield');
    raidTicket = issueRes.body.ticket;

    const verifyRes = await request(app)
      .post('/character/raid/verify-admission')
      .set('Authorization', `Bearer ${token}`)
      .send({ ticket: raidTicket, raidCode: testRaidCode });
    assert.strictEqual(verifyRes.status, 200);
    assert.strictEqual(verifyRes.body.valid, true);
    assert.strictEqual(verifyRes.body.reservationId, testReservationId);
    assert.strictEqual(verifyRes.body.snapshot.items.length, 2);
  });

  await t.test('6. Reject Raid Admission Ticket on Mismatched RaidCode', async () => {
    const verifyRes = await request(app)
      .post('/character/raid/verify-admission')
      .set('Authorization', `Bearer ${token}`)
      .send({ ticket: raidTicket, raidCode: '000000' });
    assert.strictEqual(verifyRes.status, 401);
    assert.strictEqual(verifyRes.body.valid, false);
    assert.strictEqual(verifyRes.body.error, 'RAID_CODE_MISMATCH');
  });

  await t.test('7. Reject Admission Ticket Issuance without Active Reservation', async () => {
    const badRes = await request(app)
      .post('/character/me/raid/admission-ticket')
      .set('Authorization', `Bearer ${token}`)
      .send({ raidCode: testRaidCode, reservationId: 'non_existent_res' });
    assert.strictEqual(badRes.status, 422);
    assert.strictEqual(badRes.body.error, 'NO_ACTIVE_RESERVATION');
  });
});

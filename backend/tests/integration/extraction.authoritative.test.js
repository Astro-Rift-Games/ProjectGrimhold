'use strict';

const test = require('node:test');
const assert = require('node:assert');
const crypto = require('crypto');
const request = require('supertest');
const app = require('../../src/app');
const env = require('../../src/config/env');
const testDb = require('./testDb');

const Account = require('../../src/models/Account');
const Character = require('../../src/models/Character');
const AuthoritativeExtractionResult = require('../../src/models/AuthoritativeExtractionResult');
const { buildCanonicalResultString } = require('../../src/utils/raidResultCanonicalizer');

test('E2E Authoritative Extraction Result & Anti-Replay Validation (US-58 / SEC-58.4)', async (t) => {
  await testDb.connect();

  t.after(async () => {
    await testDb.closeDatabase();
  });

  let hostToken;
  let hostAccountId;
  let playerToken;
  let playerAccountId;
  let playerCharacterId;

  const raidId = 'raid_forest_alpha_01';
  const resultSequence = 1;

  function signPayload(p) {
    const targetIdentifier = p.targetAccountId || p.accountId || p.targetCharacterId || '';
    const effectiveResultId = p.resultId || (p.resultSequence !== undefined ? `${p.raidId}:${targetIdentifier}:${p.resultSequence}` : `${p.raidId}:${targetIdentifier}`);
    const canonical = buildCanonicalResultString({
      ...p,
      resultId: effectiveResultId
    });
    return crypto.createHmac('sha256', env.webhookSecret).update(canonical).digest('hex');
  }

  await t.test('1. Setup Host and Player Accounts & Characters', async () => {
    // 1a. Register Host
    const hostReg = await request(app).post('/auth/register').send({ username: 'hostUser1', password: 'password123' });
    assert.strictEqual(hostReg.status, 201);
    hostToken = hostReg.body.token;

    const hostChar = await request(app).post('/character/me').set('Authorization', `Bearer ${hostToken}`).send({ name: 'RaidHost' });
    assert.strictEqual(hostChar.status, 201);

    const hostAccDoc = await Account.findOne({ username: 'hostUser1' });
    hostAccountId = hostAccDoc._id.toString();

    // 1b. Register Player
    const playerReg = await request(app).post('/auth/register').send({ username: 'playerUser1', password: 'password123' });
    assert.strictEqual(playerReg.status, 201);
    playerToken = playerReg.body.token;

    const playerChar = await request(app).post('/character/me').set('Authorization', `Bearer ${playerToken}`).send({ name: 'Adventurer' });
    assert.strictEqual(playerChar.status, 201);
    playerCharacterId = playerChar.body.characterId;

    const playerAccDoc = await Account.findOne({ username: 'playerUser1' });
    playerAccountId = playerAccDoc._id.toString();

    // Initial character state: 0 XP, level 1, empty loadout
    const char = await Character.findOne({ accountId: playerAccountId });
    assert.strictEqual(char.experience, 0);
    assert.strictEqual(char.level, 1);
    assert.strictEqual(char.inventory.loadout.length, 0);
  });

  await t.test('2. Exploit Defense: Tampered Signature, Modified Loot or Inflated XP are Rejected (401)', async () => {
    const validPayload = {
      raidId,
      resultSequence,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'iron_ore', amount: 5 }],
      preparedEquipment: { helmet: 'helm_iron' },
      experienceGranted: 100,
      issuedAt: new Date().toISOString()
    };
    const validSig = signPayload(validPayload);

    // 2a. Random fake signature
    const fakeSigRes = await request(app)
      .post('/character/raid/extraction-result')
      .set('Authorization', `Bearer ${hostToken}`)
      .send({ ...validPayload, hostSignature: 'deadbeef00112233445566778899aabbccddeeff' });
    assert.strictEqual(fakeSigRes.status, 401);
    assert.strictEqual(fakeSigRes.body.error, 'INVALID_HOST_SIGNATURE');

    // 2b. Tampering loot amount (signed for 5, sending 999)
    const tamperedLootRes = await request(app)
      .post('/character/raid/extraction-result')
      .set('Authorization', `Bearer ${hostToken}`)
      .send({ ...validPayload, items: [{ lootId: 'iron_ore', amount: 999 }], hostSignature: validSig });
    assert.strictEqual(tamperedLootRes.status, 401);
    assert.strictEqual(tamperedLootRes.body.error, 'INVALID_HOST_SIGNATURE');

    // 2c. Tampering XP (signed for 100, sending 50000)
    const tamperedXpRes = await request(app)
      .post('/character/raid/extraction-result')
      .set('Authorization', `Bearer ${hostToken}`)
      .send({ ...validPayload, experienceGranted: 50000, hostSignature: validSig });
    assert.strictEqual(tamperedXpRes.status, 401);
    assert.strictEqual(tamperedXpRes.body.error, 'INVALID_HOST_SIGNATURE');

    // 2d. Tampering recipient account
    const tamperedAccRes = await request(app)
      .post('/character/raid/extraction-result')
      .set('Authorization', `Bearer ${hostToken}`)
      .send({ ...validPayload, targetAccountId: hostAccountId, hostSignature: validSig });
    assert.strictEqual(tamperedAccRes.status, 401);
    assert.strictEqual(tamperedAccRes.body.error, 'INVALID_HOST_SIGNATURE');

    // Verify DB remains clean: NO AuthoritativeExtractionResult was stored
    const existing = await AuthoritativeExtractionResult.findOne({ raidId });
    assert.strictEqual(existing, null);
  });

  await t.test('3. Legitimate Host Publication Creates AuthoritativeExtractionResult', async () => {
    const payload = {
      raidId,
      resultSequence,
      targetAccountId: playerAccountId,
      items: [
        { lootId: 'iron_ore', amount: 5 },
        { lootId: 'potion_heal', amount: 2 }
      ],
      preparedEquipment: {
        helmet: 'helm_iron',
        armor: 'armor_leather',
        weaponSlot1: 'sword_basic'
      },
      experienceGranted: 50,
      issuedAt: new Date().toISOString()
    };
    payload.hostSignature = signPayload(payload);

    const res = await request(app)
      .post('/character/raid/extraction-result')
      .set('Authorization', `Bearer ${hostToken}`)
      .send(payload);

    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.status, 'published');
    assert.strictEqual(res.body.alreadyPublished, false);
    assert.strictEqual(res.body.accountId, playerAccountId);

    // Verify DB state
    const doc = await AuthoritativeExtractionResult.findOne({ raidId, accountId: playerAccountId });
    assert.ok(doc);
    assert.strictEqual(doc.consumed, false);
    assert.strictEqual(doc.experienceGranted, 50);
    assert.strictEqual(doc.items.length, 2);
  });

  await t.test('4. Republication Conflict: Changing Payload for Same Result is Rejected (409)', async () => {
    // Attempt to re-publish with different items for same resultId
    const conflictingPayload = {
      raidId,
      resultSequence,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'gold_bar', amount: 100 }],
      experienceGranted: 9999,
      issuedAt: new Date().toISOString()
    };
    conflictingPayload.hostSignature = signPayload(conflictingPayload);

    const res = await request(app)
      .post('/character/raid/extraction-result')
      .set('Authorization', `Bearer ${hostToken}`)
      .send(conflictingPayload);

    assert.strictEqual(res.status, 409);
    assert.strictEqual(res.body.error, 'RESULT_CONFLICT');
  });

  await t.test('5. Idempotent Republication: Exact Payload Returns 200 (alreadyPublished: true)', async () => {
    const existing = await AuthoritativeExtractionResult.findOne({ raidId, accountId: playerAccountId });
    const exactPayload = {
      raidId,
      resultSequence,
      targetAccountId: playerAccountId,
      items: existing.items.map(i => ({ lootId: i.lootId, amount: i.amount })),
      preparedEquipment: {
        helmet: 'helm_iron',
        armor: 'armor_leather',
        weaponSlot1: 'sword_basic'
      },
      experienceGranted: existing.experienceGranted,
      issuedAt: existing.issuedAt.toISOString()
    };
    exactPayload.hostSignature = signPayload(exactPayload);

    const res = await request(app)
      .post('/character/raid/extraction-result')
      .set('Authorization', `Bearer ${hostToken}`)
      .send(exactPayload);

    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.body.status, 'published');
    assert.strictEqual(res.body.alreadyPublished, true);
  });

  await t.test('6. Legitimate Player Extraction Commit Consumes Authoritative Result & Awards Rewards', async () => {
    const commitRes = await request(app)
      .post('/character/me/extraction/commit')
      .set('Authorization', `Bearer ${playerToken}`)
      .send({
        raidId,
        resultSequence
      });

    assert.strictEqual(commitRes.status, 201);
    assert.strictEqual(commitRes.body.alreadySecured, false);
    assert.strictEqual(commitRes.body.experience, 50);

    // Verify DB states:
    // 6a. AuthoritativeExtractionResult is locked and marked consumed
    const doc = await AuthoritativeExtractionResult.findOne({ raidId, accountId: playerAccountId });
    assert.strictEqual(doc.consumed, true);
    assert.ok(doc.consumedAt instanceof Date);

    // 6b. Character has 50 XP
    const char = await Character.findOne({ accountId: playerAccountId });
    assert.strictEqual(char.experience, 50);

    // 6c. Character loadout has the 2 authoritative items
    const iron = char.inventory.loadout.find(i => i.lootId === 'iron_ore');
    const pot = char.inventory.loadout.find(i => i.lootId === 'potion_heal');
    assert.ok(iron && iron.amount === 5);
    assert.ok(pot && pot.amount === 2);

    // 6d. Equipment is restored
    assert.strictEqual(char.inventory.preparedEquipment.helmet, 'helm_iron');
    assert.strictEqual(char.inventory.preparedEquipment.armor, 'armor_leather');
    assert.strictEqual(char.inventory.preparedEquipment.weaponSlot1, 'sword_basic');

    // 6e. Extraction receipt is recorded
    assert.strictEqual(char.inventory.appliedExtractionReceipts.length, 1);
    assert.strictEqual(char.inventory.appliedExtractionReceipts[0].raidId, raidId);
    assert.strictEqual(char.inventory.appliedExtractionReceipts[0].resultSequence, 1);
  });

  await t.test('7. Republication Defense: Already Consumed Result Cannot be Overwritten (409)', async () => {
    const payload = {
      raidId,
      resultSequence,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'iron_ore', amount: 5 }],
      experienceGranted: 50,
      issuedAt: new Date().toISOString()
    };
    payload.hostSignature = signPayload(payload);

    const res = await request(app)
      .post('/character/raid/extraction-result')
      .set('Authorization', `Bearer ${hostToken}`)
      .send(payload);

    assert.strictEqual(res.status, 409);
    assert.strictEqual(res.body.error, 'RESULT_ALREADY_CONSUMED');
  });

  await t.test('8. Multi-Sequence Replay Exploit Defense: Emptying Loadout and Claiming sequence 2 is REJECTED (409)', async () => {
    // 8a. Empty player loadout directly in DB to simulate moving everything to stash
    await Character.findOneAndUpdate(
      { accountId: playerAccountId },
      { $set: { 'inventory.loadout': [] } }
    );

    // Verify loadout is indeed empty
    const charBefore = await Character.findOne({ accountId: playerAccountId });
    assert.strictEqual(charBefore.inventory.loadout.length, 0);
    assert.strictEqual(charBefore.experience, 50);

    // 8b. Player tries to claim the same raid with sequence 2
    const replayRes = await request(app)
      .post('/character/me/extraction/commit')
      .set('Authorization', `Bearer ${playerToken}`)
      .send({
        raidId,
        resultSequence: 2 // Trying to double dip!
      });

    assert.strictEqual(replayRes.status, 409);
    assert.strictEqual(replayRes.body.error, 'RESULT_ALREADY_CONSUMED');

    // 8c. Verify progression and loot were NOT duplicated
    const charAfter = await Character.findOne({ accountId: playerAccountId });
    assert.strictEqual(charAfter.experience, 50, 'XP must NOT increase');
    assert.strictEqual(charAfter.inventory.loadout.length, 0, 'Loadout must remain empty');
  });

  await t.test('9. Idempotency: Exact Retry of (raidId, resultSequence = 1) Returns 200 (alreadySecured: true)', async () => {
    const retryRes = await request(app)
      .post('/character/me/extraction/commit')
      .set('Authorization', `Bearer ${playerToken}`)
      .send({
        raidId,
        resultSequence: 1 // Legitimate retry of original receipt
      });

    assert.strictEqual(retryRes.status, 200);
    assert.strictEqual(retryRes.body.alreadySecured, true);

    const char = await Character.findOne({ accountId: playerAccountId });
    assert.strictEqual(char.experience, 50, 'XP remains 50');
  });

  await t.test('10. Concurrency Defense: Simultaneous Commits for Fresh Authoritative Result', async () => {
    // Setup second raid
    const raidId2 = 'raid_forest_beta_02';
    const payload2 = {
      raidId: raidId2,
      resultSequence: 1,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'gold_coin', amount: 100 }],
      experienceGranted: 25,
      issuedAt: new Date().toISOString()
    };
    payload2.hostSignature = signPayload(payload2);

    const pubRes = await request(app)
      .post('/character/raid/extraction-result')
      .set('Authorization', `Bearer ${hostToken}`)
      .send(payload2);
    assert.strictEqual(pubRes.status, 200);

    // Dispatch 2 simultaneous commits for raidId2
    const [resA, resB] = await Promise.all([
      request(app).post('/character/me/extraction/commit').set('Authorization', `Bearer ${playerToken}`).send({ raidId: raidId2, resultSequence: 1 }),
      request(app).post('/character/me/extraction/commit').set('Authorization', `Bearer ${playerToken}`).send({ raidId: raidId2, resultSequence: 1 })
    ]);

    const statuses = [resA.status, resB.status].sort();
    assert.strictEqual(statuses[0], 201, 'Exactly one concurrent commit must succeed with 201');
    assert.ok(statuses[1] === 200 || statuses[1] === 409, 'The other request must be 200 or 409');

    const finalDoc = await AuthoritativeExtractionResult.findOne({ raidId: raidId2, accountId: playerAccountId });
    assert.strictEqual(finalDoc.consumed, true);

    const finalChar = await Character.findOne({ accountId: playerAccountId });
    // Started with 50 XP, gained exactly 25 XP -> 75 XP (not 50 + 25 + 25 = 100 XP)
    assert.strictEqual(finalChar.experience, 75, 'Concurrency must not duplicate XP');
  });
});

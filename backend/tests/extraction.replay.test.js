// tests/extraction.replay.test.js
'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const ExtractionCommitService = require('../src/services/ExtractionCommitService');
const Character = require('../src/models/Character');
const AuthoritativeExtractionResult = require('../src/models/AuthoritativeExtractionResult');

test('Extraction Replay Security (US-58 / SEC-58.3)', async (t) => {
  const originalCharFindOne = Character.findOne;
  const originalCharFindOneAndUpdate = Character.findOneAndUpdate;
  const originalAuthFindOne = AuthoritativeExtractionResult.findOne;
  const originalAuthFindOneAndUpdate = AuthoritativeExtractionResult.findOneAndUpdate;

  let mockChar = null;
  let mockAuthResult = null;

  t.afterEach(() => {
    Character.findOne = originalCharFindOne;
    Character.findOneAndUpdate = originalCharFindOneAndUpdate;
    AuthoritativeExtractionResult.findOne = originalAuthFindOne;
    AuthoritativeExtractionResult.findOneAndUpdate = originalAuthFindOneAndUpdate;
    mockChar = null;
    mockAuthResult = null;
  });

  t.beforeEach(() => {
    mockChar = {
      accountId: 'acc123',
      level: 1,
      experience: 0,
      revision: 0,
      characterAttributes: { availablePoints: 0, vitality: 5 },
      inventory: {
        loadout: [],
        stash: [],
        preparedEquipment: {},
        appliedExtractionReceipts: []
      },
      appliedProgressionReceipts: [],
      save: async function() { return this; },
      markModified: function() {}
    };

    mockAuthResult = {
      _id: 'mock_auth_id_1',
      resultId: 'res_auth_1',
      raidId: 'raid_100',
      accountId: 'acc123',
      items: [{ lootId: 'healthpotion', amount: 2 }],
      experienceGranted: 20,
      consumed: false,
      consumedAt: null
    };

    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => mockAuthResult;

    AuthoritativeExtractionResult.findOneAndUpdate = async (query, update, options) => {
      if (mockAuthResult.consumed && query.consumed?.$ne === true) {
        return null; // Atomic lock fails if already consumed!
      }
      if (update.$set) {
        if (update.$set.consumed !== undefined) mockAuthResult.consumed = update.$set.consumed;
        if (update.$set.consumedAt !== undefined) mockAuthResult.consumedAt = update.$set.consumedAt;
      }
      return mockAuthResult;
    };

    Character.findOneAndUpdate = async (query, update, options) => {
      if (update.$set) {
        if (update.$set['inventory.loadout']) mockChar.inventory.loadout = update.$set['inventory.loadout'];
        if (update.$set.experience !== undefined) mockChar.experience = update.$set.experience;
        if (update.$set.level !== undefined) mockChar.level = update.$set.level;
      }
      if (update.$push && update.$push['inventory.appliedExtractionReceipts']) {
        mockChar.inventory.appliedExtractionReceipts.push(...update.$push['inventory.appliedExtractionReceipts'].$each);
      }
      if (update.$push && update.$push['appliedProgressionReceipts']) {
        mockChar.appliedProgressionReceipts.push(...update.$push['appliedProgressionReceipts'].$each);
      }
      mockChar.revision += 1;
      return mockChar;
    };
  });

  await t.test('Exploit: Claiming result once awards 20 XP and loot; claiming again with resultSequence = 2 after emptying loadout must be REJECTED', async () => {
    // 1. First legitimate claim with resultSequence = 1
    const firstCommit = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid_100',
      resultSequence: 1
    });

    assert.equal(firstCommit.alreadySecured, false);
    assert.equal(mockChar.experience, 20);
    assert.equal(mockChar.inventory.loadout.length, 1);
    assert.equal(mockChar.inventory.loadout[0].amount, 2);

    // 2. Attacker empties loadout by moving items to stash
    mockChar.inventory.stash.push(...mockChar.inventory.loadout);
    mockChar.inventory.loadout = []; // Empty loadout!

    // 3. Attacker calls commit again with resultSequence = 2 attempting to get 40 XP
    await assert.rejects(
      () => ExtractionCommitService.commit('acc123', {
        raidId: 'raid_100',
        resultSequence: 2 // Different sequence!
      }),
      err => {
        assert.equal(err.statusCode, 409);
        assert.equal(err.errorCode, 'RESULT_ALREADY_CONSUMED');
        return true;
      }
    );

    // XP and loot must NOT have increased!
    assert.equal(mockChar.experience, 20, 'Experience must remain 20, NOT 40!');
  });

  await t.test('Idempotency: Replaying exact same resultSequence = 1 returns alreadySecured without error or mutation', async () => {
    // Apply first commit
    await ExtractionCommitService.commit('acc123', { raidId: 'raid_100', resultSequence: 1 });

    // Replay identical request (e.g. dropped network packet retry)
    const retry = await ExtractionCommitService.commit('acc123', { raidId: 'raid_100', resultSequence: 1 });

    assert.equal(retry.alreadySecured, true);
    assert.equal(mockChar.experience, 20);
  });

  await t.test('Rejection: If result is already marked consumed in DB, cannot be consumed', async () => {
    mockAuthResult.consumed = true;
    mockAuthResult.consumedAt = new Date();

    await assert.rejects(
      () => ExtractionCommitService.commit('acc123', { raidId: 'raid_100', resultSequence: 99 }),
      err => {
        assert.equal(err.statusCode, 409);
        assert.equal(err.errorCode, 'RESULT_ALREADY_CONSUMED');
        return true;
      }
    );
  });
});

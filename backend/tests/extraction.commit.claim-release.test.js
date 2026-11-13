// tests/extraction.commit.claim-release.test.js
//
// FIX-20: a failure while writing the character after the authoritative result
// was claimed must release the claim, so a later retry applies the loot exactly once.
// Runs against an in-memory MongoDB; only the character write is made to fail once.

'use strict';

const { describe, test, before, after, beforeEach } = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { startMemoryMongo } = require('./support/memoryMongo');
const Character = require('../src/models/Character');
const AuthoritativeExtractionResult = require('../src/models/AuthoritativeExtractionResult');
const ExtractionCommitService = require('../src/services/ExtractionCommitService');

const accountId = new mongoose.Types.ObjectId().toString();
const raidId = 'raid-claim-release';
const resultSequence = 1;

describe('ExtractionCommitService claim release (FIX-20)', () => {
  let mongoServer;

  before(async () => {
    mongoServer = await startMemoryMongo();
    await mongoose.connect(mongoServer.getUri());
  });

  after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  beforeEach(async () => {
    await Character.deleteMany({});
    await AuthoritativeExtractionResult.deleteMany({});
    await Character.create({ accountId, name: 'ClaimHero' });
    await AuthoritativeExtractionResult.create({
      raidId,
      accountId,
      items: [{ lootId: 'bone', amount: 2 }],
      experienceGranted: 10
    });
  });

  function failNextCharacterWrite(error) {
    const original = Character.findOneAndUpdate;
    Character.findOneAndUpdate = function failingWrite() {
      Character.findOneAndUpdate = original;
      return Promise.reject(error);
    };
    return () => { Character.findOneAndUpdate = original; };
  }

  test('a throwing character write releases the claim and leaves the character untouched', async () => {
    const restore = failNextCharacterWrite(new Error('DB write failed'));
    try {
      await assert.rejects(
        () => ExtractionCommitService.commit(accountId, { raidId, resultSequence }),
        /DB write failed/
      );
    } finally {
      restore();
    }

    const result = await AuthoritativeExtractionResult.findOne({ raidId, accountId }).lean();
    assert.equal(result.consumed, false);
    assert.equal(result.consumedAt, null);

    const character = await Character.findOne({ accountId }).lean();
    assert.deepEqual(character.inventory.loadout, []);
    assert.deepEqual(character.inventory.appliedExtractionReceipts, []);
    assert.equal(character.experience, 0);
    assert.equal(character.revision || 0, 0);
  });

  test('a retry after the failed write applies the loot exactly once', async () => {
    const restore = failNextCharacterWrite(new Error('DB write failed'));
    try {
      await assert.rejects(() => ExtractionCommitService.commit(accountId, { raidId, resultSequence }));
    } finally {
      restore();
    }

    const applied = await ExtractionCommitService.commit(accountId, { raidId, resultSequence });
    assert.equal(applied.alreadySecured, false);
    assert.deepEqual(applied.loadout.map(i => [i.lootId, i.amount]), [['bone', 2]]);
    assert.equal(applied.experience, 10);

    const replay = await ExtractionCommitService.commit(accountId, { raidId, resultSequence });
    assert.equal(replay.alreadySecured, true);
    assert.deepEqual(replay.loadout.map(i => [i.lootId, i.amount]), [['bone', 2]]);
    assert.equal(replay.experience, 10);

    const character = await Character.findOne({ accountId }).lean();
    assert.equal(character.inventory.appliedExtractionReceipts.length, 1);
    const result = await AuthoritativeExtractionResult.findOne({ raidId, accountId }).lean();
    assert.equal(result.consumed, true);
  });
});

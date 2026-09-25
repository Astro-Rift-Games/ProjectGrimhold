const test = require('node:test');
const assert = require('node:assert');
const mongoose = require('mongoose');
const { MongoMemoryServer } = require('mongodb-memory-server');

const Character = require('../../src/models/Character');
const AuthoritativeExtractionResult = require('../../src/models/AuthoritativeExtractionResult');
const ProgressionService = require('../../src/services/ProgressionService');
const ExtractionCommitService = require('../../src/services/ExtractionCommitService');

let mongoServer;

test('ProgressionService - Concurrency with Real MongoDB', async (t) => {
  t.before(async () => {
    mongoServer = await MongoMemoryServer.create();
    const uri = mongoServer.getUri();
    await mongoose.connect(uri);
  });

  t.after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  t.afterEach(async () => {
    await Character.deleteMany({});
    await AuthoritativeExtractionResult.deleteMany({});
  });

  await t.test('Promise.allSettled: Two simultaneous commitProgression calls, exact one wins', async () => {
    const accountId = new mongoose.Types.ObjectId();
    const char = new Character({
      accountId: accountId,
      name: 'SimulChar',
      revision: 0,
      characterAttributes: { availablePoints: 5, vitality: 5 }
    });
    await char.save();

    const expectedRevision = 0;

    const results = await Promise.allSettled([
      ProgressionService.commitProgression(accountId.toString(), { attribute: 'vitality', expectedRevision }),
      ProgressionService.commitProgression(accountId.toString(), { attribute: 'vitality', expectedRevision })
    ]);

    const fulfilled = results.filter(r => r.status === 'fulfilled');
    const rejected = results.filter(r => r.status === 'rejected');

    assert.strictEqual(fulfilled.length, 1, 'Exactly one should succeed');
    assert.strictEqual(rejected.length, 1, 'Exactly one should fail');
    
    assert.strictEqual(fulfilled[0].value.revision, 1);
    assert.strictEqual(rejected[0].reason.statusCode, 409);
    assert.strictEqual(rejected[0].reason.errorCode, 'REVISION_CONFLICT');

    const dbChar = await Character.findOne({ accountId });
    assert.strictEqual(dbChar.revision, 1);
    assert.strictEqual(dbChar.characterAttributes.vitality, 6);
    assert.strictEqual(dbChar.characterAttributes.availablePoints, 4);
  });

  await t.test('Concurrent commitProgression during extraction fails the extraction and preserves the spent point', async () => {
    const accountId = new mongoose.Types.ObjectId();
    const char = new Character({
      accountId: accountId,
      name: 'ExtChar',
      revision: 0,
      level: 1,
      experience: 0,
      characterAttributes: { availablePoints: 1, vitality: 5 },
      inventory: { stash: [], loadout: [], preparedEquipment: {} }
    });
    await char.save();

    await AuthoritativeExtractionResult.create({
      raidId: 'raid_999',
      accountId: accountId,
      items: [],
      experienceGranted: 1000 // Grants level up
    });

    const originalFindOneAndUpdate = Character.findOneAndUpdate;
    let updateCalled = false;
    
    Character.findOneAndUpdate = async function(filter, update, options) {
      if (!updateCalled && update.$push && update.$push['appliedProgressionReceipts']) {
        updateCalled = true;
        await ProgressionService.commitProgression(accountId.toString(), { attribute: 'vitality', expectedRevision: 0 });
      }
      return originalFindOneAndUpdate.call(this, filter, update, options);
    };

    try {
      await assert.rejects(
        () => ExtractionCommitService.commit(accountId.toString(), { raidId: 'raid_999', resultSequence: 1 }),
        err => {
          assert.strictEqual(err.statusCode, 409);
          assert.strictEqual(err.errorCode, 'REVISION_CONFLICT');
          return true;
        }
      );
    } finally {
      Character.findOneAndUpdate = originalFindOneAndUpdate;
    }

    const dbChar = await Character.findOne({ accountId });
    assert.strictEqual(dbChar.revision, 1);
    assert.strictEqual(dbChar.characterAttributes.availablePoints, 0);
    assert.strictEqual(dbChar.characterAttributes.vitality, 6);
    assert.strictEqual(dbChar.level, 1);
  });
});

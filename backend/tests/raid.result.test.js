// tests/raid.result.test.js

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('crypto');
const env = require('../src/config/env');
const AuthoritativeExtractionResult = require('../src/models/AuthoritativeExtractionResult');
const RaidResultService = require('../src/services/RaidResultService');

test('RaidResultService.publishHostResult', async (t) => {
  const originalFindOneAndUpdate = AuthoritativeExtractionResult.findOneAndUpdate;
  let findOneAndUpdateArgs = null;

  t.afterEach(() => {
    AuthoritativeExtractionResult.findOneAndUpdate = originalFindOneAndUpdate;
    findOneAndUpdateArgs = null;
  });

  t.beforeEach(() => {
    AuthoritativeExtractionResult.findOneAndUpdate = async (query, update, options) => {
      findOneAndUpdateArgs = { query, update, options };
      return {}; // Dummy return
    };
  });

  const accountId = 'host_acc_123';
  const raidId = 'raid_999';
  const resultSequence = 1;

  const validSignature = crypto
    .createHmac('sha256', env.webhookSecret)
    .update(`${raidId}:${resultSequence}`)
    .digest('hex');

  await t.test('successfully publishes result with valid signature', async () => {
    const payload = {
      raidId,
      resultSequence,
      items: [{ lootId: 'gold', amount: 100 }],
      experienceGranted: 50,
      hostSignature: validSignature
    };

    const result = await RaidResultService.publishHostResult(accountId, payload);

    assert.equal(result.status, 'published');
    assert.equal(result.raidId, raidId);
    assert.equal(result.accountId, accountId);

    assert.ok(findOneAndUpdateArgs);
    assert.deepEqual(findOneAndUpdateArgs.query, { raidId, accountId });
    assert.deepEqual(findOneAndUpdateArgs.update.items, payload.items);
    assert.equal(findOneAndUpdateArgs.update.experienceGranted, payload.experienceGranted);
  });

  await t.test('throws 401 when signature is invalid', async () => {
    const payload = {
      raidId,
      resultSequence,
      items: [],
      experienceGranted: 0,
      hostSignature: 'invalid_signature_hex'
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(accountId, payload),
      err => {
        assert.equal(err.statusCode, 401);
        assert.equal(err.errorCode, 'INVALID_HOST_SIGNATURE');
        return true;
      }
    );
  });

  await t.test('throws 400 when required fields are missing', async () => {
    const payload = {
      raidId,
      // missing resultSequence
      hostSignature: validSignature
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(accountId, payload),
      err => {
        assert.equal(err.statusCode, 400);
        assert.equal(err.errorCode, 'INVALID_RESULT_PAYLOAD');
        return true;
      }
    );
  });
});

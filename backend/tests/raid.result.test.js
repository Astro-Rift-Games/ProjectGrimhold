// tests/raid.result.test.js

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('crypto');
const env = require('../src/config/env');
const AuthoritativeExtractionResult = require('../src/models/AuthoritativeExtractionResult');
const RaidResultService = require('../src/services/RaidResultService');
const { buildCanonicalResultString } = require('../src/utils/raidResultCanonicalizer');

test('RaidResultService.publishHostResult (US-58 / SEC-58.2)', async (t) => {
  const Character = require('../src/models/Character');
  const originalExists = Character.exists;
  t.after(() => { Character.exists = originalExists; });
  Character.exists = async () => null;
  const originalFindOne = AuthoritativeExtractionResult.findOne;
  const originalCreate = AuthoritativeExtractionResult.create;
  let mockExistingDoc = null;
  let createArgs = null;

  t.afterEach(() => {
    AuthoritativeExtractionResult.findOne = originalFindOne;
    AuthoritativeExtractionResult.create = originalCreate;
    mockExistingDoc = null;
    createArgs = null;
  });

  t.beforeEach(() => {
    AuthoritativeExtractionResult.findOne = async (query) => {
      return mockExistingDoc;
    };
    AuthoritativeExtractionResult.create = async (doc) => {
      createArgs = doc;
      return { ...doc, _id: 'mock_doc_id' };
    };
  });

  const accountId = 'host_acc_123';
  const targetAccountId = 'player_acc_456';
  const raidId = 'raid_999';
  const resultId = 'res_001';
  const issuedAt = '2026-10-19T12:00:00.000Z';

  function signPayload(p) {
    const canonical = buildCanonicalResultString(p);
    return crypto.createHmac('sha256', env.webhookSecret).update(canonical).digest('hex');
  }

  await t.test('successfully publishes result with valid canonical signature', async () => {
    const payload = {
      raidId,
      resultId,
      targetAccountId,
      items: [{ lootId: 'gold', amount: 100 }],
      experienceGranted: 50,
      preparedEquipment: { weaponSlot1: 'sword' },
      issuedAt
    };
    payload.hostSignature = signPayload(payload);

    const result = await RaidResultService.publishHostResult(accountId, payload);

    assert.equal(result.status, 'published');
    assert.equal(result.raidId, raidId);
    assert.equal(result.accountId, targetAccountId);
    assert.equal(result.resultId, resultId);

    assert.ok(createArgs);
    assert.equal(createArgs.raidId, raidId);
    assert.equal(createArgs.accountId, targetAccountId);
    assert.deepEqual(createArgs.items, payload.items);
    assert.equal(createArgs.experienceGranted, 50);
    assert.equal(createArgs.consumed, false);
  });

  await t.test('throws 401 when signature is invalid (random hex)', async () => {
    const payload = {
      raidId,
      resultId,
      targetAccountId,
      items: [],
      experienceGranted: 0,
      issuedAt,
      hostSignature: 'deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef'
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

  await t.test('Exploit: modifying items in payload fails signature verification', async () => {
    const legitimate = {
      raidId,
      resultId,
      targetAccountId,
      items: [{ lootId: 'slime_gel', amount: 1 }],
      experienceGranted: 20,
      issuedAt
    };
    const signature = signPayload(legitimate);

    // Attacker tampers with items: injects 999 diamond items
    const tampered = {
      ...legitimate,
      items: [{ lootId: 'diamond', amount: 999 }],
      hostSignature: signature
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(accountId, tampered),
      err => {
        assert.equal(err.statusCode, 401);
        assert.equal(err.errorCode, 'INVALID_HOST_SIGNATURE');
        return true;
      }
    );
  });

  await t.test('Exploit: modifying experienceGranted fails signature verification', async () => {
    const legitimate = {
      raidId,
      resultId,
      targetAccountId,
      items: [],
      experienceGranted: 20,
      issuedAt
    };
    const signature = signPayload(legitimate);

    // Attacker inflates XP to 1,000,000
    const tampered = {
      ...legitimate,
      experienceGranted: 1000000,
      hostSignature: signature
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(accountId, tampered),
      err => {
        assert.equal(err.statusCode, 401);
        assert.equal(err.errorCode, 'INVALID_HOST_SIGNATURE');
        return true;
      }
    );
  });

  await t.test('Exploit: changing target account fails signature verification', async () => {
    const legitimate = {
      raidId,
      resultId,
      targetAccountId: 'victim_account',
      items: [{ lootId: 'gold', amount: 500 }],
      experienceGranted: 100,
      issuedAt
    };
    const signature = signPayload(legitimate);

    // Attacker redirects result to their own account
    const tampered = {
      ...legitimate,
      targetAccountId: 'attacker_account',
      hostSignature: signature
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(accountId, tampered),
      err => {
        assert.equal(err.statusCode, 401);
        assert.equal(err.errorCode, 'INVALID_HOST_SIGNATURE');
        return true;
      }
    );
  });

  await t.test('Conflict: republishing different payload for existing resultId throws 409', async () => {
    mockExistingDoc = {
      resultId,
      raidId,
      accountId: targetAccountId,
      items: [{ lootId: 'gold', amount: 100 }],
      preparedEquipment: {},
      experienceGranted: 50,
      consumed: false
    };

    const payload = {
      raidId,
      resultId,
      targetAccountId,
      items: [{ lootId: 'gold', amount: 200 }], // conflicting amount!
      experienceGranted: 50,
      issuedAt
    };
    payload.hostSignature = signPayload(payload);

    await assert.rejects(
      () => RaidResultService.publishHostResult(accountId, payload),
      err => {
        assert.equal(err.statusCode, 409);
        assert.equal(err.errorCode, 'RESULT_CONFLICT');
        return true;
      }
    );
  });

  await t.test('Conflict: republishing an already consumed result throws 409', async () => {
    mockExistingDoc = {
      resultId,
      raidId,
      accountId: targetAccountId,
      items: [{ lootId: 'gold', amount: 100 }],
      preparedEquipment: {},
      experienceGranted: 50,
      consumed: true, // already consumed!
      consumedAt: new Date()
    };

    const payload = {
      raidId,
      resultId,
      targetAccountId,
      items: [{ lootId: 'gold', amount: 100 }],
      experienceGranted: 50,
      issuedAt
    };
    payload.hostSignature = signPayload(payload);

    await assert.rejects(
      () => RaidResultService.publishHostResult(accountId, payload),
      err => {
        assert.equal(err.statusCode, 409);
        assert.equal(err.errorCode, 'RESULT_ALREADY_CONSUMED');
        return true;
      }
    );
  });

  await t.test('Idempotency: republishing identical payload returns alreadyPublished: true', async () => {
    mockExistingDoc = {
      resultId,
      raidId,
      accountId: targetAccountId,
      items: [{ lootId: 'gold', amount: 100 }],
      preparedEquipment: {
        armor: '', boots: '', gloves: '', helmet: '', offHand1: '', offHand2: '', weaponSlot1: '', weaponSlot2: ''
      },
      experienceGranted: 50,
      consumed: false
    };

    const payload = {
      raidId,
      resultId,
      targetAccountId,
      items: [{ lootId: 'gold', amount: 100 }],
      experienceGranted: 50,
      preparedEquipment: {},
      issuedAt
    };
    payload.hostSignature = signPayload(payload);

    const res = await RaidResultService.publishHostResult(accountId, payload);
    assert.equal(res.status, 'published');
    assert.equal(res.alreadyPublished, true);
  });

  await t.test('Rejects with 404 CHARACTER_NOT_FOUND when targetCharacterId does not exist instead of falling back to callerAccountId', async () => {
    const Character = require('../src/models/Character');
    const origFindById = Character.findById;
    Character.findById = async () => null;
    try {
      const missingCharId = '507f1f77bcf86cd799439099';
      const payload = {
        raidId,
        resultId: `${raidId}:${missingCharId}:1`,
        resultSequence: 1,
        targetCharacterId: missingCharId,
        items: [{ lootId: 'gold', amount: 10 }],
        experienceGranted: 10,
        issuedAt
      };
      payload.hostSignature = signPayload({
        ...payload,
        targetAccountId: missingCharId
      });

      await assert.rejects(
        () => RaidResultService.publishHostResult(accountId, payload),
        err => {
          assert.equal(err.statusCode, 404);
          assert.equal(err.errorCode, 'CHARACTER_NOT_FOUND');
          return true;
        }
      );
    } finally {
      Character.findById = origFindById;
    }
  });
});

test('RaidResultService Bounded Player-Host Authority (TASK-223 / SEC-223)', async (t) => {
  const Character = require('../src/models/Character');
  const originalExists = Character.exists;
  const originalFindOne = Character.findOne;
  const originalFindById = Character.findById;
  const originalResultFindOne = AuthoritativeExtractionResult.findOne;
  const originalResultCreate = AuthoritativeExtractionResult.create;

  let createArgs = null;
  let mockExistingDoc = null;

  const mockCharacters = new Map();

  t.beforeEach(() => {
    createArgs = null;
    mockExistingDoc = null;
    mockCharacters.clear();

    mockCharacters.set('host_acc_123', {
      _id: 'char_host_id',
      accountId: 'host_acc_123',
      name: 'HostCharacter',
      inventory: {
        pendingReservation: { reservationId: 'res_host' },
        appliedExtractionReceipts: []
      }
    });

    mockCharacters.set('player_acc_456', {
      _id: 'char_player_id',
      accountId: 'player_acc_456',
      name: 'PlayerCharacter',
      inventory: {
        pendingReservation: { reservationId: 'res_player' },
        appliedExtractionReceipts: []
      }
    });

    Character.exists = async () => null;
    Character.findOne = async (query) => {
      if (query.accountId) return mockCharacters.get(query.accountId) || null;
      return null;
    };
    Character.findById = async (id) => {
      for (const char of mockCharacters.values()) {
        if (char._id === id) return char;
      }
      return null;
    };
    AuthoritativeExtractionResult.findOne = async () => mockExistingDoc;
    AuthoritativeExtractionResult.create = async (doc) => {
      createArgs = doc;
      return { ...doc, _id: 'mock_doc_id' };
    };
  });

  t.afterEach(() => {
    Character.exists = originalExists;
    Character.findOne = originalFindOne;
    Character.findById = originalFindById;
    AuthoritativeExtractionResult.findOne = originalResultFindOne;
    AuthoritativeExtractionResult.create = originalResultCreate;
  });

  const hostAccountId = 'host_acc_123';
  const playerAccountId = 'player_acc_456';
  const raidId = 'raid_bounded_1';
  const resultId = 'res_bounded_001';
  const issuedAt = '2026-10-27T12:00:00.000Z';

  await t.test('successfully publishes unsigned result when host and recipient have active pendingReservation', async () => {
    const payload = {
      raidId,
      resultId,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'iron_ore', amount: 5 }],
      preparedEquipment: { weaponSlot1: 'arming_sword' },
      experienceGranted: 250,
      hostSignature: '',
      issuedAt
    };

    const result = await RaidResultService.publishHostResult(hostAccountId, payload);
    assert.equal(result.status, 'published');
    assert.equal(result.raidId, raidId);
    assert.equal(result.accountId, playerAccountId);
    assert.ok(createArgs);
    assert.equal(createArgs.experienceGranted, 250);
    assert.equal(createArgs.hostSignature, '');
  });

  await t.test('rejects with 422 NO_ACTIVE_RESERVATION when target character has no active reservation', async () => {
    mockCharacters.get(playerAccountId).inventory.pendingReservation = null;

    const payload = {
      raidId,
      resultId,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'iron_ore', amount: 1 }],
      experienceGranted: 50,
      hostSignature: '',
      issuedAt
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(hostAccountId, payload),
      err => {
        assert.equal(err.statusCode, 422);
        assert.equal(err.errorCode, 'NO_ACTIVE_RESERVATION');
        return true;
      }
    );
  });

  await t.test('rejects with 403 HOST_NOT_IN_RAID when caller host has no reservation and no prior receipt for this raid', async () => {
    const outsiderHost = 'outsider_host_acc';
    mockCharacters.set(outsiderHost, {
      _id: 'char_outsider_id',
      accountId: outsiderHost,
      name: 'OutsiderHost',
      inventory: {
        pendingReservation: null,
        appliedExtractionReceipts: []
      }
    });

    const payload = {
      raidId,
      resultId,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'iron_ore', amount: 1 }],
      experienceGranted: 50,
      hostSignature: '',
      issuedAt
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(outsiderHost, payload),
      err => {
        assert.equal(err.statusCode, 403);
        assert.equal(err.errorCode, 'HOST_NOT_IN_RAID');
        return true;
      }
    );
  });

  await t.test('rejects with 422 EXCEEDS_INVENTORY_CAPACITY when items array exceeds 30 slots', async () => {
    const items = [];
    for (let i = 0; i < 31; i++) {
      items.push({ lootId: `loot_item_${i}`, amount: 1 });
    }

    const payload = {
      raidId,
      resultId,
      targetAccountId: playerAccountId,
      items,
      experienceGranted: 100,
      hostSignature: '',
      issuedAt
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(hostAccountId, payload),
      err => {
        assert.equal(err.statusCode, 422);
        assert.equal(err.errorCode, 'EXCEEDS_INVENTORY_CAPACITY');
        return true;
      }
    );
  });

  await t.test('rejects with 422 INVALID_ITEM_SPECIFICATION when item amount is negative or non-integer', async () => {
    const payload = {
      raidId,
      resultId,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'gold', amount: -5 }],
      experienceGranted: 100,
      hostSignature: '',
      issuedAt
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(hostAccountId, payload),
      err => {
        assert.equal(err.statusCode, 422);
        assert.equal(err.errorCode, 'INVALID_ITEM_SPECIFICATION');
        return true;
      }
    );
  });

  await t.test('rejects with 422 EXCEEDS_EXPERIENCE_BUDGET when experienceGranted exceeds 5000 XP', async () => {
    const payload = {
      raidId,
      resultId,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'gold', amount: 10 }],
      experienceGranted: 999999, // Exploit attempt
      hostSignature: '',
      issuedAt
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(hostAccountId, payload),
      err => {
        assert.equal(err.statusCode, 422);
        assert.equal(err.errorCode, 'EXCEEDS_EXPERIENCE_BUDGET');
        return true;
      }
    );
  });

  await t.test('rejects with 422 INVALID_EXPERIENCE when experienceGranted is negative', async () => {
    const payload = {
      raidId,
      resultId,
      targetAccountId: playerAccountId,
      items: [],
      experienceGranted: -50,
      hostSignature: '',
      issuedAt
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(hostAccountId, payload),
      err => {
        assert.equal(err.statusCode, 422);
        assert.equal(err.errorCode, 'INVALID_EXPERIENCE');
        return true;
      }
    );
  });

  await t.test('rejects with 401 INVALID_HOST_SIGNATURE when client presents forged or obsolete dev secret signature', async () => {
    const canonical = buildCanonicalResultString({
      raidId,
      resultId,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'gold', amount: 1000 }],
      preparedEquipment: {},
      experienceGranted: 1000,
      issuedAt
    });

    // Attacker computes HMAC using leaked or stale development secret
    const forgedSignature = crypto
      .createHmac('sha256', 'old-leaked-dev-secret')
      .update(canonical)
      .digest('hex');

    const payload = {
      raidId,
      resultId,
      targetAccountId: playerAccountId,
      items: [{ lootId: 'gold', amount: 1000 }],
      experienceGranted: 1000,
      hostSignature: forgedSignature,
      issuedAt
    };

    await assert.rejects(
      () => RaidResultService.publishHostResult(hostAccountId, payload),
      err => {
        assert.equal(err.statusCode, 401);
        assert.equal(err.errorCode, 'INVALID_HOST_SIGNATURE');
        return true;
      }
    );
  });
});

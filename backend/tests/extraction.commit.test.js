// tests/extraction.commit.test.js
//
// Unit tests for ExtractionCommitService.
// Uses node:test + node:assert. No real MongoDB — Character.findOne is monkey-patched.

'use strict';

const test   = require('node:test');
const assert = require('node:assert/strict');
const Character             = require('../src/models/Character');
const AuthoritativeExtractionResult = require('../src/models/AuthoritativeExtractionResult');
const ExtractionCommitService = require('../src/services/ExtractionCommitService');

// ---------------------------------------------------------------------------
// Test fixture helpers
// ---------------------------------------------------------------------------

function makeCharacter(overrides = {}) {
  const doc = {
    accountId:  'acc123',
    level:      1,
    experience: 0,
    lastAppliedProgressionResultSequence: 0,
    lastProgressionReceipt:   null,
    appliedProgressionReceipts: [],
    characterAttributes: {
      vitality: 5, resistance: 5, strength: 5,
      dexterity: 5, intelligence: 5, luck: 5,
      availablePoints: 10,
    },
    inventory: {
      stash:   [],
      loadout: [],
      preparedEquipment:        { weaponSlot1: '', weaponSlot2: '', helmet: '', armor: '', gloves: '', boots: '' },
      pendingReservation:       null,
      appliedExtractionReceipts: [],
    },
    ...overrides,
  };
  doc.markModified = () => {};
  doc.save = async function () { return this; };
  return doc;
}

// ---------------------------------------------------------------------------
// ExtractionCommitService.commit
// ---------------------------------------------------------------------------

test('ExtractionCommitService.commit', async (t) => {
  const originalFindOne = Character.findOne;
  const originalAuthFindOne = AuthoritativeExtractionResult.findOne;
  const originalFindOneAndUpdate = Character.findOneAndUpdate;
  t.afterEach(() => { 
    Character.findOne = originalFindOne; 
    AuthoritativeExtractionResult.findOne = originalAuthFindOne;
    Character.findOneAndUpdate = originalFindOneAndUpdate;
  });
  t.beforeEach(() => {
    // Default auth result if not overridden
    AuthoritativeExtractionResult.findOne = async () => ({
      items: [],
      experienceGranted: 0,
    });
    
    Character.findOneAndUpdate = async (query, update, options) => {
      const char = await Character.findOne(query);
      if (!char) return null;

      const notMatch = query['inventory.appliedExtractionReceipts']?.$not?.$elemMatch;
      if (notMatch) {
        const { raidId, resultSequence } = notMatch;
        const exists = char.inventory.appliedExtractionReceipts.some(r => r.raidId === raidId && r.resultSequence === resultSequence);
        if (exists) return null;
      }

      if (update.$unset && update.$unset['inventory.pendingReservation']) {
        char.inventory.pendingReservation = null;
      }
      
      if (update.$set) {
        if (update.$set['inventory.preparedEquipment'] !== undefined) char.inventory.preparedEquipment = update.$set['inventory.preparedEquipment'];
        if (update.$set['inventory.loadout'] !== undefined) char.inventory.loadout = update.$set['inventory.loadout'];
        if (update.$set.level !== undefined) char.level = update.$set.level;
        if (update.$set.experience !== undefined) char.experience = update.$set.experience;
        if (update.$set['characterAttributes.availablePoints'] !== undefined) {
          if (!char.characterAttributes) char.characterAttributes = {};
          char.characterAttributes.availablePoints = update.$set['characterAttributes.availablePoints'];
        }
        if (update.$set.lastAppliedProgressionResultSequence !== undefined) char.lastAppliedProgressionResultSequence = update.$set.lastAppliedProgressionResultSequence;
        if (update.$set.lastProgressionReceipt !== undefined) char.lastProgressionReceipt = update.$set.lastProgressionReceipt;
      }

      if (update.$push) {
        if (update.$push['inventory.appliedExtractionReceipts']) {
          char.inventory.appliedExtractionReceipts.push(...update.$push['inventory.appliedExtractionReceipts'].$each);
          const slice = update.$push['inventory.appliedExtractionReceipts'].$slice;
          if (slice && slice < 0) char.inventory.appliedExtractionReceipts = char.inventory.appliedExtractionReceipts.slice(slice);
        }
        if (update.$push['appliedProgressionReceipts']) {
          char.appliedProgressionReceipts.push(...update.$push['appliedProgressionReceipts'].$each);
          const slice = update.$push['appliedProgressionReceipts'].$slice;
          if (slice && slice < 0) char.appliedProgressionReceipts = char.appliedProgressionReceipts.slice(slice);
        }
      }
      
      await char.save();
      return char;
    };
  });

  // -------------------------------------------------------------------------
  // Hardening scenarios
  // -------------------------------------------------------------------------

  await t.test('throws 422 if no AuthoritativeExtractionResult is found', async () => {
    AuthoritativeExtractionResult.findOne = async () => null; // Missing result
    
    await assert.rejects(
      () => ExtractionCommitService.commit('acc123', { raidId: 'r', resultSequence: 1 }),
      err => {
        assert.equal(err.statusCode, 422);
        assert.equal(err.errorCode, 'NO_AUTHORITATIVE_RESULT');
        return true;
      }
    );
  });

  // -------------------------------------------------------------------------
  // Loot-only scenarios
  // -------------------------------------------------------------------------

  await t.test('first commit: persists items to loadout and records receipt', async () => {
    const mockChar = makeCharacter();
    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => ({
      items: [{ lootId: 'sword', amount: 2 }, { lootId: 'potion', amount: 5 }],
      experienceGranted: 0
    });

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-001', resultSequence: 1,
      // items sent in payload are ignored now, but we'll include them to show they don't matter
      items: [{ lootId: 'wrong', amount: 99 }]
    });

    assert.equal(result.alreadySecured, false);
    assert.deepEqual(result.loadout, [
      { lootId: 'sword',  amount: 2 },
      { lootId: 'potion', amount: 5 },
    ]);
    assert.equal(mockChar.inventory.appliedExtractionReceipts.length, 1);
    assert.equal(mockChar.inventory.appliedExtractionReceipts[0].raidId, 'raid-001');
    assert.equal(mockChar.inventory.appliedExtractionReceipts[0].resultSequence, 1);
  });

  await t.test('replayed receipt: returns alreadySecured without mutating state', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.appliedExtractionReceipts = [{ raidId: 'raid-001', resultSequence: 1 }];
    mockChar.inventory.loadout = [{ lootId: 'sword', amount: 2 }];
    let saveCalled = false;
    mockChar.save = async () => { saveCalled = true; return mockChar; };
    Character.findOne = async () => mockChar;
    
    // Auth result doesn't even need to matter here as long as it exists (returns early)
    AuthoritativeExtractionResult.findOne = async () => ({ items: [{ lootId: 'sword', amount: 2 }] });

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-001', resultSequence: 1,
    });

    assert.equal(result.alreadySecured, true);
    assert.equal(mockChar.inventory.loadout.length, 1);   // unchanged
    assert.equal(saveCalled, false);                       // no write
  });

  await t.test('zero-loot extraction: records receipt, leaves loadout empty', async () => {
    const mockChar = makeCharacter();
    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => ({ items: [], experienceGranted: 0 });

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-002', resultSequence: 1,
    });

    assert.equal(result.alreadySecured, false);
    assert.deepEqual(result.loadout, []);
    assert.equal(mockChar.inventory.appliedExtractionReceipts.length, 1);
  });

  await t.test('throws 404 when character does not exist', async () => {
    Character.findOne = async () => null;

    await assert.rejects(
      () => ExtractionCommitService.commit('missing', { raidId: 'r', resultSequence: 1 }),
      err => {
        assert.equal(err.statusCode, 404);
        assert.equal(err.errorCode, 'CHARACTER_NOT_FOUND');
        return true;
      }
    );
  });

  await t.test('throws 409 when loadout is not empty', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [{ lootId: 'axe', amount: 1 }];
    Character.findOne = async () => mockChar;

    await assert.rejects(
      () => ExtractionCommitService.commit('acc123', { raidId: 'r', resultSequence: 1 }),
      err => {
        assert.equal(err.statusCode, 409);
        assert.equal(err.errorCode, 'LOADOUT_NOT_EMPTY');
        return true;
      }
    );
  });

  await t.test('evicts oldest receipts beyond cap of 256', async () => {
    const mockChar = makeCharacter();
    for (let i = 1; i <= 256; i++) {
      mockChar.inventory.appliedExtractionReceipts.push({ raidId: `raid-${i}`, resultSequence: i });
    }
    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => ({ items: [], experienceGranted: 0 });

    await ExtractionCommitService.commit('acc123', { raidId: 'raid-999', resultSequence: 999 });

    assert.equal(mockChar.inventory.appliedExtractionReceipts.length, 256);
    assert.equal(mockChar.inventory.appliedExtractionReceipts[0].raidId, 'raid-2');   // oldest evicted
    assert.equal(mockChar.inventory.appliedExtractionReceipts[255].raidId, 'raid-999');
  });

  await t.test('clears pendingReservation and restores preparedEquipment from authResult', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.pendingReservation = {
      reservationId: 'res-1',
      items: [],
      preparedEquipment: { weaponSlot1: 'sword_epic', weaponSlot2: '', helmet: '', armor: '', gloves: '', boots: '' },
    };
    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => ({ 
      items: [{ lootId: 'gem', amount: 1 }],
      experienceGranted: 0,
      preparedEquipment: { weaponSlot1: 'sword_epic', weaponSlot2: 'shield', helmet: '', armor: '', gloves: '', boots: '' }
    });

    await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-003', resultSequence: 1,
    });

    assert.equal(mockChar.inventory.pendingReservation, null);
    assert.equal(mockChar.inventory.preparedEquipment.weaponSlot1, 'sword_epic');
    assert.equal(mockChar.inventory.preparedEquipment.weaponSlot2, 'shield');
  });

  await t.test('clears pendingReservation and restores preparedEquipment from pendingReservation if authResult is missing it', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.pendingReservation = {
      reservationId: 'res-1',
      items: [],
      preparedEquipment: { weaponSlot1: 'sword_epic', weaponSlot2: '', helmet: '', armor: '', gloves: '', boots: '' },
    };
    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => ({ 
      items: [{ lootId: 'gem', amount: 1 }],
      experienceGranted: 0,
    });

    await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-003', resultSequence: 1,
    });

    assert.equal(mockChar.inventory.pendingReservation, null);
    assert.equal(mockChar.inventory.preparedEquipment.weaponSlot1, 'sword_epic');
    assert.equal(mockChar.inventory.preparedEquipment.weaponSlot2, '');
  });

  // -------------------------------------------------------------------------
  // Progression scenarios
  // -------------------------------------------------------------------------

  await t.test('with progression: applies XP gain and levels up', async () => {
    const mockChar = makeCharacter({ level: 1, experience: 0 });
    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => ({ 
      items: [],
      experienceGranted: 100
    });

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-004', resultSequence: 1,
    });

    assert.equal(result.alreadySecured, false);
    assert.equal(result.level, 2);
    assert.equal(result.experience, 0);  // exactly met threshold
    // 1 level gained → 1 attribute point
    assert.equal(result.characterAttributes.availablePoints, 11);
    assert.equal(mockChar.lastAppliedProgressionResultSequence, 1);
    assert.equal(mockChar.appliedProgressionReceipts.length, 1);
  });

  await t.test('with progression: partial XP gain with no level-up', async () => {
    const mockChar = makeCharacter({ level: 1, experience: 0 });
    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => ({ 
      items: [],
      experienceGranted: 50
    });

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-005', resultSequence: 1,
    });

    assert.equal(result.level, 1);
    assert.equal(result.experience, 50);
    assert.equal(result.characterAttributes.availablePoints, 10);  // no points granted
  });

  await t.test('with progression: multi-level-up grants correct attribute points', async () => {
    const mockChar = makeCharacter({ level: 1, experience: 0 });
    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => ({ 
      items: [],
      experienceGranted: 205
    });

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-006', resultSequence: 1,
    });

    assert.equal(result.level, 3);
    assert.equal(result.experience, 0);
    assert.equal(result.characterAttributes.availablePoints, 12);  // 2 levels gained = 2 points
  });

  await t.test('save failure: error propagates, no partial state visible', async () => {
    const mockChar = makeCharacter();
    mockChar.save = async () => { throw new Error('DB unavailable'); };
    Character.findOne = async () => mockChar;
    AuthoritativeExtractionResult.findOne = async () => ({ items: [{ lootId: 'shield', amount: 1 }] });

    await assert.rejects(
      () => ExtractionCommitService.commit('acc123', {
        raidId: 'raid-010', resultSequence: 1,
      }),
      /DB unavailable/
    );
  });
});

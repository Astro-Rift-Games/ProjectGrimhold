// tests/extraction.commit.test.js
//
// Unit tests for ExtractionCommitService.
// Uses node:test + node:assert. No real MongoDB — Character.findOne is monkey-patched.

'use strict';

const test   = require('node:test');
const assert = require('node:assert/strict');
const Character             = require('../src/models/Character');
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
  t.afterEach(() => { Character.findOne = originalFindOne; });

  // -------------------------------------------------------------------------
  // Loot-only scenarios
  // -------------------------------------------------------------------------

  await t.test('first commit: persists items to loadout and records receipt', async () => {
    const mockChar = makeCharacter();
    Character.findOne = async () => mockChar;

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-001', resultSequence: 1,
      items: [{ lootId: 'sword', amount: 2 }, { lootId: 'potion', amount: 5 }],
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

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-001', resultSequence: 1,
      items: [{ lootId: 'sword', amount: 2 }],
    });

    assert.equal(result.alreadySecured, true);
    assert.equal(mockChar.inventory.loadout.length, 1);   // unchanged
    assert.equal(saveCalled, false);                       // no write
  });

  await t.test('zero-loot extraction: records receipt, leaves loadout empty', async () => {
    const mockChar = makeCharacter();
    Character.findOne = async () => mockChar;

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-002', resultSequence: 1,
      items: [],
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

    await ExtractionCommitService.commit('acc123', { raidId: 'raid-999', resultSequence: 999 });

    assert.equal(mockChar.inventory.appliedExtractionReceipts.length, 256);
    assert.equal(mockChar.inventory.appliedExtractionReceipts[0].raidId, 'raid-2');   // oldest evicted
    assert.equal(mockChar.inventory.appliedExtractionReceipts[255].raidId, 'raid-999');
  });

  await t.test('clears pendingReservation and restores preparedEquipment', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.pendingReservation = {
      reservationId: 'res-1',
      items: [],
      preparedEquipment: { weaponSlot1: 'sword_epic', weaponSlot2: '', helmet: '', armor: '', gloves: '', boots: '' },
    };
    Character.findOne = async () => mockChar;

    await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-003', resultSequence: 1,
      items:  [{ lootId: 'gem', amount: 1 }],
    });

    assert.equal(mockChar.inventory.pendingReservation, null);
    assert.equal(mockChar.inventory.preparedEquipment.weaponSlot1, 'sword_epic');
  });

  // -------------------------------------------------------------------------
  // Progression scenarios
  // -------------------------------------------------------------------------

  await t.test('with progression: applies XP gain and levels up', async () => {
    // Level 1, 0 XP. Earn 100 XP → should reach level 2.
    const mockChar = makeCharacter({ level: 1, experience: 0 });
    Character.findOne = async () => mockChar;

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-004', resultSequence: 1,
      progression: { consolidatedExperience: 100, resultingLevel: 2 },
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

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-005', resultSequence: 1,
      progression: { consolidatedExperience: 50, resultingLevel: 1 },
    });

    assert.equal(result.level, 1);
    assert.equal(result.experience, 50);
    assert.equal(result.characterAttributes.availablePoints, 10);  // no points granted
  });

  await t.test('with progression: multi-level-up grants correct attribute points', async () => {
    // Level 1, 0 XP. Earn 210 XP → level 1→2 needs 100, level 2→3 needs 105: 205 total → level 3, 5 XP left.
    const mockChar = makeCharacter({ level: 1, experience: 0 });
    Character.findOne = async () => mockChar;

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-006', resultSequence: 1,
      progression: { consolidatedExperience: 205, resultingLevel: 3 },
    });

    assert.equal(result.level, 3);
    assert.equal(result.experience, 0);
    assert.equal(result.characterAttributes.availablePoints, 12);  // 2 levels gained = 2 points
  });

  await t.test('with progression: throws 422 when resultingLevel does not match server computation', async () => {
    const mockChar = makeCharacter({ level: 1, experience: 0 });
    Character.findOne = async () => mockChar;

    await assert.rejects(
      () => ExtractionCommitService.commit('acc123', {
        raidId: 'raid-007', resultSequence: 1,
        // 50 XP at level 1 → still level 1, but client claims level 99
        progression: { consolidatedExperience: 50, resultingLevel: 99 },
      }),
      err => {
        assert.equal(err.statusCode, 422);
        assert.equal(err.errorCode, 'PROGRESSION_MISMATCH');
        return true;
      }
    );
  });

  await t.test('with progression already applied: skips progression, still applies loot', async () => {
    // resultSequence 1 was already applied (watermark = 1).
    const mockChar = makeCharacter({ level: 2, experience: 50, lastAppliedProgressionResultSequence: 1 });
    Character.findOne = async () => mockChar;

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-008', resultSequence: 1,
      items:  [{ lootId: 'gem', amount: 3 }],
      progression: { consolidatedExperience: 100, resultingLevel: 3 },  // would be invalid if applied
    });

    // Level and XP must not change.
    assert.equal(result.level, 2);
    assert.equal(result.experience, 50);
    // Loot must be applied.
    assert.deepEqual(result.loadout, [{ lootId: 'gem', amount: 3 }]);
  });

  await t.test('commit without progression field: only loot is applied', async () => {
    const mockChar = makeCharacter({ level: 5, experience: 200 });
    Character.findOne = async () => mockChar;

    const result = await ExtractionCommitService.commit('acc123', {
      raidId: 'raid-009', resultSequence: 1,
      items: [{ lootId: 'arrow', amount: 10 }],
      // no `progression` key
    });

    assert.equal(result.level, 5);      // unchanged
    assert.equal(result.experience, 200); // unchanged
    assert.deepEqual(result.loadout, [{ lootId: 'arrow', amount: 10 }]);
  });

  await t.test('save failure: error propagates, no partial state visible', async () => {
    const mockChar = makeCharacter();
    mockChar.save = async () => { throw new Error('DB unavailable'); };
    Character.findOne = async () => mockChar;

    await assert.rejects(
      () => ExtractionCommitService.commit('acc123', {
        raidId: 'raid-010', resultSequence: 1,
        items: [{ lootId: 'shield', amount: 1 }],
      }),
      /DB unavailable/
    );
  });
});

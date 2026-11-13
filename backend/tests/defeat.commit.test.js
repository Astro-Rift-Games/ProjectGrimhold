// tests/defeat.commit.test.js
//
// Unit tests for DefeatCommitService.
// Uses node:test + node:assert. No real MongoDB — Character and AuthoritativeExtractionResult are monkey-patched.

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const Character = require('../src/models/Character');
const AuthoritativeExtractionResult = require('../src/models/AuthoritativeExtractionResult');
const DefeatCommitService = require('../src/services/DefeatCommitService');

function makeCharacter(overrides = {}) {
  const doc = {
    accountId: 'acc123',
    level: 1,
    experience: 0,
    lastAppliedProgressionResultSequence: 0,
    lastProgressionReceipt: null,
    appliedProgressionReceipts: [],
    characterAttributes: {
      vitality: 5, resistance: 5, strength: 5,
      dexterity: 5, intelligence: 5, luck: 5,
      availablePoints: 10,
    },
    inventory: {
      stash: [],
      loadout: [],
      preparedEquipment: { weaponSlot1: '', weaponSlot2: '', offHand1: '', offHand2: '', helmet: '', armor: '', gloves: '', boots: '' },
      pendingReservation: { reservationId: 'res-1', items: [{ lootId: 'sword-1', amount: 1 }] },
      appliedExtractionReceipts: [],
    },
    revision: 1,
    ...overrides,
  };
  doc.markModified = () => {};
  doc.save = async function () { return this; };
  return doc;
}

test('DefeatCommitService.commit', async (t) => {
  const originalFindOne = Character.findOne;
  const originalAuthFindOne = AuthoritativeExtractionResult.findOne;
  const originalAuthFindOneAndUpdate = AuthoritativeExtractionResult.findOneAndUpdate;
  const originalFindOneAndUpdate = Character.findOneAndUpdate;

  t.afterEach(() => {
    Character.findOne = originalFindOne;
    AuthoritativeExtractionResult.findOne = originalAuthFindOne;
    AuthoritativeExtractionResult.findOneAndUpdate = originalAuthFindOneAndUpdate;
    Character.findOneAndUpdate = originalFindOneAndUpdate;
  });

  t.beforeEach(() => {
    AuthoritativeExtractionResult.findOne = async () => ({
      _id: 'auth123',
      raidId: 'raid-1',
      accountId: 'acc123',
      items: [],
      experienceGranted: 500,
      consumed: false
    });

    AuthoritativeExtractionResult.findOneAndUpdate = async (query, update, options) => ({
      _id: 'auth123',
      raidId: 'raid-1',
      accountId: 'acc123',
      items: [],
      experienceGranted: 500,
      consumed: true
    });

    Character.findOneAndUpdate = async (query, update, options) => {
      const char = await Character.findOne(query);
      if (!char) return null;

      const notMatch = query['appliedProgressionReceipts']?.$not?.$elemMatch;
      if (notMatch) {
        const { raidId, resultSequence } = notMatch;
        const exists = (char.appliedProgressionReceipts || []).some(
          r => r.raidId === raidId && r.resultSequence === resultSequence
        );
        if (exists) return null;
      }

      if (update.$set) {
        Object.assign(char, update.$set);
      }
      if (update.$unset) {
        for (const k of Object.keys(update.$unset)) {
          if (k.startsWith('inventory.')) {
            const sub = k.split('.')[1];
            delete char.inventory[sub];
          } else {
            delete char[k];
          }
        }
      }
      if (update.$push?.appliedProgressionReceipts?.$each) {
        char.appliedProgressionReceipts = char.appliedProgressionReceipts || [];
        char.appliedProgressionReceipts.push(...update.$push.appliedProgressionReceipts.$each);
      }
      if (update.$inc?.revision) {
        char.revision = (char.revision || 0) + update.$inc.revision;
      }
      return char;
    };
  });

  await t.test('throws 404 if character does not exist', async () => {
    Character.findOne = async () => null;
    await assert.rejects(
      () => DefeatCommitService.commit('acc_unknown', { raidId: 'raid-1', resultSequence: 1 }),
      (err) => err.statusCode === 404 && err.errorCode === 'CHARACTER_NOT_FOUND'
    );
  });

  await t.test('throws 422 if no AuthoritativeExtractionResult is found', async () => {
    Character.findOne = async () => makeCharacter();
    AuthoritativeExtractionResult.findOne = async () => null;

    await assert.rejects(
      () => DefeatCommitService.commit('acc123', { raidId: 'raid-1', resultSequence: 1 }),
      (err) => err.statusCode === 422 && err.errorCode === 'NO_AUTHORITATIVE_RESULT'
    );
  });

  await t.test('throws 409 if AuthoritativeExtractionResult was already marked consumed', async () => {
    Character.findOne = async () => makeCharacter();
    AuthoritativeExtractionResult.findOne = async () => ({
      raidId: 'raid-1',
      experienceGranted: 500,
      consumed: true
    });

    await assert.rejects(
      () => DefeatCommitService.commit('acc123', { raidId: 'raid-1', resultSequence: 1 }),
      (err) => err.statusCode === 409 && err.errorCode === 'RESULT_ALREADY_CONSUMED'
    );
  });

  await t.test('applies defeat progression, clears reservation, and saves progression receipt', async () => {
    const char = makeCharacter({
      level: 1,
      experience: 0,
      characterAttributes: { vitality: 5, resistance: 5, strength: 5, dexterity: 5, intelligence: 5, luck: 5, availablePoints: 10 }
    });
    Character.findOne = async () => char;

    const result = await DefeatCommitService.commit('acc123', { raidId: 'raid-1', resultSequence: 1 });

    assert.equal(result.alreadySecured, false);
    assert.ok(result.level >= 1);
    assert.ok(result.experience >= 0);
    assert.ok(result.characterAttributes.availablePoints >= 10);
    assert.equal(char.lastAppliedProgressionResultSequence, 1);
    assert.equal(char.lastProgressionReceipt.raidId, 'raid-1');
    assert.equal(char.lastProgressionReceipt.resultSequence, 1);
    assert.equal(char.lastProgressionReceipt.consolidatedExperience, 500);
    assert.equal(char.inventory.pendingReservation, undefined);
  });

  await t.test('idempotent replay of same (raidId, resultSequence) returns alreadySecured: true without mutating', async () => {
    const char = makeCharacter({
      level: 2,
      experience: 100,
      lastAppliedProgressionResultSequence: 1,
      appliedProgressionReceipts: [
        { raidId: 'raid-1', resultSequence: 1, consolidatedExperience: 500, resultingLevel: 2 }
      ],
      lastProgressionReceipt: { raidId: 'raid-1', resultSequence: 1, consolidatedExperience: 500, resultingLevel: 2 }
    });
    Character.findOne = async () => char;

    const result = await DefeatCommitService.commit('acc123', { raidId: 'raid-1', resultSequence: 1 });

    assert.equal(result.alreadySecured, true);
    assert.equal(result.level, 2);
    assert.equal(result.experience, 100);
  });

  await t.test('throws 409 if defeat progression for this raid was already applied with a different sequence', async () => {
    const char = makeCharacter({
      appliedProgressionReceipts: [
        { raidId: 'raid-1', resultSequence: 1, consolidatedExperience: 500, resultingLevel: 2 }
      ]
    });
    Character.findOne = async () => char;

    await assert.rejects(
      () => DefeatCommitService.commit('acc123', { raidId: 'raid-1', resultSequence: 2 }),
      (err) => err.statusCode === 409 && err.errorCode === 'RESULT_ALREADY_CONSUMED'
    );
  });
});

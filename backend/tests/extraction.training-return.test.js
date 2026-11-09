// tests/extraction.training-return.test.js
//
// Doc 09 §15.2 / TASK-279: on Town return the extraction commit returns the training weapon
// to the Trainer once the character owns a normal weapon; otherwise it is kept.

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const Character = require('../src/models/Character');
const AuthoritativeExtractionResult = require('../src/models/AuthoritativeExtractionResult');
const ExtractionCommitService = require('../src/services/ExtractionCommitService');
const { createEquipment } = require('../src/services/ItemInstances');

const EMPTY_RESULT_EQUIPMENT = Object.freeze({
  weaponSlot1: null, weaponSlot2: null, offHand1: null, offHand2: null,
  helmet: null, armor: null, gloves: null, boots: null
});

function stashEntry(lootId, slot, instanceId) {
  return { slot, lootId, amount: 1, instance: { ...createEquipment(lootId).instance, instanceId } };
}

function makeCharacter(stash = []) {
  return {
    accountId: 'acc-training',
    level: 1,
    experience: 0,
    revision: 3,
    schemaVersion: 11,
    lastAppliedProgressionResultSequence: 0,
    lastProgressionReceipt: null,
    appliedProgressionReceipts: [],
    characterAttributes: { vitality: 5, resistance: 5, strength: 5, dexterity: 5, intelligence: 5, luck: 5, availablePoints: 0 },
    inventory: {
      inventorySchemaVersion: 3,
      stash,
      loadout: [],
      preparedEquipment: { ...EMPTY_RESULT_EQUIPMENT },
      pendingReservation: null,
      appliedExtractionReceipts: []
    }
  };
}

function lootIds(character) {
  const equipment = Object.values(character.inventory.preparedEquipment).filter(Boolean).map(entry => entry.lootId);
  return {
    loadout: character.inventory.loadout.map(entry => entry.lootId),
    stash: character.inventory.stash.map(entry => entry.lootId),
    equipment
  };
}

test('ExtractionCommitService.commit training weapon return (TASK-279)', async (t) => {
  const original = {
    findOne: Character.findOne,
    findOneAndUpdate: Character.findOneAndUpdate,
    authFindOne: AuthoritativeExtractionResult.findOne,
    authFindOneAndUpdate: AuthoritativeExtractionResult.findOneAndUpdate
  };
  let character;
  let authResult;

  t.beforeEach(() => {
    Character.findOne = async () => character;
    Character.findOneAndUpdate = async (filter, update) => {
      if (filter.revision !== character.revision) return null;
      const next = structuredClone(character);
      for (const [path, value] of Object.entries(update.$set || {})) {
        const parts = path.split('.');
        let cursor = next;
        for (let i = 0; i < parts.length - 1; i++) cursor = cursor[parts[i]];
        cursor[parts[parts.length - 1]] = structuredClone(value);
      }
      next.revision += 1;
      next.inventory.appliedExtractionReceipts.push(...update.$push['inventory.appliedExtractionReceipts'].$each);
      character = next;
      return character;
    };
    AuthoritativeExtractionResult.findOne = async () => authResult;
    AuthoritativeExtractionResult.findOneAndUpdate = async () => ({ ...authResult, consumed: true });
  });

  t.afterEach(() => {
    Character.findOne = original.findOne;
    Character.findOneAndUpdate = original.findOneAndUpdate;
    AuthoritativeExtractionResult.findOne = original.authFindOne;
    AuthoritativeExtractionResult.findOneAndUpdate = original.authFindOneAndUpdate;
  });

  const commit = () => ExtractionCommitService.commit('acc-training', { raidId: 'raid-training', resultSequence: 1 });

  await t.test('extracting a normal weapon removes the equipped training weapon', async () => {
    character = makeCharacter();
    authResult = {
      items: [{ lootId: 'arming_sword', amount: 1 }],
      preparedEquipment: { ...EMPTY_RESULT_EQUIPMENT, weaponSlot1: { lootId: 'training_dagger' } },
      experienceGranted: 0,
      consumed: false
    };

    const result = await commit();

    assert.equal(result.alreadySecured, false);
    assert.deepEqual(lootIds(character), { loadout: ['arming_sword'], stash: [], equipment: [] });
    assert.deepEqual(result.loadout.map(entry => entry.lootId), ['arming_sword']);
    assert.equal(character.revision, 4);
  });

  await t.test('extracting without any normal weapon keeps the training weapon', async () => {
    character = makeCharacter();
    authResult = {
      items: [{ lootId: 'RES_BONE', amount: 2 }],
      preparedEquipment: { ...EMPTY_RESULT_EQUIPMENT, weaponSlot1: { lootId: 'training_dagger' } },
      experienceGranted: 0,
      consumed: false
    };

    await commit();

    assert.deepEqual(lootIds(character), { loadout: ['RES_BONE'], stash: [], equipment: ['training_dagger'] });
  });

  await t.test('a normal weapon kept only in the stash removes the extracted training weapon', async () => {
    character = makeCharacter([stashEntry('rapier', 0, '0123456789abcdef01234567')]);
    authResult = {
      items: [{ lootId: 'training_wand', amount: 1 }],
      preparedEquipment: { ...EMPTY_RESULT_EQUIPMENT },
      experienceGranted: 0,
      consumed: false
    };

    await commit();

    assert.deepEqual(lootIds(character), { loadout: [], stash: ['rapier'], equipment: [] });
  });

  await t.test('a training weapon left in the stash is returned when a normal weapon is extracted', async () => {
    character = makeCharacter([stashEntry('training_short_sword', 0, '0123456789abcdef01234567')]);
    authResult = {
      items: [{ lootId: 'rapier', amount: 1 }],
      preparedEquipment: { ...EMPTY_RESULT_EQUIPMENT },
      experienceGranted: 0,
      consumed: false
    };

    await commit();

    assert.deepEqual(lootIds(character), { loadout: ['rapier'], stash: [], equipment: [] });
  });
});

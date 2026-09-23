// tests/progression.concurrency.test.js

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const Character = require('../src/models/Character');
const ProgressionService = require('../src/services/ProgressionService');

const VALID_ACCOUNT_ID = new mongoose.Types.ObjectId().toHexString();

function makeCharacter(overrides = {}) {
  const doc = {
    accountId: VALID_ACCOUNT_ID,
    revision: 0,
    characterAttributes: {
      vitality: 5, resistance: 5, strength: 5,
      dexterity: 5, intelligence: 5, luck: 5,
      availablePoints: 10,
    },
    ...overrides,
  };
  doc.markModified = () => {};
  doc.save = async function () { return this; };
  return doc;
}

test('ProgressionService.commitProgression with concurrency/revision', async (t) => {
  const originalFindOne = Character.findOne;
  const originalFindOneAndUpdate = Character.findOneAndUpdate;

  t.afterEach(() => {
    Character.findOne = originalFindOne;
    Character.findOneAndUpdate = originalFindOneAndUpdate;
  });

  await t.test('Commit exitoso incrementa la revisión', async () => {
    const mockChar = makeCharacter({ revision: 5, characterAttributes: { vitality: 5, availablePoints: 5 } });
    
    Character.findOne = async () => mockChar;

    Character.findOneAndUpdate = async (query, update, options) => {
      assert.equal(query.accountId, VALID_ACCOUNT_ID);
      assert.equal(query.revision, 5);
      
      mockChar.characterAttributes.vitality += update.$inc['characterAttributes.vitality'] || 0;
      mockChar.characterAttributes.availablePoints += update.$inc['characterAttributes.availablePoints'] || 0;
      mockChar.revision += update.$inc.revision || 0;
      return mockChar;
    };

    const result = await ProgressionService.commitProgression(VALID_ACCOUNT_ID, {
      attribute: 'vitality',
      expectedRevision: 5
    });

    assert.equal(result.revision, 6);
    assert.equal(result.characterAttributes.vitality, 6);
    assert.equal(result.characterAttributes.availablePoints, 4);
  });

  await t.test('Commit con expectedRevision stale devuelve 409 REVISION_CONFLICT', async () => {
    Character.findOneAndUpdate = async (query, update, options) => {
      // Simula que la condición { revision: expectedRevision } no coincide (o doc no existe)
      return null;
    };
    
    Character.findOne = async (query) => {
      // Simula que el personaje SÍ existe en DB, pero su revision es diferente (por eso falló el update)
      return makeCharacter({ revision: 10 });
    };

    await assert.rejects(
      () => ProgressionService.commitProgression(VALID_ACCOUNT_ID, {
        attribute: 'vitality',
        expectedRevision: 5 // stale
      }),
      err => {
        assert.equal(err.statusCode, 409);
        assert.equal(err.errorCode, 'REVISION_CONFLICT');
        return true;
      }
    );
  });

  await t.test('Sin expectedRevision devuelve 400 REVISION_REQUIRED', async () => {
    Character.findOne = async () => makeCharacter();

    await assert.rejects(
      () => ProgressionService.commitProgression(VALID_ACCOUNT_ID, {
        attribute: 'vitality'
      }),
      err => {
        assert.equal(err.statusCode, 400);
        assert.equal(err.errorCode, 'REVISION_REQUIRED');
        return true;
      }
    );
  });
  
  await t.test('Fallo porque no encuentra el Character (borrado) devuelve 404', async () => {
    Character.findOneAndUpdate = async (query, update, options) => {
      return null;
    };
    Character.findOne = async (query) => {
      return null; // character no existe
    };

    await assert.rejects(
      () => ProgressionService.commitProgression(VALID_ACCOUNT_ID, {
        attribute: 'vitality',
        expectedRevision: 0
      }),
      err => {
        assert.equal(err.statusCode, 404);
        assert.equal(err.errorCode, 'CHARACTER_NOT_FOUND');
        return true;
      }
    );
  });
});

const test = require('node:test');
const assert = require('node:assert');

const Character = require('../src/models/Character');
const ProgressionService = require('../src/services/ProgressionService');

test('Progression Attributes - Authoritative Assignment', async (t) => {
  const originalFindOne = Character.findOne;
  const originalFindOneAndUpdate = Character.findOneAndUpdate;

  t.afterEach(() => {
    Character.findOne = originalFindOne;
    Character.findOneAndUpdate = originalFindOneAndUpdate;
  });

  await t.test('should successfully increment an attribute and decrement available points', async () => {
    Character.findOne = async () => ({
      accountId: 'acc123',
      characterAttributes: {
        vitality: 5, resistance: 5, strength: 5,
        dexterity: 5, intelligence: 5, luck: 5, availablePoints: 10
      }
    });

    Character.findOneAndUpdate = async (query, updateDoc, options) => {
      // Simulate the inc
      return {
        characterAttributes: {
          vitality: 6, resistance: 5, strength: 5,
          dexterity: 5, intelligence: 5, luck: 5, availablePoints: 9
        }
      };
    };

    const result = await ProgressionService.commitProgression('acc123', { attribute: 'Vitality', expectedRevision: 0 });

    assert.strictEqual(result.characterAttributes.vitality, 6);
    assert.strictEqual(result.characterAttributes.availablePoints, 9);
  });

  await t.test('should throw 400 if attribute name is missing', async () => {
    Character.findOne = async () => ({});
    try {
      await ProgressionService.commitProgression('acc123', { expectedRevision: 0 });
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 400);
      assert.strictEqual(err.errorCode, 'INVALID_ATTRIBUTE');
    }
  });

  await t.test('should throw 400 if attribute name is invalid', async () => {
    Character.findOne = async () => ({});
    try {
      await ProgressionService.commitProgression('acc123', { attribute: 'InvalidAttribute', expectedRevision: 0 });
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 400);
      assert.strictEqual(err.errorCode, 'INVALID_ATTRIBUTE');
    }
  });

  await t.test('should throw 422 if no available points', async () => {
    Character.findOne = async () => ({
      accountId: 'acc123',
      characterAttributes: {
        strength: 5,
        availablePoints: 0
      }
    });

    try {
      await ProgressionService.commitProgression('acc123', { attribute: 'Strength', expectedRevision: 0 });
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 422);
      assert.strictEqual(err.errorCode, 'NO_AVAILABLE_POINTS');
    }
  });

  await t.test('should throw 422 if attribute is at max value', async () => {
    const { MAX_ATTRIBUTE_VALUE } = require('../src/config/progressionBalance');
    Character.findOne = async () => ({
      accountId: 'acc123',
      characterAttributes: {
        strength: MAX_ATTRIBUTE_VALUE,
        availablePoints: 10
      }
    });

    try {
      await ProgressionService.commitProgression('acc123', { attribute: 'Strength', expectedRevision: 0 });
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 422);
      assert.strictEqual(err.errorCode, 'ATTRIBUTE_MAXED');
    }
  });

  await t.test('should handle case insensitivity of the attribute name', async () => {
    Character.findOne = async () => ({
      accountId: 'acc123',
      characterAttributes: {
        intelligence: 5,
        availablePoints: 10
      }
    });

    Character.findOneAndUpdate = async (query, updateDoc, options) => {
      return {
        characterAttributes: {
          intelligence: 6, availablePoints: 9
        }
      };
    };

    const result = await ProgressionService.commitProgression('acc123', { attribute: 'inTeLLigence', expectedRevision: 0 });

    assert.strictEqual(result.characterAttributes.intelligence, 6);
    assert.strictEqual(result.characterAttributes.availablePoints, 9);
  });
});

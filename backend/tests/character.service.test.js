const test = require('node:test');
const assert = require('node:assert');

const Character = require('../src/models/Character');
const CharacterService = require('../src/services/CharacterService');

test('CharacterService', async (t) => {
  const originalFindOne = Character.findOne;
  const originalSave = Character.prototype.save;

  t.afterEach(() => {
    Character.findOne = originalFindOne;
    Character.prototype.save = originalSave;
  });

  await t.test('getByAccountId() - should return character if found', async () => {
    const mockCharacter = { _id: 'char123', accountId: 'acc123', name: 'Alpha' };
    Character.findOne = async () => mockCharacter;

    const result = await CharacterService.getByAccountId('acc123');
    
    assert.strictEqual(result._id, 'char123');
    assert.strictEqual(result.name, 'Alpha');
  });

  await t.test('getByAccountId() - should throw 404 if character not found', async () => {
    Character.findOne = async () => null;

    try {
      await CharacterService.getByAccountId('acc_missing');
      assert.fail('Should have thrown an error');
    } catch (error) {
      assert.strictEqual(error.statusCode, 404);
      assert.strictEqual(error.errorCode, 'CHARACTER_NOT_FOUND');
    }
  });

  await t.test('updateProfile() - should update customNote and lastSeen', async () => {
    // Create a dummy character object with a save method to mock mongoose document
    const mockCharacter = { 
      _id: 'char123', 
      accountId: 'acc123', 
      profile: { customNote: 'old', lastSeen: null },
      save: async function() { return this; }
    };
    
    Character.findOne = async () => mockCharacter;

    const profileData = { customNote: 'new note' };
    const result = await CharacterService.updateProfile('acc123', profileData);

    assert.strictEqual(result.profile.customNote, 'new note');
    assert.ok(result.profile.lastSeen instanceof Date);
  });
});

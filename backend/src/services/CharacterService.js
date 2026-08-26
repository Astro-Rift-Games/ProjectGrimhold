// src/services/CharacterService.js
const Character = require('../models/Character');

class CharacterService {
  /**
   * Creates an initial character for the given account.
   * @throws 409 if a character already exists for this account.
   */
  static async createForAccount(accountId, name) {
    const existingCharacter = await Character.findOne({ accountId });
    if (existingCharacter) {
      throw {
        statusCode: 409,
        errorCode: 'CHARACTER_ALREADY_EXISTS',
        message: 'Account already has a character'
      };
    }

    const character = new Character({
      accountId,
      name,
      profile: {
        lastSeen: null,
        customNote: ''
      }
    });

    await character.save();
    return {
      characterId: character._id.toString(),
      name: character.name
    };
  }
  /**
   * Retrieves the character associated with the given accountId.
   * @throws 404 if character is not found.
   */
  static async getByAccountId(accountId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw {
        statusCode: 404,
        errorCode: 'CHARACTER_NOT_FOUND',
        message: 'No character found for this account.'
      };
    }
    return character;
  }

  /**
   * Updates the profile of the character associated with the given accountId.
   */
  static async updateProfile(accountId, profileData) {
    const character = await this.getByAccountId(accountId);
    
    if (profileData.customNote !== undefined) {
      character.profile.customNote = profileData.customNote;
    }
    character.profile.lastSeen = new Date();
    
    await character.save();
    return character;
  }
}

module.exports = CharacterService;

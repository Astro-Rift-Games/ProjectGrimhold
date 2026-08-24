// src/services/CharacterService.js
const Character = require('../models/Character');

class CharacterService {
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

// src/services/ProgressionService.js
const Character = require('../models/Character');

class ProgressionService {
  /**
   * Retrieves the progression state for the given account's character.
   * @throws 404 if no character exists for this account.
   */
  static async getProgression(accountId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    return {
      level: character.level,
      experience: character.experience,
      lastAppliedProgressionResultSequence: character.lastAppliedProgressionResultSequence,
      characterAttributes: character.characterAttributes || {
        vitality: 0, resistance: 0, strength: 0,
        dexterity: 0, intelligence: 0, luck: 0, availablePoints: 0
      }
    };
  }

}

module.exports = ProgressionService;

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
        vitality: 5, resistance: 5, strength: 5,
        dexterity: 5, intelligence: 5, luck: 5, availablePoints: 10
      }
    };
  }
  static async commitProgression(accountId, payload) {
    let updateDoc = {};
    if (payload.characterAttributes) {
      updateDoc['characterAttributes'] = payload.characterAttributes;
    }

    if (Object.keys(updateDoc).length === 0) {
      // Nothing to update
      const character = await Character.findOne({ accountId });
      if (!character) {
        throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found.' };
      }
      return {
        alreadyApplied: false,
        level: character.level,
        experience: character.experience
      };
    }

    const updated = await Character.findOneAndUpdate(
      { accountId },
      { $set: updateDoc },
      { new: true }
    );

    if (!updated) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found.' };
    }

    return {
      alreadyApplied: false,
      level: updated.level,
      experience: updated.experience
    };
  }
}

module.exports = ProgressionService;

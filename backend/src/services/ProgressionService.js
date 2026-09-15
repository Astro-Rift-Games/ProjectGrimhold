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
    const { MAX_ATTRIBUTE_VALUE } = require('../config/progressionBalance');
    
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found.' };
    }

    if (!payload || !payload.attribute) {
      throw { statusCode: 400, errorCode: 'INVALID_ATTRIBUTE', message: 'Attribute is required.' };
    }

    const attributeName = payload.attribute.toLowerCase();
    const validAttributes = ['vitality', 'resistance', 'strength', 'dexterity', 'intelligence', 'luck'];

    if (!validAttributes.includes(attributeName)) {
      throw { statusCode: 400, errorCode: 'INVALID_ATTRIBUTE', message: `Invalid attribute: ${payload.attribute}` };
    }

    const attrs = character.characterAttributes || {
      vitality: 5, resistance: 5, strength: 5,
      dexterity: 5, intelligence: 5, luck: 5, availablePoints: 10
    };

    if (attrs.availablePoints <= 0) {
      throw { statusCode: 422, errorCode: 'NO_AVAILABLE_POINTS', message: 'No available attribute points.' };
    }

    if (attrs[attributeName] >= MAX_ATTRIBUTE_VALUE) {
      throw { statusCode: 422, errorCode: 'ATTRIBUTE_MAXED', message: `Attribute ${payload.attribute} is already at the maximum value.` };
    }

    const updateQuery = {
      $inc: {
        [`characterAttributes.${attributeName}`]: 1,
        'characterAttributes.availablePoints': -1
      }
    };

    const updated = await Character.findOneAndUpdate(
      { accountId },
      updateQuery,
      { new: true }
    );

    return {
      characterAttributes: updated.characterAttributes
    };
  }
}

module.exports = ProgressionService;

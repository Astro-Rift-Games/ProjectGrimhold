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
      },
      revision: character.revision || 0
    };
  }
  static async commitProgression(accountId, payload) {
    const { MAX_ATTRIBUTE_VALUE } = require('../config/progressionBalance');
    
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found.' };
    }

    if (!payload || typeof payload.attribute !== 'string' || payload.attribute.trim() === '') {
      throw { statusCode: 400, errorCode: 'INVALID_ATTRIBUTE', message: 'Attribute must be a non-empty string.' };
    }

    if (typeof payload.expectedRevision !== 'number' || payload.expectedRevision < 0 || !Number.isInteger(payload.expectedRevision)) {
      throw { statusCode: 400, errorCode: 'INVALID_REVISION', message: 'expectedRevision must be a non-negative integer.' };
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
        'characterAttributes.availablePoints': -1,
        revision: 1
      }
    };

    const updated = await Character.findOneAndUpdate(
      { 
        accountId, 
        revision: payload.expectedRevision,
        'characterAttributes.availablePoints': { $gt: 0 } 
      },
      updateQuery,
      { new: true }
    );

    if (!updated) {
      // It might have failed because the revision was wrong, or character deleted,
      // or availablePoints became 0 due to concurrent requests.
      const exists = await Character.findOne({ accountId }, null, { lean: true });
      if (!exists) {
        throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found.' };
      }
      
      const currentAttrs = exists.characterAttributes;
      if (!currentAttrs || typeof currentAttrs.availablePoints !== 'number') {
        console.error(`[ProgressionService] Legacy document detected without characterAttributes.availablePoints. AccountId: ${accountId}`);
        throw { statusCode: 500, errorCode: 'LEGACY_DOCUMENT_UNNORMALIZED', message: 'Legacy document is missing required attributes.' };
      }
      
      if (currentAttrs.availablePoints <= 0) {
        throw { statusCode: 422, errorCode: 'NO_AVAILABLE_POINTS', message: 'No available attribute points.' };
      }

      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }

    return {
      characterAttributes: updated.characterAttributes,
      revision: updated.revision
    };
  }
}

module.exports = ProgressionService;

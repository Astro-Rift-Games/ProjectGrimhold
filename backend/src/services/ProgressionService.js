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

  /**
   * Persists the character's progression after a raid (extraction, defeat, etc).
   * 
   * Idempotent: if the same (raidId, resultSequence) pair is provided, it returns
   * { alreadyApplied: true } without mutating the document.
   */
  static async commitProgression(accountId, payload) {
    const MAX_PROGRESSION_RECEIPTS = 256;
    const {
      raidId, resultSequence, consolidatedExperience, resultingLevel,
      newLevel, newExperience, characterAttributes
    } = payload;

    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    // Check idempotency against watermark or applied receipts
    if (resultSequence <= character.lastAppliedProgressionResultSequence) {
      if (
        character.lastProgressionReceipt &&
        character.lastProgressionReceipt.raidId === raidId &&
        character.lastProgressionReceipt.resultSequence === resultSequence
      ) {
        return {
          alreadyApplied: true,
          level: character.level,
          experience: character.experience
        };
      }
      
      const alreadyApplied = character.appliedProgressionReceipts.some(
        r => r.raidId === raidId && r.resultSequence === resultSequence
      );
      if (alreadyApplied) {
        return {
          alreadyApplied: true,
          level: character.level,
          experience: character.experience
        };
      }
    }

    // Apply progression updates
    character.level = newLevel;
    character.experience = newExperience;
    character.lastAppliedProgressionResultSequence = resultSequence;
    
    if (characterAttributes) {
      character.characterAttributes = characterAttributes;
    }

    const receipt = {
      raidId,
      resultSequence,
      consolidatedExperience,
      resultingLevel
    };

    character.lastProgressionReceipt = receipt;
    character.appliedProgressionReceipts.push(receipt);

    // Evict oldest entries beyond the cap
    while (character.appliedProgressionReceipts.length > MAX_PROGRESSION_RECEIPTS) {
      character.appliedProgressionReceipts.shift();
    }

    await character.save();

    return {
      alreadyApplied: false,
      level: character.level,
      experience: character.experience
    };
  }
}

module.exports = ProgressionService;

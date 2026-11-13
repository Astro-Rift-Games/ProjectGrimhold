// src/services/DefeatCommitService.js
//
// Authoritative, idempotent commit of expedition progression following a Defeat.
//
// Design invariants:
//   - Idempotent: same (raidId, resultSequence) → alreadySecured = true, no mutation.
//   - Atomic: defeat progression applied and pendingReservation cleared in a single findOneAndUpdate call.
//   - Authoritative: the server independently recalculates level/XP/attribute points from the authoritative raid result.

'use strict';

const Character = require('../models/Character');
const AuthoritativeExtractionResult = require('../models/AuthoritativeExtractionResult');
const { requireMigratedInventory } = require('./InventoryEntries');
const {
  computeLevelAndExperience,
  computeAttributePointsGranted,
} = require('../config/progressionBalance');

const MAX_PROGRESSION_RECEIPTS = 256;

function serializeAttributes(attrs) {
  if (!attrs) {
    return { vitality: 0, resistance: 0, strength: 0, dexterity: 0, intelligence: 0, luck: 0, availablePoints: 0 };
  }
  return {
    vitality: attrs.vitality || 0,
    resistance: attrs.resistance || 0,
    strength: attrs.strength || 0,
    dexterity: attrs.dexterity || 0,
    intelligence: attrs.intelligence || 0,
    luck: attrs.luck || 0,
    availablePoints: attrs.availablePoints || 0,
  };
}

class DefeatCommitService {
  static async commit(accountId, payload) {
    const { raidId, resultSequence } = payload || {};

    if (!raidId || typeof raidId !== 'string' || raidId.trim() === '') {
      throw {
        statusCode: 400,
        errorCode: 'INVALID_PAYLOAD',
        message: 'raidId is required.',
      };
    }

    if (!Number.isInteger(resultSequence) || resultSequence < 1) {
      throw {
        statusCode: 400,
        errorCode: 'INVALID_PAYLOAD',
        message: 'resultSequence must be a positive integer.',
      };
    }

    // ------------------------------------------------------------------
    // 1. Authoritative Lookup
    // ------------------------------------------------------------------
    let authResult = await AuthoritativeExtractionResult.findOne({ raidId, accountId });
    if (!authResult) {
      // In case the commit arrived slightly before the host finished publishing,
      // poll briefly (up to 500ms) before rejecting.
      for (let attempt = 0; attempt < 5 && !authResult; attempt++) {
        await new Promise(resolve => setTimeout(resolve, 100));
        authResult = await AuthoritativeExtractionResult.findOne({ raidId, accountId });
      }
    }

    if (!authResult) {
      throw {
        statusCode: 422,
        errorCode: 'NO_AUTHORITATIVE_RESULT',
        message: 'No authoritative defeat result found for this raid. Client payloads are not trusted.',
      };
    }

    // ------------------------------------------------------------------
    // 2. Pre-fetch character and enforce single consumption
    // ------------------------------------------------------------------
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw {
        statusCode: 404,
        errorCode: 'CHARACTER_NOT_FOUND',
        message: 'No character found for this account.',
      };
    }

    requireMigratedInventory(character);

    const progressionReceipts = character.appliedProgressionReceipts || [];
    const exactReceiptMatch = progressionReceipts.some(
      r => r.raidId === raidId && r.resultSequence === resultSequence
    );

    if (exactReceiptMatch) {
      return {
        alreadySecured: true,
        level: character.level,
        experience: character.experience,
        characterAttributes: serializeAttributes(character.characterAttributes),
        revision: character.revision || 0
      };
    }

    if (authResult.consumed) {
      throw {
        statusCode: 409,
        errorCode: 'RESULT_ALREADY_CONSUMED',
        message: 'This authoritative result has already been consumed and cannot be claimed again.',
      };
    }

    const raidAlreadyApplied = progressionReceipts.some(r => r.raidId === raidId);
    if (raidAlreadyApplied) {
      throw {
        statusCode: 409,
        errorCode: 'RESULT_ALREADY_CONSUMED',
        message: 'A progression receipt for this raid has already been committed and cannot be claimed with a different sequence.',
      };
    }

    // ------------------------------------------------------------------
    // 3. Atomically claim authoritative result (distributed lock)
    // ------------------------------------------------------------------
    const authQuery = authResult._id ? { _id: authResult._id } : { raidId, accountId };
    const claimedResult = await AuthoritativeExtractionResult.findOneAndUpdate(
      { ...authQuery, consumed: { $ne: true } },
      { $set: { consumed: true, consumedAt: new Date() } },
      { new: true }
    );

    if (!claimedResult) {
      let refreshedChar = await Character.findOne({ accountId });
      let alreadyApplied = (refreshedChar?.appliedProgressionReceipts || []).some(
        r => r.raidId === raidId && r.resultSequence === resultSequence
      );

      for (let attempt = 0; attempt < 6 && !alreadyApplied; attempt++) {
        await new Promise(resolve => setTimeout(resolve, 50));
        refreshedChar = await Character.findOne({ accountId });
        alreadyApplied = (refreshedChar?.appliedProgressionReceipts || []).some(
          r => r.raidId === raidId && r.resultSequence === resultSequence
        );
      }

      if (alreadyApplied) {
        return {
          alreadySecured: true,
          level: refreshedChar.level,
          experience: refreshedChar.experience,
          characterAttributes: serializeAttributes(refreshedChar.characterAttributes),
          revision: refreshedChar.revision || 0
        };
      }

      throw {
        statusCode: 409,
        errorCode: 'RESULT_ALREADY_CONSUMED',
        message: 'This authoritative result has already been consumed.',
      };
    }

    // ------------------------------------------------------------------
    // 4. Compute new progression states in memory
    // ------------------------------------------------------------------
    const experienceToGrant = Math.max(0, Number(authResult.experienceGranted || 0));
    const computed = computeLevelAndExperience(
      character.level,
      character.experience,
      experienceToGrant
    );
    const pointsGranted = computeAttributePointsGranted(character.level, computed.resultingLevel);
    const newAvailablePoints = (character.characterAttributes?.availablePoints || 0) + pointsGranted;

    const progressionReceipt = {
      raidId,
      resultSequence,
      consolidatedExperience: experienceToGrant,
      resultingLevel: computed.resultingLevel,
    };

    // ------------------------------------------------------------------
    // 5. Atomic Database Update on Character
    // ------------------------------------------------------------------
    const updateDoc = {
      $unset: { 'inventory.pendingReservation': 1 },
      $set: {
        level: computed.resultingLevel,
        experience: computed.resultingExperience,
        'characterAttributes.availablePoints': newAvailablePoints,
        lastAppliedProgressionResultSequence: resultSequence,
        lastProgressionReceipt: progressionReceipt
      },
      $inc: { revision: 1 },
      $push: {
        appliedProgressionReceipts: {
          $each: [progressionReceipt],
          $slice: -MAX_PROGRESSION_RECEIPTS
        }
      }
    };

    const characterFilter = {
      accountId,
      appliedProgressionReceipts: {
        $not: {
          $elemMatch: { raidId, resultSequence }
        }
      }
    };

    const updatedCharacter = await Character.findOneAndUpdate(
      characterFilter,
      updateDoc,
      { new: true }
    );

    if (!updatedCharacter) {
      const refreshedChar = await Character.findOne({ accountId });
      const alreadyApplied = (refreshedChar?.appliedProgressionReceipts || []).some(
        r => r.raidId === raidId && r.resultSequence === resultSequence
      );

      if (alreadyApplied) {
        return {
          alreadySecured: true,
          level: refreshedChar.level,
          experience: refreshedChar.experience,
          characterAttributes: serializeAttributes(refreshedChar.characterAttributes),
          revision: refreshedChar.revision || 0
        };
      }

      // Roll back lock on unexpected conflict so client can retry cleanly
      await AuthoritativeExtractionResult.findOneAndUpdate(
        { ...authQuery, consumed: true },
        { $set: { consumed: false, consumedAt: null } }
      );

      throw {
        statusCode: 409,
        errorCode: 'CONCURRENT_MUTATION',
        message: 'Could not commit defeat progression due to a concurrent update. Please retry.',
      };
    }

    return {
      alreadySecured: false,
      level: updatedCharacter.level,
      experience: updatedCharacter.experience,
      characterAttributes: serializeAttributes(updatedCharacter.characterAttributes),
      revision: updatedCharacter.revision
    };
  }
}

module.exports = DefeatCommitService;

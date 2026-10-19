// src/services/ExtractionCommitService.js
//
// Unified, authoritative commit of raid loot and progression.
//
// Design invariants:
//   - Idempotent: same (raidId, resultSequence) → alreadySecured = true, no mutation.
//   - Atomic: loot + progression applied in a single findOneAndUpdate call with DB-level lock.
//   - Authoritative: the server independently recalculates level/XP/attribute points and pulls loot from Fusion results.

'use strict';

const Character = require('../models/Character');
const AuthoritativeExtractionResult = require('../models/AuthoritativeExtractionResult');
const {
  normalizeCharacterInventory,
  normalizeItems,
  normalizePreparedEquipment
} = require('./InventoryLootIdNormalizer');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');
const {
  computeLevelAndExperience,
  computeAttributePointsGranted,
} = require('../config/progressionBalance');

// Maximum number of extraction receipts kept in history (mirrors Unity's cap).
const MAX_EXTRACTION_RECEIPTS = 256;
// Maximum number of progression receipts kept in history.
const MAX_PROGRESSION_RECEIPTS = 256;

function sanitizePreparedEquipment(eq) {
  if (!eq) return {};
  const base = {};
  for (const slot of EQUIPMENT_SLOTS) {
    base[slot] = eq[slot] || '';
  }
  return normalizePreparedEquipment(base);
}

class ExtractionCommitService {
  static async commit(accountId, payload) {
    const { raidId, resultSequence } = payload;

    // ------------------------------------------------------------------
    // 1. Authoritative Lookup
    // ------------------------------------------------------------------
    // We do NOT trust client payloads for items or experience ideally,
    // but in Stage 1 we fallback to the client payload if the mock webhook isn't used.
    let authResult = await AuthoritativeExtractionResult.findOne({ raidId, accountId });
    if (!authResult) {
      throw {
        statusCode: 422,
        errorCode: 'NO_AUTHORITATIVE_RESULT',
        message: 'No authoritative extraction result found for this raid. Client payloads are no longer trusted.',
      };
    }

    // ------------------------------------------------------------------
    // 2. Pre-fetch character and enforce single consumption
    // ------------------------------------------------------------------
    let character = await Character.findOne({ accountId });
    if (!character) {
      throw {
        statusCode: 404,
        errorCode: 'CHARACTER_NOT_FOUND',
        message: 'No character found for this account.',
      };
    }

    if (normalizeCharacterInventory(character)) {
      await character.save();
    }

    const extractionReceipts = character.inventory.appliedExtractionReceipts || [];
    const exactReceiptMatch = extractionReceipts.some(
      r => r.raidId === raidId && r.resultSequence === resultSequence
    );

    if (exactReceiptMatch) {
      return {
        alreadySecured:      true,
        loadout:             serializeItems(character.inventory.loadout),
        level:               character.level,
        experience:          character.experience,
        characterAttributes: serializeAttributes(character.characterAttributes),
        revision:            character.revision || 0
      };
    }

    // If result was already marked consumed in database
    if (authResult.consumed) {
      throw {
        statusCode: 409,
        errorCode:  'RESULT_ALREADY_CONSUMED',
        message:    'This authoritative extraction result has already been consumed and cannot be claimed again.',
      };
    }

    // If character already recorded an extraction from this raid with another sequence
    const raidAlreadyApplied = extractionReceipts.some(r => r.raidId === raidId);
    if (raidAlreadyApplied) {
      throw {
        statusCode: 409,
        errorCode:  'RESULT_ALREADY_CONSUMED',
        message:    'An extraction for this raid has already been committed and cannot be claimed with a different sequence.',
      };
    }

    if (character.inventory.loadout && character.inventory.loadout.length > 0) {
      throw {
        statusCode: 409,
        errorCode:  'LOADOUT_NOT_EMPTY',
        message:    'Cannot commit extraction while loadout is not empty.',
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
      let alreadyApplied = (refreshedChar?.inventory?.appliedExtractionReceipts || []).some(
        r => r.raidId === raidId && r.resultSequence === resultSequence
      );

      // If concurrent request is currently persisting the character, wait briefly
      for (let attempt = 0; attempt < 6 && !alreadyApplied; attempt++) {
        await new Promise(resolve => setTimeout(resolve, 50));
        refreshedChar = await Character.findOne({ accountId });
        alreadyApplied = (refreshedChar?.inventory?.appliedExtractionReceipts || []).some(
          r => r.raidId === raidId && r.resultSequence === resultSequence
        );
      }

      if (alreadyApplied) {
        return {
          alreadySecured:      true,
          loadout:             serializeItems(refreshedChar.inventory.loadout),
          level:               refreshedChar.level,
          experience:          refreshedChar.experience,
          characterAttributes: serializeAttributes(refreshedChar.characterAttributes),
          revision:            refreshedChar.revision || 0
        };
      }

      throw {
        statusCode: 409,
        errorCode:  'RESULT_ALREADY_CONSUMED',
        message:    'This authoritative extraction result has already been consumed.',
      };
    }

    // ------------------------------------------------------------------
    // 4. Compute new states in memory
    // ------------------------------------------------------------------
    
    // Loot
    const newLoadout = [];
    const normalizedItems = normalizeItems(authResult.items);
    if (normalizedItems.length > 0) {
      for (const item of normalizedItems) {
        const existing = newLoadout.find(i => i.lootId === item.lootId);
        if (existing) {
          existing.amount += item.amount;
        } else {
          newLoadout.push({ lootId: item.lootId, amount: item.amount });
        }
      }
    }

    // Progression
    const computed = computeLevelAndExperience(
      character.level,
      character.experience,
      authResult.experienceGranted
    );
    const pointsGranted = computeAttributePointsGranted(character.level, computed.resultingLevel);
    const newAvailablePoints = (character.characterAttributes?.availablePoints || 0) + pointsGranted;

    const progressionReceipt = {
      raidId,
      resultSequence,
      consolidatedExperience: authResult.experienceGranted,
      resultingLevel: computed.resultingLevel,
    };

    // Prepared Equipment
    const newPreparedEquipment = sanitizePreparedEquipment(
      authResult.preparedEquipment || 
      character.inventory.pendingReservation?.preparedEquipment
    );

    // ------------------------------------------------------------------
    // 5. Atomic Database Update on Character
    // ------------------------------------------------------------------
    
    const updateDoc = {
      $unset: { 'inventory.pendingReservation': 1 },
      $set: { 
        'inventory.preparedEquipment': newPreparedEquipment,
        'inventory.loadout': newLoadout,
        level: computed.resultingLevel,
        experience: computed.resultingExperience,
        'characterAttributes.availablePoints': newAvailablePoints,
        lastAppliedProgressionResultSequence: resultSequence,
        lastProgressionReceipt: progressionReceipt
      },
      $inc: { revision: 1 },
      $push: {
        'inventory.appliedExtractionReceipts': {
          $each: [{
            raidId,
            resultSequence,
            resultId: authResult.resultId || `${raidId}:${resultSequence}`,
            timestamp: new Date()
          }],
          $slice: -MAX_EXTRACTION_RECEIPTS
        },
        'appliedProgressionReceipts': {
          $each: [progressionReceipt],
          $slice: -MAX_PROGRESSION_RECEIPTS
        }
      }
    };

    const updatedCharacter = await Character.findOneAndUpdate(
      {
        accountId: accountId,
        revision: character.revision,
        'inventory.appliedExtractionReceipts': { 
          $not: { $elemMatch: { raidId: raidId, resultSequence: resultSequence } } 
        }
      },
      updateDoc,
      { new: true }
    );

    if (!updatedCharacter) {
      // Re-fetch to return the newly secured state or release claim on conflict
      let refreshedChar = await Character.findOne({ accountId });
      if (!refreshedChar) {
        await AuthoritativeExtractionResult.findOneAndUpdate(
          authQuery,
          { $set: { consumed: false, consumedAt: null } }
        );
        throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'Character not found after commit.' };
      }

      const lootAlreadyAppliedNow = (refreshedChar.inventory.appliedExtractionReceipts || []).some(
        r => r.raidId === raidId && r.resultSequence === resultSequence
      );

      if (lootAlreadyAppliedNow) {
        return {
          alreadySecured:      true,
          loadout:             serializeItems(refreshedChar.inventory.loadout),
          level:               refreshedChar.level,
          experience:          refreshedChar.experience,
          characterAttributes: serializeAttributes(refreshedChar.characterAttributes),
          revision:            refreshedChar.revision || 0
        };
      }

      // Roll back lock on revision conflict so client can retry cleanly
      await AuthoritativeExtractionResult.findOneAndUpdate(
        authQuery,
        { $set: { consumed: false, consumedAt: null } }
      );

      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during extraction commit.' };
    }

    return {
      alreadySecured:      false,
      loadout:             serializeItems(updatedCharacter.inventory.loadout),
      level:               updatedCharacter.level,
      experience:          updatedCharacter.experience,
      characterAttributes: serializeAttributes(updatedCharacter.characterAttributes),
      revision:            updatedCharacter.revision
    };
  }
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function serializeItems(items) {
  return (items || []).map(i => ({ lootId: i.lootId, amount: i.amount }));
}

function serializeAttributes(attrs) {
  if (!attrs) {
    return { vitality: 0, resistance: 0, strength: 0, dexterity: 0, intelligence: 0, luck: 0, availablePoints: 0 };
  }
  return {
    vitality:        attrs.vitality        || 0,
    resistance:      attrs.resistance      || 0,
    strength:        attrs.strength        || 0,
    dexterity:       attrs.dexterity       || 0,
    intelligence:    attrs.intelligence    || 0,
    luck:            attrs.luck            || 0,
    availablePoints: attrs.availablePoints || 0,
  };
}

module.exports = ExtractionCommitService;

// src/services/ExtractionCommitService.js
//
// Unified, authoritative commit of raid loot and progression.
//
// Design invariants:
//   - Idempotent: same (raidId, resultSequence) → alreadySecured = true, no mutation.
//   - Atomic: loot + progression applied in a single character.save() call.
//   - Authoritative: the server independently recalculates level/XP/attribute points;
//     the client's `resultingLevel` is only used as a cross-check, never trusted blindly.

'use strict';

const Character = require('../models/Character');
const {
  computeLevelAndExperience,
  computeAttributePointsGranted,
} = require('../config/progressionBalance');

// Maximum number of extraction receipts kept in history (mirrors Unity's cap).
const MAX_EXTRACTION_RECEIPTS = 256;
// Maximum number of progression receipts kept in history.
const MAX_PROGRESSION_RECEIPTS = 256;

class ExtractionCommitService {
  /**
   * Commits a raid extraction result atomically.
   *
   * @param {string} accountId
   * @param {object} payload
   * @param {string}   payload.raidId
   * @param {number}   payload.resultSequence
   * @param {Array}    [payload.items]       - [{ lootId, amount }], may be absent or empty.
   * @param {object}   [payload.progression] - { consolidatedExperience, resultingLevel }
   *
   * @returns {Promise<{
   *   alreadySecured:     boolean,
   *   loadout:            { lootId: string, amount: number }[],
   *   level:              number,
   *   experience:         number,
   *   characterAttributes: object
   * }>}
   *
   * @throws {{ statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND' }}
   * @throws {{ statusCode: 409, errorCode: 'LOADOUT_NOT_EMPTY' }}
   * @throws {{ statusCode: 422, errorCode: 'PROGRESSION_MISMATCH' }}
   */
  static async commit(accountId, payload) {
    const { raidId, resultSequence, items = [], progression } = payload;

    const character = await Character.findOne({ accountId });
    if (!character) {
      throw {
        statusCode: 404,
        errorCode: 'CHARACTER_NOT_FOUND',
        message: 'No character found for this account.',
      };
    }

    // ------------------------------------------------------------------
    // 1. Idempotency check for loot
    // ------------------------------------------------------------------
    const extractionReceipts = character.inventory.appliedExtractionReceipts || [];
    const lootAlreadyApplied = extractionReceipts.some(
      r => r.raidId === raidId && r.resultSequence === resultSequence
    );
    if (lootAlreadyApplied) {
      if (character.inventory.pendingReservation) {
        character.inventory.preparedEquipment = character.inventory.pendingReservation.preparedEquipment || {};
        character.inventory.pendingReservation = null;
        character.markModified('inventory.preparedEquipment');
        character.markModified('inventory.pendingReservation');
        await character.save();
      }

      return {
        alreadySecured:      true,
        loadout:             serializeItems(character.inventory.loadout),
        level:               character.level,
        experience:          character.experience,
        characterAttributes: serializeAttributes(character.characterAttributes),
      };
    }

    // ------------------------------------------------------------------
    // 2. Guard: loadout must be empty before an extraction can be applied
    // ------------------------------------------------------------------
    if (character.inventory.loadout && character.inventory.loadout.length > 0) {
      throw {
        statusCode: 409,
        errorCode:  'LOADOUT_NOT_EMPTY',
        message:    'Cannot commit extraction while loadout is not empty.',
      };
    }

    // ------------------------------------------------------------------
    // 3. Progression: authoritative recalculation (if included in payload)
    // ------------------------------------------------------------------
    let applyProgression  = false;
    let newLevel          = character.level;
    let newExperience     = character.experience;
    let pointsGranted     = 0;

    if (progression) {
      const { consolidatedExperience, resultingLevel: clientResultingLevel } = progression;

      // Check if this progression result was already applied (separate watermark).
      const progressionAlreadyApplied =
        resultSequence <= character.lastAppliedProgressionResultSequence;

      if (!progressionAlreadyApplied) {
        // Server recalculates independently.
        const computed = computeLevelAndExperience(
          character.level,
          character.experience,
          consolidatedExperience
        );

        // Reject if client's claimed resultingLevel doesn't match server computation.
        if (computed.resultingLevel !== clientResultingLevel) {
          throw {
            statusCode: 422,
            errorCode:  'PROGRESSION_MISMATCH',
            message: `Server computed resultingLevel=${computed.resultingLevel}, ` +
                     `but client claimed ${clientResultingLevel}. Payload rejected.`,
          };
        }

        pointsGranted = computeAttributePointsGranted(character.level, computed.resultingLevel);
        newLevel      = computed.resultingLevel;
        newExperience = computed.resultingExperience;
        applyProgression = true;
      }
    }

    // ------------------------------------------------------------------
    // 4. Apply loot to the loadout
    // ------------------------------------------------------------------
    if (items && items.length > 0) {
      for (const item of items) {
        const existing = character.inventory.loadout.find(i => i.lootId === item.lootId);
        if (existing) {
          existing.amount += item.amount;
        } else {
          character.inventory.loadout.push({ lootId: item.lootId, amount: item.amount });
        }
      }
    }

    // ------------------------------------------------------------------
    // 5. Apply progression if needed
    // ------------------------------------------------------------------
    if (applyProgression) {
      character.level      = newLevel;
      character.experience = newExperience;
      character.lastAppliedProgressionResultSequence = resultSequence;

      if (pointsGranted > 0) {
        character.characterAttributes.availablePoints =
          (character.characterAttributes.availablePoints || 0) + pointsGranted;
      }

      const progressionReceipt = {
        raidId,
        resultSequence,
        consolidatedExperience: progression.consolidatedExperience,
        resultingLevel:         newLevel,
      };
      character.lastProgressionReceipt = progressionReceipt;
      character.appliedProgressionReceipts.push(progressionReceipt);
      while (character.appliedProgressionReceipts.length > MAX_PROGRESSION_RECEIPTS) {
        character.appliedProgressionReceipts.shift();
      }
    }

    // ------------------------------------------------------------------
    // 6. Register the extraction receipt (idempotency log)
    // ------------------------------------------------------------------
    character.inventory.appliedExtractionReceipts.push({ raidId, resultSequence });
    while (character.inventory.appliedExtractionReceipts.length > MAX_EXTRACTION_RECEIPTS) {
      character.inventory.appliedExtractionReceipts.shift();
    }

    // ------------------------------------------------------------------
    // 7. Restore prepared equipment from the reservation (if any) and clear it
    // ------------------------------------------------------------------
    if (character.inventory.pendingReservation) {
      character.inventory.preparedEquipment =
        character.inventory.pendingReservation.preparedEquipment || {};
      character.inventory.pendingReservation = null;
      character.markModified('inventory.preparedEquipment');
      character.markModified('inventory.pendingReservation');
    }

    // ------------------------------------------------------------------
    // 8. Atomic save — single write to MongoDB
    // ------------------------------------------------------------------
    character.markModified('inventory.loadout');
    character.markModified('inventory.appliedExtractionReceipts');

    await character.save();

    return {
      alreadySecured:      false,
      loadout:             serializeItems(character.inventory.loadout),
      level:               character.level,
      experience:          character.experience,
      characterAttributes: serializeAttributes(character.characterAttributes),
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

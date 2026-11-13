'use strict';

const { ABILITY_CATALOG } = require('../config/abilityCatalog');

const MAX_ABILITY_ID_LENGTH = 64;
const SLOTS = ['slot1', 'slot2'];

function invalid(message) {
  return { statusCode: 400, errorCode: 'INVALID_PREPARED_ABILITIES', message };
}

// Same normalization as AbilityService (purchase): trimmed, lower-case ids.
function normalizeSlot(value) {
  if (value === undefined || value === null) return '';
  if (typeof value !== 'string') throw invalid('Prepared ability slots must be strings.');
  const id = value.trim().toLowerCase();
  if (id.length > MAX_ABILITY_ID_LENGTH) {
    throw invalid(`Prepared ability ids must not exceed ${MAX_ABILITY_ID_LENGTH} characters.`);
  }
  return id;
}

/**
 * Validates a prepared ability pair against the backend catalog and the character's unlocks.
 * Returns the normalized `{ slot1, slot2 }`; empty slots are valid. Pure: never touches storage.
 * @throws 400 INVALID_PREPARED_ABILITIES (shape, length, same ability in both slots)
 * @throws 422 UNKNOWN_ABILITY (not in the catalog)
 * @throws 422 ABILITY_NOT_UNLOCKED (in the catalog, not owned)
 */
function validatePreparedAbilities(preparedAbilities, unlockedAbilities) {
  if (preparedAbilities === null || typeof preparedAbilities !== 'object' || Array.isArray(preparedAbilities)) {
    throw invalid('preparedAbilities must be an object with slot1 and slot2.');
  }

  const normalized = {};
  for (const slot of SLOTS) normalized[slot] = normalizeSlot(preparedAbilities[slot]);
  if (normalized.slot1 && normalized.slot1 === normalized.slot2) {
    throw invalid('The same ability cannot occupy both prepared slots.');
  }

  const owned = Array.from(unlockedAbilities || []);
  for (const slot of SLOTS) {
    const id = normalized[slot];
    if (!id) continue;
    if (!Object.hasOwn(ABILITY_CATALOG, id)) {
      throw { statusCode: 422, errorCode: 'UNKNOWN_ABILITY', message: `Ability '${id}' does not exist.` };
    }
    if (!owned.includes(id)) {
      throw { statusCode: 422, errorCode: 'ABILITY_NOT_UNLOCKED', message: `Ability '${id}' is not unlocked.` };
    }
  }
  return normalized;
}

module.exports = { validatePreparedAbilities, MAX_ABILITY_ID_LENGTH };

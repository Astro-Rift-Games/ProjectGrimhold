'use strict';

const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');

const LOOT_ID_ALIASES = Object.freeze({
  recovery_sword: 'arming_sword',
  training_sword: 'arming_sword',
  longsword: 'arming_sword',
  greatsword: 'long_sword',
  wand: 'magic_wand',
  staff: 'magic_staff',
  spellbook: 'magic_wand',
  training_shield: 'shield',
  // Legacy leg armor stored without the separator before "plate"
  heavy_armor_legplate: 'heavy_armor_leg_plate'
});

function normalizeLootId(lootId) {
  if (typeof lootId !== 'string') return lootId;
  return LOOT_ID_ALIASES[lootId] || lootId;
}

// Only aggregate boundary payloads (Raid rewards/admission), never persisted
// Town lists. Stored inventory is migrated explicitly, not repaired at runtime.
function normalizeItems(items) {
  const merged = [];
  const indices = new Map();

  for (const item of items || []) {
    const lootId = normalizeLootId(item.lootId);
    const amount = item.amount;
    const existingIndex = indices.get(lootId);
    if (existingIndex !== undefined) {
      merged[existingIndex].amount += amount;
      continue;
    }

    indices.set(lootId, merged.length);
    merged.push({ lootId, amount });
  }

  return merged;
}

function normalizePreparedEquipment(equipment) {
  if (!equipment) return equipment;

  for (const slot of EQUIPMENT_SLOTS) {
    equipment[slot] = normalizeLootId(equipment[slot] || '');
  }

  return equipment;
}

module.exports = { normalizeItems, normalizeLootId, normalizePreparedEquipment };

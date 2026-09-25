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
  training_shield: 'shield'
});

function normalizeLootId(lootId) {
  if (typeof lootId !== 'string') return lootId;
  return LOOT_ID_ALIASES[lootId] || lootId;
}

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

function normalizeCharacterInventory(character) {
  const inventory = character && character.inventory;
  if (!inventory) return false;

  const before = JSON.stringify({
    stash: inventory.stash,
    loadout: inventory.loadout,
    preparedEquipment: inventory.preparedEquipment,
    pendingReservation: inventory.pendingReservation
  });

  inventory.stash = normalizeItems(inventory.stash);
  inventory.loadout = normalizeItems(inventory.loadout);
  normalizePreparedEquipment(inventory.preparedEquipment);

  if (inventory.pendingReservation) {
    inventory.pendingReservation.items = normalizeItems(inventory.pendingReservation.items);
    normalizePreparedEquipment(inventory.pendingReservation.preparedEquipment);
  }

  // Legacy migration: ensure all 8 slots exist (documents created with 6 slots)
  for (const slot of EQUIPMENT_SLOTS) {
    if (inventory.preparedEquipment && inventory.preparedEquipment[slot] === undefined) {
      inventory.preparedEquipment[slot] = '';
    }
    if (inventory.pendingReservation?.preparedEquipment &&
        inventory.pendingReservation.preparedEquipment[slot] === undefined) {
      inventory.pendingReservation.preparedEquipment[slot] = '';
    }
  }

  const after = JSON.stringify({
    stash: inventory.stash,
    loadout: inventory.loadout,
    preparedEquipment: inventory.preparedEquipment,
    pendingReservation: inventory.pendingReservation
  });

  if (before === after) return false;

  character.markModified('inventory.stash');
  character.markModified('inventory.loadout');
  character.markModified('inventory.preparedEquipment');
  character.markModified('inventory.pendingReservation');
  return true;
}

module.exports = {
  normalizeCharacterInventory,
  normalizeItems,
  normalizeLootId,
  normalizePreparedEquipment
};

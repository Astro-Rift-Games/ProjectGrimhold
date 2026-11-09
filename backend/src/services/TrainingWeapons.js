// src/services/TrainingWeapons.js
//
// Training weapon identity and the Doc 09 §15.2 Town-return rule.

'use strict';

const { normalizeLootId } = require('./InventoryLootIdNormalizer');
const { getEquipmentTemplate } = require('../config/shopCatalog');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');

const TRAINING_WEAPONS = Object.freeze([
  'training_short_sword',
  'training_dagger',
  'training_wand'
]);

function isTrainingWeapon(lootId) {
  return typeof lootId === 'string' && TRAINING_WEAPONS.includes(normalizeLootId(lootId));
}

function isNormalWeapon(lootId) {
  return typeof lootId === 'string' && getEquipmentTemplate(lootId)?.category === 'Weapon' && !isTrainingWeapon(lootId);
}

// Prepared equipment holds lootId strings (v2) or { lootId, instance } entries (v3).
function equipmentLootId(value) {
  return typeof value === 'string' ? value : value?.lootId;
}

/**
 * On Town return the training weapon goes back to the Trainer once the character owns a
 * normal weapon in loadout, stash or prepared equipment; attribute requirements do not matter.
 * Without a normal weapon the training weapon is kept so the character is never left unarmed.
 */
function returnTrainingWeapons({ loadout, stash, preparedEquipment }) {
  const owned = [
    ...loadout.map(entry => entry.lootId),
    ...stash.map(entry => entry.lootId),
    ...EQUIPMENT_SLOTS.map(slot => equipmentLootId(preparedEquipment?.[slot]))
  ];
  if (!owned.some(isNormalWeapon)) return { loadout, stash, preparedEquipment, changed: false };

  const nextLoadout = loadout.filter(entry => !isTrainingWeapon(entry.lootId));
  const nextStash = stash.filter(entry => !isTrainingWeapon(entry.lootId));
  const nextEquipment = { ...preparedEquipment };
  let equipmentChanged = false;
  for (const slot of EQUIPMENT_SLOTS) {
    const value = nextEquipment[slot];
    if (!isTrainingWeapon(equipmentLootId(value))) continue;
    nextEquipment[slot] = typeof value === 'string' ? '' : null;
    equipmentChanged = true;
  }

  return {
    loadout: nextLoadout,
    stash: nextStash,
    preparedEquipment: nextEquipment,
    changed: equipmentChanged || nextLoadout.length !== loadout.length || nextStash.length !== stash.length
  };
}

module.exports = { TRAINING_WEAPONS, isTrainingWeapon, isNormalWeapon, returnTrainingWeapons };

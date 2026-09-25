'use strict';

/**
 * Canonical ordered list of PreparedEquipment slot names.
 *
 * All serializers, validators, normalizers and schemas derive their slot list
 * from this single source of truth.  To add or rename a slot, edit only this
 * array and update the Mongoose schema fields accordingly.
 *
 * Current slots (6):
 *   weaponSlot1 — Set A Main Hand
 *   weaponSlot2 — Set B Main Hand
 *   helmet
 *   armor
 *   gloves
 *   boots
 */
const EQUIPMENT_SLOTS = Object.freeze([
  'weaponSlot1',
  'weaponSlot2',
  'offHand1',
  'helmet',
  'armor',
  'gloves',
  'boots',
  'offHand2',
]);

module.exports = { EQUIPMENT_SLOTS };

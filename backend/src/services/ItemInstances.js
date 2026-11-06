'use strict';

const { Types } = require('mongoose');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');
const { getEquipmentTemplate, GEM_CATALOG } = require('../config/shopCatalog');
const { normalizeLootId } = require('./InventoryLootIdNormalizer');

const ATTRIBUTES = Object.freeze(['vitality', 'resistance', 'strength', 'dexterity', 'intelligence', 'luck']);
const GRADE_COEFFICIENTS = Object.freeze({ E: 0.25, D: 0.40, C: 0.55, B: 0.70, A: 0.85, S: 1 });
const INSTANCE_ID = /^[a-f0-9]{24}$/;
const plainObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const onlyKeys = (value, keys) => plainObject(value) && Object.keys(value).every(key => keys.includes(key));
const plain = value => typeof value?.toObject === 'function' ? value.toObject() : value;

function invalidInventory() {
  throw { statusCode: 400, errorCode: 'INVALID_INVENTORY', message: 'Invalid equipment instance or inventory shape.' };
}

function validScaling(value) {
  return onlyKeys(value, ['attribute', 'grade']) && ATTRIBUTES.includes(value.attribute) &&
    typeof value.grade === 'string' && Object.hasOwn(GRADE_COEFFICIENTS, value.grade);
}

function validModifiers(lootId, modifiers) {
  const template = getEquipmentTemplate(lootId);
  if (modifiers === undefined) return true;
  if (template?.category !== 'Weapon' || !onlyKeys(modifiers, ['primary', 'secondary', 'gemSocketCount', 'gems'])) return false;
  if (modifiers.primary !== undefined && (!validScaling(modifiers.primary) || modifiers.primary.attribute !== template.naturalAttribute)) return false;
  if (modifiers.secondary !== undefined && (!modifiers.primary || !validScaling(modifiers.secondary) || modifiers.secondary.attribute === modifiers.primary.attribute)) return false;
  const count = modifiers.gemSocketCount;
  return Number.isInteger(count) && count >= 0 && count <= 2 && Array.isArray(modifiers.gems) &&
    modifiers.gems.length === count && modifiers.gems.every(gem => gem === null || (typeof gem === 'string' && Object.hasOwn(GEM_CATALOG, gem)));
}

function validInstance(lootId, instance) {
  return Boolean(getEquipmentTemplate(lootId)) && onlyKeys(instance, ['instanceId', 'modifiers']) &&
    typeof instance.instanceId === 'string' && INSTANCE_ID.test(instance.instanceId) && validModifiers(lootId, instance.modifiers);
}

function validItem(item, requireSlot = true) {
  item = plain(item);
  return plainObject(item) && typeof item.lootId === 'string' && item.lootId.length > 0 &&
    Number.isSafeInteger(item.amount) && item.amount >= 1 &&
    (!requireSlot || (Number.isSafeInteger(item.slot) && item.slot >= 0)) &&
    (getEquipmentTemplate(item.lootId) ? item.amount === 1 && validInstance(item.lootId, item.instance) : item.instance === undefined);
}

function validEquipment(value) {
  value = plain(value);
  return value === null || (onlyKeys(value, ['lootId', 'instance']) && validInstance(value.lootId, value.instance));
}

function validInventory(inventory) {
  inventory = plain(inventory);
  const ids = new Set();
  const checkIdentity = instance => {
    if (!instance) return true;
    if (ids.has(instance.instanceId)) return false;
    ids.add(instance.instanceId); return true;
  };
  const checkList = items => Array.isArray(items) && new Set(items.map(item => item.slot)).size === items.length &&
    items.every(item => validItem(item) && checkIdentity(item.instance));
  const checkEquipment = eq => EQUIPMENT_SLOTS.every(slot => {
    const value = eq?.[slot] ?? null;
    return validEquipment(value) && checkIdentity(value?.instance);
  });
  return checkList(inventory.stash || []) && checkList(inventory.loadout || []) && checkEquipment(inventory.preparedEquipment) &&
    (!inventory.pendingReservation || (checkList(inventory.pendingReservation.items || []) && checkEquipment(inventory.pendingReservation.preparedEquipment)));
}

// This is the only instance ID issuer. All creation paths use it, including migration.
function createInstance(lootId, modifiers = plainModifiers(lootId)) {
  if (!getEquipmentTemplate(lootId) || !validModifiers(lootId, modifiers)) invalidInventory();
  return { instanceId: new Types.ObjectId().toHexString(), ...(modifiers === undefined ? {} : { modifiers: structuredClone(modifiers) }) };
}
function plainModifiers(lootId) {
  const template = getEquipmentTemplate(lootId);
  if (template?.category !== 'Weapon') return undefined;
  const grade = Object.keys(GRADE_COEFFICIENTS).find(grade => GRADE_COEFFICIENTS[grade] === template.legacyCoefficient);
  if (template.legacyCoefficient !== 0 && !grade) invalidInventory();
  return { ...(grade ? { primary: { attribute: template.naturalAttribute, grade } } : {}), gemSocketCount: 0, gems: [] };
}
function createEquipment(lootId, modifiers) {
  lootId = normalizeLootId(lootId);
  return { lootId, instance: createInstance(lootId, modifiers) };
}

// Raid DTOs are ID-free. Do not fold equipment variants into a lootId aggregate.
function validateResultItems(items) {
  if (!Array.isArray(items)) invalidInventory();
  return items.map(raw => {
    const item = plain(raw);
    if (!onlyKeys(item, ['lootId', 'amount', 'modifiers']) || typeof item.lootId !== 'string' || !item.lootId ||
        !Number.isSafeInteger(item.amount) || item.amount < 1) invalidInventory();
    const lootId = normalizeLootId(item.lootId);
    if ((getEquipmentTemplate(lootId) && item.amount !== 1) || !validModifiers(lootId, item.modifiers)) invalidInventory();
    return { lootId, amount: item.amount, ...(item.modifiers === undefined ? {} : { modifiers: structuredClone(item.modifiers) }) };
  });
}
function validateResultEquipment(equipment = {}) {
  equipment = plain(equipment);
  if (!onlyKeys(equipment, EQUIPMENT_SLOTS)) invalidInventory();
  return Object.fromEntries(EQUIPMENT_SLOTS.map(slot => {
    const value = plain(equipment[slot]) ?? null;
    if (value === null) return [slot, null];
    if (!onlyKeys(value, ['lootId', 'modifiers']) || !getEquipmentTemplate(value.lootId) || !validModifiers(value.lootId, value.modifiers)) invalidInventory();
    return [slot, { lootId: normalizeLootId(value.lootId), ...(value.modifiers === undefined ? {} : { modifiers: structuredClone(value.modifiers) }) }];
  }));
}
function resultEquipmentToInstances(equipment) {
  return Object.fromEntries(Object.entries(validateResultEquipment(equipment)).map(([slot, value]) =>
    [slot, value ? createEquipment(value.lootId, value.modifiers) : null]));
}
function equipmentToResult(equipment) {
  return Object.fromEntries(EQUIPMENT_SLOTS.map(slot => {
    const value = plain(equipment?.[slot]);
    return [slot, value ? { lootId: value.lootId, ...(value.instance.modifiers === undefined ? {} : { modifiers: structuredClone(value.instance.modifiers) }) } : null];
  }));
}

module.exports = { ATTRIBUTES, INSTANCE_ID, validModifiers, validInstance, validItem, validEquipment, validInventory,
  invalidInventory, createInstance, createEquipment, plainModifiers, validateResultItems, validateResultEquipment,
  resultEquipmentToInstances, equipmentToResult };

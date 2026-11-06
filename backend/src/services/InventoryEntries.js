'use strict';

const { normalizeLootId } = require('./InventoryLootIdNormalizer');
const { getEquipmentTemplate } = require('../config/shopCatalog');
const { validInventory, invalidInventory, createEquipment } = require('./ItemInstances');

function inventoryVersion(character) {
  return character.inventory?.inventorySchemaVersion ?? 1;
}

// Copy declared subdocument fields together; never reconstruct a stored entry
// from just lootId/amount. Future schema additions travel with the entry too.
function copyEntry(item) {
  return typeof item.toObject === 'function' ? item.toObject() : structuredClone(item);
}

function serializeItems(items, sorted = false) {
  const entries = (items || []).map(copyEntry);
  return sorted ? entries.sort((a, b) => a.slot - b.slot) : entries;
}

function requireMigratedInventory(character) {
  const env = require('../config/env');
  const version = inventoryVersion(character);
  if ((env.inventoryInstanceProtocolGate && version !== 3) ||
      (!env.inventoryInstanceProtocolGate && env.inventoryProtocolGate && version !== 2 && version !== 3)) {
    throw { statusCode: 409, errorCode: 'INVENTORY_MIGRATION_REQUIRED', message: 'Inventory migration is required.' };
  }
  if (version === 3 && !validInventory(character.inventory)) invalidInventory();
}

const CAPACITIES = Object.freeze({ loadout: 30, stash: 60 });

function inventoryFull() {
  throw { statusCode: 409, errorCode: 'INVENTORY_FULL', message: 'Inventory has no available slot within capacity.' };
}

function requireSplitEnabled() {
  if (!require('../config/env').inventorySplitEnabled) {
    throw { statusCode: 403, errorCode: 'FEATURE_DISABLED', message: 'Inventory split is disabled.' };
  }
}

function requireSlotInventory(character) {
  requireMigratedInventory(character);
  if (![2, 3].includes(inventoryVersion(character))) {
    throw { statusCode: 409, errorCode: 'INVENTORY_MIGRATION_REQUIRED', message: 'Inventory migration is required.' };
  }
}

function requireSourceSlot(character, sourceSlot) {
  requireMigratedInventory(character);
  if (![2, 3].includes(inventoryVersion(character))) {
    if (sourceSlot !== undefined) requireSlotInventory(character);
    return;
  }
  if (sourceSlot === undefined) {
    throw { statusCode: 409, errorCode: 'INVENTORY_SLOT_ADDRESS_REQUIRED', message: 'A source slot is required for this inventory operation.' };
  }
  assertSlot(sourceSlot);
}

function assertSlot(slot) {
  if (!Number.isSafeInteger(slot) || slot < 0) {
    throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'Slot must be a non-negative integer.' };
  }
}

function lowestFreeSlot(items, capacity) {
  const occupied = new Set(items.map(item => item.slot));
  for (let slot = 0; slot < capacity; slot++) if (!occupied.has(slot)) return slot;
  return -1;
}

function lowestItemIndex(items, lootId, capacity = Infinity) {
  let index = -1;
  for (let i = 0; i < items.length; i++) {
    if (matchesLootId(items[i], lootId) && items[i].slot < capacity &&
        (index === -1 || items[i].slot < items[index].slot)) index = i;
  }
  return index;
}

function addAmount(item, amount) {
  const total = item.amount + amount;
  if (!Number.isSafeInteger(total) || total < 1) {
    throw { statusCode: 400, errorCode: 'INTEGER_OVERFLOW', message: 'Item amount causes integer overflow.' };
  }
  item.amount = total;
}

// All receivers share this rule. Overflow entries are sources only, never targets.
// Copies travel whole when changing list; new aggregate rewards have no slot yet.
function receiveItem(items, list, item, version) {
  const capacity = CAPACITIES[list];
  const isInstance = version === 3 && Boolean(getEquipmentTemplate(item.lootId));
  // Receivers of newly issued units use the same issuer and placement as all other writers.
  if (isInstance && !item.instance) {
    for (let unit = 0; unit < item.amount; unit++) {
      receiveItem(items, list, { amount: 1, ...createEquipment(item.lootId, item.modifiers) }, version);
    }
    return;
  }
  const index = isInstance ? -1 : version >= 2 ? lowestItemIndex(items, item.lootId, capacity)
    : items.findIndex(existing => matchesLootId(existing, item.lootId));
  if (index !== -1) {
    if (version >= 2) addAmount(items[index], item.amount);
    else items[index].amount += item.amount;
    return;
  }
  if (version < 2) { items.push(copyEntry(item)); return; }
  const slot = lowestFreeSlot(items, capacity);
  if (items.length >= capacity || slot === -1) inventoryFull();
  items.push({ ...copyEntry(item), slot });
}

// Rollback is the sole restoration exception to normal receiving: no merging,
// no dropping, and overflow allowed. Reserve free original positions first so a
// collision fallback cannot steal another reserved entry's original position.
function restoreEntries(current, reserved, list) {
  const restored = serializeItems(current);
  const occupied = new Set(restored.map(item => item.slot));
  const collisions = [];
  for (const item of serializeItems(reserved, true)) {
    if (Number.isSafeInteger(item.slot) && !occupied.has(item.slot)) {
      restored.push(item); occupied.add(item.slot);
    } else collisions.push(item);
  }
  for (const item of collisions) {
    let slot = lowestFreeSlot(restored, CAPACITIES[list]);
    if (slot === -1) {
      slot = CAPACITIES[list];
      while (occupied.has(slot)) slot++;
    }
    restored.push({ ...item, slot }); occupied.add(slot);
  }
  return restored;
}

function assertUnambiguousItems(items, lootIds = null) {
  const addressed = lootIds && new Set(lootIds.map(normalizeLootId));
  const seen = new Set();
  for (const item of items || []) {
    const id = normalizeLootId(item.lootId);
    if (addressed && !addressed.has(id)) continue;
    if (seen.has(id)) {
      throw { statusCode: 409, errorCode: 'INVENTORY_MIGRATION_REQUIRED', message: 'Inventory migration is required.' };
    }
    seen.add(id);
  }
}

function matchesLootId(item, lootId) {
  return normalizeLootId(item.lootId) === normalizeLootId(lootId);
}

module.exports = { inventoryVersion, copyEntry, serializeItems, requireMigratedInventory, requireSplitEnabled,
  requireSlotInventory, requireSourceSlot, assertUnambiguousItems, matchesLootId,
  CAPACITIES, inventoryFull, assertSlot, lowestItemIndex, receiveItem, restoreEntries, addAmount };

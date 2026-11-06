'use strict';

/**
 * Explicit v2 -> v3 migration. From backend/:
 * node scripts/migrate_item_instances.js [--dry-run] [--help]
 *
 * Importing does not load environment files or connect. During maintenance, back
 * up BOTH collections, drain writers/hosts, deploy v3-capable clients/backend,
 * dry-run against a database copy, then migrate and enable the separate v3 gate.
 * V1 must first run migrate_inventory_slots.js. No read performs migration.
 *
 * Results migrate first and stay ID-free. Their conversion is independently
 * idempotent, so an interrupted run can resume before the character cutover.
 * Character ownership changes in one revision-guarded update; conflicts re-read
 * and re-plan (three retries). No units or overflow entries are discarded.
 * JSON lines contain per-character status, then summary; never driver errors.
 * Exit 0 success, 1 aborted/database failure, 2 usage error. Dry-run never writes.
 */
const { EQUIPMENT_SLOTS } = require('../src/config/equipmentSlots');
const { getEquipmentTemplate } = require('../src/config/shopCatalog');
const { normalizeLootId } = require('../src/services/InventoryLootIdNormalizer');
const { restoreEntries } = require('../src/services/InventoryEntries');
const { createEquipment, plainModifiers, validInventory, validateResultItems, validateResultEquipment } = require('../src/services/ItemInstances');

function migrateList(items, list) {
  if (!Array.isArray(items) || items.some(item => !item || typeof item.lootId !== 'string' || !item.lootId ||
      !Number.isSafeInteger(item.slot) || item.slot < 0 || !Number.isSafeInteger(item.amount) || item.amount < 1 || item.instance !== undefined) ||
      new Set(items.map(item => item.slot)).size !== items.length) throw new Error('INVALID_ENTRY');
  const extras = [];
  const first = items.map(item => {
    if (!getEquipmentTemplate(item.lootId)) return structuredClone(item);
    for (let unit = 1; unit < item.amount; unit++) extras.push({ amount: 1, ...createEquipment(item.lootId) });
    return { ...structuredClone(item), ...createEquipment(item.lootId), amount: 1 };
  });
  // Reuse lossless restoration: original positions are reserved first; extras
  // use the lowest holes and then overflow, never merges or capacity truncation.
  return restoreEntries(first, extras, list);
}
function migrateEquipment(equipment = {}, result = false) {
  return Object.fromEntries(EQUIPMENT_SLOTS.map(slot => {
    const value = equipment[slot];
    if (value === undefined || value === null || value === '' || value === 'null') return [slot, null];
    if (result && typeof value === 'object') {
      return [slot, validateResultEquipment({ [slot]: value })[slot]];
    }
    if (typeof value !== 'string' || !getEquipmentTemplate(value)) throw new Error('INVALID_EQUIPMENT');
    const lootId = normalizeLootId(value);
    const modifiers = plainModifiers(lootId);
    return [slot, result ? { lootId, ...(modifiers === undefined ? {} : { modifiers }) } : createEquipment(lootId)];
  }));
}
function planItemInstances(character) {
  const inventory = character.inventory || {};
  if (inventory.inventorySchemaVersion !== 2) return { status: 'skipped', reason: inventory.inventorySchemaVersion === 3 ? 'ALREADY_V3' : 'UNSUPPORTED_VERSION' };
  try {
    const set = {
      'inventory.stash': migrateList(inventory.stash || [], 'stash'),
      'inventory.loadout': migrateList(inventory.loadout || [], 'loadout'),
      'inventory.preparedEquipment': migrateEquipment(inventory.preparedEquipment),
      'inventory.inventorySchemaVersion': 3
    };
    if (inventory.pendingReservation) set['inventory.pendingReservation'] = { ...structuredClone(inventory.pendingReservation),
      items: migrateList(inventory.pendingReservation.items || [], 'loadout'),
      preparedEquipment: migrateEquipment(inventory.pendingReservation.preparedEquipment) };
    const candidate = { ...inventory, stash: set['inventory.stash'], loadout: set['inventory.loadout'], preparedEquipment: set['inventory.preparedEquipment'],
      pendingReservation: set['inventory.pendingReservation'] || null };
    if (!validInventory(candidate)) throw new Error('INVALID_INVENTORY');
    return { status: 'planned', set };
  } catch {
    return { status: 'aborted', reason: 'INVALID_INVENTORY' };
  }
}
function planStoredResult(result) {
  if (!Array.isArray(result.items)) throw new Error('INVALID_RESULT');
  const items = result.items.flatMap(item => {
    if (!item || !Number.isSafeInteger(item.amount) || item.amount < 1) throw new Error('INVALID_RESULT');
    if (!getEquipmentTemplate(item.lootId)) return [structuredClone(item)];
    const lootId = normalizeLootId(item.lootId);
    const modifiers = item.modifiers === undefined ? plainModifiers(lootId) : item.modifiers;
    return Array.from({ length: item.amount }, () => ({ lootId, amount: 1, ...(modifiers === undefined ? {} : { modifiers: structuredClone(modifiers) }) }));
  });
  return { items: validateResultItems(items), preparedEquipment: migrateEquipment(result.preparedEquipment, true) };
}

async function migrateCharacter(collection, results, initial, { dryRun, maxRetries }) {
  let character = initial; let conflicts = 0;
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    const plan = planItemInstances(character);
    const record = { characterId: String(character._id), status: plan.status, ...(plan.reason ? { reason: plan.reason } : {}) };
    if (plan.status !== 'planned') return { record, conflicts };
    if (character.revision !== undefined && (!Number.isSafeInteger(character.revision) || character.revision < 0 || character.revision === Number.MAX_SAFE_INTEGER)) {
      return { record: { ...record, status: 'aborted', reason: 'INVALID_REVISION' }, conflicts };
    }
    const resultPlans = [];
    if (results) {
      for await (const result of results.find({ accountId: String(character.accountId) })) {
        resultPlans.push({ before: result, set: planStoredResult(result) });
      }
    }
    record.results = resultPlans.length;
    if (dryRun) return { record, conflicts };
    for (const { before, set } of resultPlans) {
      const filter = { _id: before._id, items: before.items,
        preparedEquipment: before.preparedEquipment === undefined ? { $exists: false } : before.preparedEquipment };
      const updated = await results.updateOne(filter, { $set: set });
      if (updated.matchedCount !== 1) return { record: { ...record, status: 'aborted', reason: 'RESULT_CONFLICT' }, conflicts: conflicts + 1 };
    }
    const updated = await collection.updateOne({ _id: character._id, 'inventory.inventorySchemaVersion': 2,
      revision: character.revision === undefined ? { $exists: false } : character.revision }, { $set: plan.set, $inc: { revision: 1 } });
    if (updated.matchedCount === 1) return { record: { ...record, status: 'migrated' }, conflicts };
    conflicts++;
    if (attempt === maxRetries) return { record: { ...record, status: 'aborted', reason: 'REVISION_CONFLICT' }, conflicts };
    character = await collection.findOne({ _id: initial._id });
    if (!character) return { record: { characterId: String(initial._id), status: 'skipped', reason: 'CHARACTER_REMOVED' }, conflicts };
  }
}
async function migrateItemInstances(collection, results = null, { dryRun = false, maxRetries = 3, report = record => console.log(JSON.stringify(record)) } = {}) {
  if (!Number.isSafeInteger(maxRetries) || maxRetries < 0) throw new RangeError('Invalid maxRetries.');
  const summary = { type: 'summary', dryRun, scanned: 0, planned: 0, migrated: 0, skipped: 0, aborted: 0, conflicts: 0, exitCode: 0 };
  for await (const character of collection.find({})) {
    summary.scanned++;
    let outcome;
    try { outcome = await migrateCharacter(collection, results, character, { dryRun, maxRetries }); }
    catch { outcome = { record: { characterId: String(character._id), status: 'aborted', reason: 'CHARACTER_ERROR' }, conflicts: 0 }; }
    summary[outcome.record.status]++; summary.conflicts += outcome.conflicts; report(outcome.record);
  }
  summary.exitCode = summary.aborted ? 1 : 0; report(summary); return summary;
}
async function main(args = process.argv.slice(2)) {
  const usage = 'Usage: node scripts/migrate_item_instances.js [--dry-run] [--help]';
  if (args.some(arg => arg !== '--dry-run' && arg !== '--help')) { console.error(usage); return 2; }
  if (args.includes('--help')) {
    console.log(`${usage}\nMONGODB_URI via environment or .env. Back up both collections during maintenance.\n--dry-run reports plans without writing. See backend/README.md for v3 cutover requirements.`);
    return 0;
  }
  require('dotenv').config({ quiet: true });
  if (!process.env.MONGODB_URI) { console.error('Item instance migration failed: MONGODB_URI is required.'); return 1; }
  const mongoose = require('mongoose');
  try {
    await mongoose.connect(process.env.MONGODB_URI);
    const summary = await migrateItemInstances(mongoose.connection.db.collection('characters'),
      mongoose.connection.db.collection('authoritativeextractionresults'), { dryRun: args.includes('--dry-run') });
    return summary.exitCode;
  } catch { console.error('Item instance migration failed. Check database availability and permissions.'); return 1; }
  finally { await mongoose.disconnect(); }
}
module.exports = { planItemInstances, planStoredResult, migrateItemInstances, main };
if (require.main === module) main().then(code => { process.exitCode = code; }).catch(() => { console.error('Item instance migration failed.'); process.exitCode = 1; });

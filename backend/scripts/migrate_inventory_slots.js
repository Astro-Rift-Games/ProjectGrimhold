'use strict';

/**
 * Inventory slot migration (explicit, one-time; never invoked by a read).
 * From backend/: node scripts/migrate_inventory_slots.js [--dry-run]
 * MONGODB_URI is read from the environment / backend/.env, like migrate_legacy_data.js.
 *
 * First dry-run against a database COPY. Before the real run: complete B1 rollout,
 * drain old backend instances, update clients/hosts, enable the protocol gate and
 * take a backup during the maintenance window (proposal §8.2). New characters
 * must be created at version 2. Never roll back below B1 or disable the gate after
 * migration; restoring the backup is safe only before any post-migration write.
 * This script changes neither deployment configuration nor new-character defaults.
 *
 * JSON lines: one character report (lists, duplicates, counts, unknown ids, exact
 * canonical per-lootId totals), then a summary. Unknown means absent from the
 * backend buy/sell/equipment catalogs; it is diagnostic only, not an eligibility
 * check. Unknown items and overflow remain in their lists at their assigned slots.
 * Duplicate amounts are folded using the legacy normalizer; other fields come
 * from the first occurrence, intact. Equipment, receipts and Raid results are
 * never rewritten. Missing/1 versions migrate; v2 and other versions are skipped.
 * Each commit uses one atomic $set + $inc revision, guarded by revision and legacy
 * version. Conflicts re-read/re-plan, with at most three retries per character.
 * Invalid data, unsafe quantities, total mismatches or exhausted conflicts abort
 * that character without a migration write. Dry-run does not write anything.
 * Exit: 0 = no aborted characters, 1 = abort/database failure, 2 = invalid usage.
 *
 * Exports accept plain stored documents / a MongoDB collection; importing this
 * module does not connect, load .env, or run the migration.
 */

const { normalizeLootId, normalizeItems } = require('../src/services/InventoryLootIdNormalizer');
const { BUY_CATALOG, SELL_CATALOG, BUYBACK_ELIGIBLE_IDS } = require('../src/config/shopCatalog');

const KNOWN_IDS = new Set([
  ...Object.keys(BUY_CATALOG), ...Object.keys(SELL_CATALOG), ...BUYBACK_ELIGIBLE_IDS
]);
const LEGACY_VERSION = { $or: [
  { 'inventory.inventorySchemaVersion': { $exists: false } },
  { 'inventory.inventorySchemaVersion': 1 }
] };

// BigInt makes the check independent of Number rounding during duplicate folding.
function totals(items) {
  const result = new Map();
  for (const item of items) {
    const id = normalizeLootId(item.lootId);
    result.set(id, (result.get(id) || 0n) + BigInt(item.amount));
  }
  return result;
}

function reportTotals(values) {
  return Object.fromEntries([...values].map(([id, amount]) => [id, amount.toString()]));
}

function planList(items, capacity) {
  if (!Array.isArray(items)) return { error: 'INVALID_LIST' };
  if (items.some(item => !item || typeof item.lootId !== 'string' || !item.lootId ||
      !Number.isSafeInteger(item.amount) || item.amount < 1)) {
    return { error: 'INVALID_ENTRY' };
  }

  const first = new Map();
  const occurrences = new Map();
  for (const item of items) {
    const id = normalizeLootId(item.lootId);
    if (!first.has(id)) first.set(id, item);
    occurrences.set(id, (occurrences.get(id) || 0) + 1);
  }
  // Reuse the approved normalizer's alias/fold/order behavior, but retain the
  // complete first subdocument rather than its aggregate-only output shape.
  const entries = normalizeItems(items).map((item, slot) => ({
    ...first.get(item.lootId), ...item, slot
  }));
  const before = totals(items);
  const after = totals(entries);
  const totalsCheck = before.size === after.size &&
    [...before].every(([id, amount]) => after.get(id) === amount);
  const safeAmounts = entries.every(item => Number.isSafeInteger(item.amount));
  return {
    entries,
    error: !totalsCheck ? 'TOTALS_MISMATCH' : !safeAmounts ? 'UNSAFE_AMOUNT' : undefined,
    report: {
      duplicates: [...occurrences].filter(([, count]) => count > 1)
        .map(([lootId, count]) => ({ lootId, entries: count })),
      duplicatesFolded: items.length - entries.length,
      entriesBefore: items.length,
      entriesAfter: entries.length,
      capacity,
      overCapacity: capacity !== null && entries.length > capacity,
      overflowEntries: capacity === null ? 0 : Math.max(0, entries.length - capacity),
      unknownIds: [...first.keys()].filter(id => !KNOWN_IDS.has(id)),
      totalsBefore: reportTotals(before),
      totalsAfter: reportTotals(after),
      totalsCheck
    }
  };
}

function planInventorySlots(character) {
  const inventory = character.inventory || {};
  const version = inventory.inventorySchemaVersion;
  if (version !== undefined && version !== 1) {
    return { status: 'skipped', reason: version === 2 ? 'ALREADY_V2' : 'UNSUPPORTED_VERSION' };
  }
  const lists = {};
  const set = {};
  let reason;
  const sources = [
    ['stash', inventory.stash === undefined ? [] : inventory.stash, 60],
    ['loadout', inventory.loadout === undefined ? [] : inventory.loadout, 30]
  ];
  if (inventory.pendingReservation != null) {
    sources.push(['pendingReservation.items', inventory.pendingReservation.items === undefined
      ? [] : inventory.pendingReservation.items, null]);
  }
  for (const [name, items, capacity] of sources) {
    const planned = planList(items, capacity);
    lists[name] = planned.report || { error: planned.error, totalsCheck: false };
    if (planned.error) reason = reason || planned.error;
    set[`inventory.${name}`] = planned.entries;
  }
  if (reason) return { status: 'aborted', reason, lists };
  set['inventory.inventorySchemaVersion'] = 2;
  return { status: 'planned', set, lists };
}

async function migrateCharacter(collection, initial, { dryRun, maxRetries }) {
  let character = initial;
  let conflicts = 0;
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    const plan = planInventorySlots(character);
    const record = { characterId: String(character._id), status: plan.status,
      ...(plan.reason && { reason: plan.reason }), ...(plan.lists && { lists: plan.lists }) };
    if (plan.status !== 'planned') return { record, conflicts };
    if (character.revision !== undefined &&
        (!Number.isSafeInteger(character.revision) || character.revision < 0 ||
         character.revision === Number.MAX_SAFE_INTEGER)) {
      return { record: { ...record, status: 'aborted', reason: 'INVALID_REVISION' }, conflicts };
    }
    if (dryRun) return { record, conflicts };
    const result = await collection.updateOne({
      _id: character._id,
      revision: character.revision === undefined ? { $exists: false } : character.revision,
      ...LEGACY_VERSION
    }, { $set: plan.set, $inc: { revision: 1 } });
    if (result.matchedCount === 1) {
      return { record: { ...record, status: 'migrated' }, conflicts };
    }
    conflicts++;
    if (attempt === maxRetries) {
      return { record: { ...record, status: 'aborted', reason: 'REVISION_CONFLICT' }, conflicts };
    }
    character = await collection.findOne({ _id: initial._id });
    if (!character) {
      return { record: { characterId: String(initial._id), status: 'skipped', reason: 'CHARACTER_REMOVED' }, conflicts };
    }
  }
}

async function migrateInventorySlots(collection, {
  dryRun = false, maxRetries = 3, report = record => console.log(JSON.stringify(record))
} = {}) {
  if (!Number.isSafeInteger(maxRetries) || maxRetries < 0) throw new RangeError('Invalid maxRetries.');
  const summary = { type: 'summary', dryRun, scanned: 0, planned: 0,
    migrated: 0, skipped: 0, aborted: 0, conflicts: 0, exitCode: 0 };
  // Raw driver documents preserve stored fields that a strict schema could strip.
  for await (const character of collection.find({})) {
    summary.scanned++;
    let outcome;
    try {
      outcome = await migrateCharacter(collection, character, { dryRun, maxRetries });
    } catch {
      // Do not print driver errors: they can contain connection credentials/data.
      outcome = { record: { characterId: String(character._id), status: 'aborted', reason: 'CHARACTER_ERROR' }, conflicts: 0 };
    }
    summary[outcome.record.status]++;
    summary.conflicts += outcome.conflicts;
    report(outcome.record);
  }
  summary.exitCode = summary.aborted ? 1 : 0;
  report(summary);
  return summary;
}

async function main(args = process.argv.slice(2)) {
  if (args.some(arg => arg !== '--dry-run' && arg !== '--help')) {
    console.error('Usage: node scripts/migrate_inventory_slots.js [--dry-run] [--help]');
    return 2;
  }
  if (args.includes('--help')) {
    console.log('Usage: node scripts/migrate_inventory_slots.js [--dry-run] [--help]\n' +
      'MONGODB_URI via environment or .env. Back up during maintenance before migrating.\n' +
      '--dry-run reports plans and totals without writing. See the script header for cutover requirements.');
    return 0;
  }
  require('dotenv').config({ quiet: true });
  if (!process.env.MONGODB_URI) {
    console.error('Inventory slot migration failed: MONGODB_URI is required.');
    return 1;
  }
  const mongoose = require('mongoose');
  try {
    await mongoose.connect(process.env.MONGODB_URI);
    const summary = await migrateInventorySlots(mongoose.connection.db.collection('characters'), {
      dryRun: args.includes('--dry-run')
    });
    return summary.exitCode;
  } catch {
    console.error('Inventory slot migration failed. Check database availability and permissions.');
    return 1;
  } finally {
    await mongoose.disconnect();
  }
}

module.exports = { planInventorySlots, migrateInventorySlots, main };

if (require.main === module) {
  main().then(code => { process.exitCode = code; }).catch(() => {
    console.error('Inventory slot migration failed.');
    process.exitCode = 1;
  });
}

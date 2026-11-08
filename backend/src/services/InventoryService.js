// src/services/InventoryService.js
const Character = require('../models/Character');
const AuthoritativeExtractionResult = require('../models/AuthoritativeExtractionResult');
const { randomUUID } = require('crypto');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');
const {
  normalizeItems,
  normalizeLootId,
  normalizePreparedEquipment
} = require('./InventoryLootIdNormalizer');
const {
  MAX_TRANSACTION_AMOUNT,
  BUYBACK_MAX_ENTRIES,
  BUYBACK_PRICE_MULTIPLIER,
  isBuybackEligible,
  getEquipmentTemplate,
  GEM_CATALOG,
  getBuyOffer,
  getSellOffer
} = require('../config/shopCatalog');

const { inventoryVersion, copyEntry, serializeItems, requireMigratedInventory,
  requireSlotInventory, requireSplitEnabled, requireSourceSlot, assertUnambiguousItems, matchesLootId,
  CAPACITIES, inventoryFull, assertSlot, lowestItemIndex, receiveItem, restoreEntries, addAmount } = require('./InventoryEntries');

const { INSTANCE_ID, invalidInventory, createEquipment, validateResultItems, resultEquipmentToInstances,
  equipmentToResult } = require('./ItemInstances');

function inventoryItems(character, list) {
  return serializeItems(character.inventory[list], inventoryVersion(character) >= 2);
}

function listsSnapshot(character) {
  return { stash: inventoryItems(character, 'stash'), loadout: inventoryItems(character, 'loadout'), revision: character.revision };
}

async function writeSlotLists(character, expectedRevision, lists, blockReservation = false, receipt = null) {
  const updated = await Character.findOneAndUpdate(
    { accountId: character.accountId, revision: expectedRevision,
      ...(blockReservation ? { 'inventory.pendingReservation': null } : {}) },
    { $set: Object.fromEntries(Object.entries(lists).map(([list, items]) => [`inventory.${list}`, items])), $inc: { revision: 1 },
      ...(receipt ? { $push: { appliedShopReceipts: { $each: [receipt], $slice: -64 } } } : {}) },
    { returnDocument: 'after' }
  );
  if (!updated) throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during inventory update.' };
  return listsSnapshot(updated);
}

async function transferSlotItems(character, fromList, toList, lootId, amount, expectedRevision, sourceSlot) {
  if (character.revision !== expectedRevision) throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
  if (!Number.isSafeInteger(amount) || amount < 1) throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'Amount must be a positive integer.' };
  const from = inventoryItems(character, fromList);
  const to = inventoryItems(character, toList);
  const index = from.findIndex(item => item.slot === sourceSlot && matchesLootId(item, lootId));
  if (index === -1 || from[index].amount < amount) {
    throw { statusCode: 409, errorCode: fromList === 'stash' ? 'INSUFFICIENT_STASH_ITEMS' : 'INSUFFICIENT_LOADOUT_ITEMS',
      message: `${fromList === 'stash' ? 'Stash' : 'Loadout'} does not hold ${amount} unit(s) of '${normalizeLootId(lootId)}'.` };
  }
  receiveItem(to, toList, { ...copyEntry(from[index]), amount }, inventoryVersion(character));
  if (from[index].amount === amount) from.splice(index, 1);
  else from[index].amount -= amount;
  // Legacy transfers remain usable during reservations; rollback restores collisions.
  return writeSlotLists(character, expectedRevision, { [fromList]: from, [toList]: to });
}

function restoreReservation(character) {
  const reservation = character.inventory.pendingReservation;
  const version = inventoryVersion(character);
  const equipment = serializePreparedEquipment(character.inventory.preparedEquipment, version);
  const displaced = [];
  for (const slot of EQUIPMENT_SLOTS) {
    const item = reservation.preparedEquipment?.[slot];
    if (!item) continue;
    if (!equipment[slot]) equipment[slot] = item;
    else displaced.push(version === 3 ? { ...copyEntry(item), amount: 1 } : { lootId: item, amount: 1 });
  }
  const loadout = restoreEntries(character.inventory.loadout, reservation.items, 'loadout');
  character.inventory.loadout = restoreEntries(loadout, displaced, 'loadout');
  character.inventory.preparedEquipment = equipment;
}

/**
 * Serializes the preparedEquipment subdocument to a plain object.
 * Iterates over EQUIPMENT_SLOTS so new slots are covered automatically.
 */
function serializePreparedEquipment(eq, version = 2) {
  const clean = (val) => (!val || val === 'null' ? (version === 3 ? null : '') :
    typeof val === 'object' ? copyEntry(val) : val);
  const result = {};
  for (const slot of EQUIPMENT_SLOTS) {
    result[slot] = clean(eq ? eq[slot] : undefined);
  }
  return result;
}

/**
 * Serializes the pendingReservation subdocument to a plain object, or null.
 */
function serializePendingReservation(res, sorted = false, version = 2) {
  if (!res || !res.reservationId) return null;
  return {
    reservationId:    res.reservationId,
    admittedAt:       res.admittedAt ? res.admittedAt.toISOString() : null,
    items:            serializeItems(res.items, sorted),
    preparedEquipment: serializePreparedEquipment(res.preparedEquipment, version)
  };
}

// Authoritative results target the owning account, not a reservation/raid identifier.
async function assertNoPendingExtractionResult(accountId) {
  if (await AuthoritativeExtractionResult.exists({ accountId, consumed: { $ne: true } })) {
    throw { statusCode: 409, errorCode: 'EXTRACTION_RESULT_PENDING', message: 'An authoritative extraction result is pending for this character.' };
  }
}

// soldAt and the internal epoch are deliberately not exposed to clients.
function serializeBuyback(inventory) {
  return { entries: (inventory.buyback?.entries || []).map(({ entryId, lootId, amount, unitPrice }) => ({ entryId, lootId, amount, unitPrice })) };
}

function buybackEpochFilter(inventory) {
  return inventory.buyback?.sessionEpoch || { $in: ['', null] };
}

function shopSnapshot(character) {
  return {
    revision: character.revision,
    currency: character.inventory.currency || 0,
    stash: inventoryItems(character, 'stash'),
    loadout: inventoryItems(character, 'loadout'),
    preparedEquipment: serializePreparedEquipment(character.inventory.preparedEquipment, inventoryVersion(character)),
    buyback: serializeBuyback(character.inventory)
  };
}

// Resolve an existing entry by identity; client modifiers are never accepted by equip.
async function updateInstanceEquipment(character, requested, expectedRevision) {
  const stash = inventoryItems(character, 'stash'); const loadout = inventoryItems(character, 'loadout');
  const old = serializePreparedEquipment(character.inventory.preparedEquipment, 3);
  const available = new Map(Object.values(old).filter(Boolean).map(item => [item.instance.instanceId, item]));
  const selected = new Set(); const equipment = {};
  for (const slot of EQUIPMENT_SLOTS) {
    const intent = requested[slot];
    if (intent == null || intent === '') { equipment[slot] = null; continue; }
    if (typeof intent !== 'object' || typeof intent.lootId !== 'string' || !INSTANCE_ID.test(intent.instanceId || '') ||
        Object.keys(intent).some(key => !['lootId', 'instanceId'].includes(key)) || selected.has(intent.instanceId)) invalidInventory();
    let item = available.get(intent.instanceId);
    if (!item) {
      const list = [loadout, stash].find(list => list.some(entry => entry.instance?.instanceId === intent.instanceId));
      const index = list?.findIndex(entry => entry.instance?.instanceId === intent.instanceId);
      if (list) { const entry = list.splice(index, 1)[0]; item = { lootId: entry.lootId, instance: copyEntry(entry.instance) }; }
    }
    if (!item || !matchesLootId(item, intent.lootId)) {
      throw { statusCode: 422, errorCode: 'ITEM_NOT_FOUND', message: 'The equipment instance is not present in the loadout, stash or prepared equipment.' };
    }
    selected.add(intent.instanceId); equipment[slot] = copyEntry(item);
  }
  for (const item of available.values()) {
    if (!selected.has(item.instance.instanceId)) receiveItem(loadout, 'loadout', { ...copyEntry(item), amount: 1 }, 3);
  }
  const snapshot = await writeSlotLists(character, expectedRevision, { stash, loadout, preparedEquipment: equipment });
  return { preparedEquipment: equipment, ...snapshot };
}

function takeRecoveryWeapon(loadout, stash) {
  for (const list of [loadout, stash]) {
    const index = lowestItemIndex(list, 'arming_sword');
    if (index !== -1) {
      const item = list.splice(index, 1)[0];
      return { lootId: item.lootId, instance: copyEntry(item.instance) };
    }
  }
  return createEquipment('arming_sword');
}

// Plan a sale on an in-memory candidate. No persisted state changes until the caller commits.
function applyShopSale(loadout, version, lootId, amount, sourceSlot) {
  const sellOffer = version === 3 && getEquipmentTemplate(lootId) ? getEquipmentTemplate(lootId) : getSellOffer(lootId);
  if (!sellOffer) throw { statusCode: 404, errorCode: 'ITEM_NOT_SELLABLE', message: `Item '${lootId}' cannot be sold to the shop.` };
  const index = loadout.findIndex(i => matchesLootId(i, lootId) && (version === 1 || i.slot === sourceSlot));
  const source = loadout[index];
  const unitSellValue = sellOffer.sellValue + (version === 3 ? (source?.instance?.modifiers?.gems || [])
    .reduce((sum, gem) => sum + (gem ? GEM_CATALOG[gem].sellValue : 0), 0) : 0);
  const revenue = unitSellValue * amount;
  if (!Number.isSafeInteger(revenue)) throw { statusCode: 400, errorCode: 'INTEGER_OVERFLOW', message: 'Calculated transaction value causes integer overflow.' };
  if (!source || source.amount < amount) throw { statusCode: 409, errorCode: 'INSUFFICIENT_ITEMS', message: `Loadout does not hold ${amount} unit(s) of '${lootId}'.` };
  if (source.amount === amount) loadout.splice(index, 1);
  else source.amount -= amount;
  return { revenue, buybackEntry: isBuybackEligible(lootId, version) ? {
    entryId: randomUUID(), lootId, amount, unitPrice: Math.ceil(unitSellValue * BUYBACK_PRICE_MULTIPLIER), soldAt: new Date()
  } : null };
}

function shopPurchaseCost(lootId, amount) {
  const offer = getBuyOffer(lootId);
  if (!offer) throw { statusCode: 404, errorCode: 'ITEM_NOT_OFFERED', message: `Item '${lootId}' is not offered in the shop.` };
  const cost = offer.buyPrice * amount;
  if (!Number.isSafeInteger(cost)) throw { statusCode: 400, errorCode: 'INTEGER_OVERFLOW', message: 'Calculated transaction cost causes integer overflow.' };
  return cost;
}

function safeTradeTotal(value) {
  if (!Number.isSafeInteger(value)) throw { statusCode: 400, errorCode: 'INTEGER_OVERFLOW', message: 'Calculated transaction value causes integer overflow.' };
  return value;
}

class InventoryService {
  /** One ticket, one revision, one receipt. Sales free space and fund purchases before placement. */
  static async shopTrade(accountId, { transactionId, expectedRevision, purchases, sales }) {
    const character = await Character.findOne({ accountId });
    if (!character) throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    requireMigratedInventory(character);
    const version = inventoryVersion(character);
    const canonicalLine = line => ({ lootId: normalizeLootId(line.lootId.trim()), amount: line.amount,
      ...(version >= 2 && Object.hasOwn(line, 'sourceSlot') ? { sourceSlot: line.sourceSlot } : {}) });
    const intent = { purchases: purchases.map(canonicalLine), sales: sales.map(canonicalLine) };
    // Ordered, normalized lines are the fingerprint: JSON property insertion order is fixed here.
    const fingerprint = JSON.stringify(intent);
    const receipt = (character.appliedShopReceipts || []).find(r => r.transactionId === transactionId);
    if (receipt) {
      if (receipt.type !== 'trade' || JSON.stringify(receipt.intent) !== fingerprint) {
        throw { statusCode: 409, errorCode: 'TRANSACTION_ID_REUSED', message: 'Transaction ID was previously applied with a different operation or parameters.' };
      }
      return { ...shopSnapshot(character), alreadySecured: true };
    }
    for (const line of intent.sales) requireSourceSlot(character, line.sourceSlot);
    if (version === 1) assertUnambiguousItems(character.inventory.loadout,
      [...intent.sales, ...intent.purchases].map(line => line.lootId));
    if (character.inventory.pendingReservation) throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot trade while a raid reservation is pending.' };
    if (character.revision !== expectedRevision) throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    const loadout = inventoryItems(character, 'loadout');
    const entries = [];
    let revenue = 0; let cost = 0;
    for (const line of intent.sales) {
      const sale = applyShopSale(loadout, version, line.lootId, line.amount, line.sourceSlot);
      revenue = safeTradeTotal(revenue + sale.revenue);
      if (sale.buybackEntry) entries.push(sale.buybackEntry);
    }
    for (const line of intent.purchases) cost = safeTradeTotal(cost + shopPurchaseCost(line.lootId, line.amount));
    const funded = safeTradeTotal((character.inventory.currency || 0) + revenue);
    const currency = safeTradeTotal(funded - cost);
    if (currency < 0) throw { statusCode: 409, errorCode: 'INSUFFICIENT_FUNDS', message: 'Not enough currency to complete trade.' };
    for (const line of intent.purchases) {
      // Reuse the single-buy receiver, including per-unit instance issuance for v3 equipment.
      if (version === 1) {
        const existing = loadout.find(item => matchesLootId(item, line.lootId));
        if (existing) safeTradeTotal(existing.amount + line.amount);
      }
      receiveItem(loadout, 'loadout', { lootId: line.lootId, amount: line.amount }, version);
    }
    const updated = await Character.findOneAndUpdate({ accountId, revision: expectedRevision,
      'inventory.pendingReservation': null,
      'inventory.currency': { $gte: Math.max(0, cost - revenue) },
      'inventory.buyback.sessionEpoch': buybackEpochFilter(character.inventory)
    }, {
      $set: { 'inventory.loadout': loadout, 'inventory.currency': currency },
      $inc: { revision: 1 },
      $push: {
        ...(entries.length ? { 'inventory.buyback.entries': { $each: entries, $slice: -BUYBACK_MAX_ENTRIES } } : {}),
        appliedShopReceipts: { $each: [{ transactionId, type: 'trade', intent, totalAmount: cost - revenue, timestamp: new Date() }], $slice: -64 }
      }
    }, { returnDocument: 'after' });
    if (!updated) {
      const latest = await Character.findOne({ accountId });
      if (latest?.inventory?.pendingReservation) throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot trade while a raid reservation is pending.' };
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during shop trade.' };
    }
    return { ...shopSnapshot(updated), alreadySecured: false };
  }

  /** Permanently fills the lowest empty socket; receipt and both lists commit together. */
  static async installGem(accountId, body) {
    const { transactionId, weaponList, weaponSlot, instanceId, gemList, gemSlot, gemLootId, expectedRevision } = body;
    if (typeof transactionId !== 'string' || !transactionId.trim() || transactionId.length > 128 ||
        !Object.hasOwn(CAPACITIES, weaponList) || !Object.hasOwn(CAPACITIES, gemList) ||
        typeof instanceId !== 'string' || !INSTANCE_ID.test(instanceId) || typeof gemLootId !== 'string' || !gemLootId ||
        !Number.isSafeInteger(expectedRevision) || expectedRevision < 0) {
      throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'Invalid input parameters.' };
    }
    assertSlot(weaponSlot); assertSlot(gemSlot);
    const character = await Character.findOne({ accountId });
    if (!character) throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    requireSlotInventory(character);
    if (inventoryVersion(character) !== 3) throw { statusCode: 409, errorCode: 'INVENTORY_MIGRATION_REQUIRED', message: 'Inventory migration is required.' };
    const intent = { weaponList, weaponSlot, instanceId, gemList, gemSlot, gemLootId };
    const receipt = (character.appliedShopReceipts || []).find(receipt => receipt.transactionId === transactionId);
    if (receipt) {
      if (receipt.type !== 'install_gem' || JSON.stringify(receipt.intent) !== JSON.stringify(intent)) {
        throw { statusCode: 409, errorCode: 'TRANSACTION_ID_REUSED', message: 'Transaction ID was previously applied with a different operation or parameters.' };
      }
      return { ...copyEntry(receipt.outcome), alreadySecured: true };
    }
    if (character.revision !== expectedRevision) throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    if (character.inventory.pendingReservation) throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot install gems while a raid reservation is pending.' };
    const lists = { stash: inventoryItems(character, 'stash'), loadout: inventoryItems(character, 'loadout') };
    const weapon = lists[weaponList].find(item => item.slot === weaponSlot);
    if (getEquipmentTemplate(weapon?.lootId)?.category !== 'Weapon' || weapon?.instance?.instanceId !== instanceId) {
      throw { statusCode: 409, errorCode: 'INSTANCE_MISMATCH', message: 'The slot does not contain the weapon instance.' };
    }
    const gem = lists[gemList].find(item => item.slot === gemSlot && item.lootId === gemLootId);
    if (!gem || !Object.hasOwn(GEM_CATALOG, gemLootId)) throw { statusCode: 409, errorCode: 'GEM_NOT_AVAILABLE', message: 'The gem is not available at the specified slot.' };
    const modifiers = weapon.instance.modifiers;
    if (!modifiers?.gemSocketCount) throw { statusCode: 409, errorCode: 'GEM_NO_SOCKETS', message: 'The weapon has no gem sockets.' };
    const socket = modifiers.gems.indexOf(null);
    if (socket === -1) throw { statusCode: 409, errorCode: 'GEM_NO_EMPTY_SOCKET', message: 'The weapon has no empty gem socket.' };
    modifiers.gems[socket] = gemLootId;
    if (--gem.amount === 0) lists[gemList].splice(lists[gemList].indexOf(gem), 1);
    const outcome = { stash: serializeItems(lists.stash, true), loadout: serializeItems(lists.loadout, true), revision: expectedRevision + 1 };
    return writeSlotLists(character, expectedRevision, lists, true,
      { transactionId, type: 'install_gem', intent, outcome, timestamp: new Date() });
  }

  /** Reorders one inventory list without changing item identity or amounts. */
  static async updateOrder(accountId, list, lootIds, expectedRevision, slotOrder) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }
    if (character.inventory.pendingReservation) {
      throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot reorder inventory while a reservation is pending.' };
    }
    requireMigratedInventory(character);
    const items = ['stash', 'loadout'].includes(list) ? character.inventory[list] : null;
    if (inventoryVersion(character) >= 2) {
      if (lootIds !== undefined || slotOrder === undefined) {
        throw { statusCode: 409, errorCode: 'INVENTORY_SLOT_ADDRESS_REQUIRED', message: 'A slot order is required for this inventory operation.' };
      }
      if (!items || !Array.isArray(slotOrder) || slotOrder.length !== items.length ||
          slotOrder.some(slot => !Number.isSafeInteger(slot) || slot < 0) ||
          new Set(slotOrder).size !== slotOrder.length ||
          slotOrder.some(slot => !items.some(item => item.slot === slot))) {
        throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'Order must be an exact permutation of the occupied slots.' };
      }
      if (items.length > CAPACITIES[list]) inventoryFull();
      const bySlot = new Map(items.map(item => [item.slot, item]));
      return writeSlotLists(character, expectedRevision,
        { [list]: slotOrder.map((slot, index) => ({ ...copyEntry(bySlot.get(slot)), slot: index })) }, true);
    }
    if (slotOrder !== undefined) requireSlotInventory(character);
    assertUnambiguousItems(items);
    if (!items || !Array.isArray(lootIds) || lootIds.length !== items.length ||
        lootIds.some(id => typeof id !== 'string' || !id.trim()) ||
        new Set(lootIds).size !== lootIds.length ||
        lootIds.some(id => !items.some(item => item.lootId === id))) {
      throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'Order must be an exact permutation of the target inventory list.' };
    }
    const byId = new Map(items.map(item => [item.lootId, item]));
    const ordered = lootIds.map(id => copyEntry(byId.get(id)));
    const updated = await Character.findOneAndUpdate(
      { accountId, revision: expectedRevision, 'inventory.pendingReservation': null },
      { $set: { [`inventory.${list}`]: ordered }, $inc: { revision: 1 } },
      { new: true }
    );
    if (!updated) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during inventory reorder.' };
    }
    return {
      stash: serializeItems(updated.inventory.stash),
      loadout: serializeItems(updated.inventory.loadout),
      revision: updated.revision
    };
  }

  /**
   * Retrieves the full inventory snapshot for the given account's character.
   * Used during login to hydrate the local Unity state.
   * @throws 404 if no character exists for this account.
   */
  static async getInventory(accountId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireMigratedInventory(character);
    const version = inventoryVersion(character);
    const sorted = version >= 2;

    const extractionReceipts = character.inventory.appliedExtractionReceipts || [];
    const lastExtraction = extractionReceipts.length > 0
      ? {
          raidId: extractionReceipts[extractionReceipts.length - 1].raidId,
          resultSequence: extractionReceipts[extractionReceipts.length - 1].resultSequence
        }
      : null;

    return {
      inventorySchemaVersion: version,
      currency:           character.inventory.currency || 0,
      stash:              serializeItems(character.inventory.stash, sorted),
      loadout:            serializeItems(character.inventory.loadout, sorted),
      preparedEquipment:  serializePreparedEquipment(character.inventory.preparedEquipment, version),
      pendingReservation: serializePendingReservation(character.inventory.pendingReservation, sorted, version),
      buyback: serializeBuyback(character.inventory),
      lastAppliedExtractionReceipt: lastExtraction,
      revision: character.revision || 0
    };
  }

  /**
   * Moves `amount` units of `lootId` from the stash to the loadout.
   * Triggered only by explicit player action in the Town UI (save-on-move).
   * @throws 404 if no character found.
   * @throws 409 if the stash does not hold enough units of that item.
   */
  static async moveToLoadout(accountId, lootId, amount, expectedRevision, sourceSlot) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireSourceSlot(character, sourceSlot);
    if (inventoryVersion(character) >= 2) return transferSlotItems(character, 'stash', 'loadout', lootId, amount, expectedRevision, sourceSlot);
    assertUnambiguousItems(character.inventory.stash, [lootId]);
    assertUnambiguousItems(character.inventory.loadout, [lootId]);
    
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }
    lootId = normalizeLootId(lootId);

    const stash   = character.inventory.stash;
    const loadout = character.inventory.loadout;

    const stashIndex = stash.findIndex(i => matchesLootId(i, lootId));
    const movedEntry = stashIndex === -1 ? null : copyEntry(stash[stashIndex]);
    if (stashIndex === -1 || stash[stashIndex].amount < amount) {
      throw {
        statusCode: 409,
        errorCode: 'INSUFFICIENT_STASH_ITEMS',
        message: `Stash does not hold ${amount} unit(s) of '${lootId}'.`
      };
    }

    // Deduct from stash
    if (stash[stashIndex].amount === amount) {
      stash.splice(stashIndex, 1);
    } else {
      stash[stashIndex].amount -= amount;
    }

    // Add to loadout, carrying the complete stored entry when it changes list.
    const loadoutIndex = loadout.findIndex(i => matchesLootId(i, lootId));
    if (loadoutIndex !== -1) {
      loadout[loadoutIndex].amount += amount;
    } else {
      loadout.push({ ...movedEntry, amount });
    }

    const updated = await Character.findOneAndUpdate(
      { accountId, revision: expectedRevision },
      {
        $set: { 'inventory.stash': stash, 'inventory.loadout': loadout },
        $inc: { revision: 1 }
      },
      { new: true }
    );

    if (!updated) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during move to loadout.' };
    }

    return {
      stash:   serializeItems(updated.inventory.stash),
      loadout: serializeItems(updated.inventory.loadout),
      revision: updated.revision
    };
  }

  /**
   * Moves `amount` units of `lootId` from the loadout to the stash.
   * Triggered only by explicit player action in the Town UI (save-on-move).
   * @throws 404 if no character found.
   * @throws 409 if the loadout does not hold enough units of that item.
   */
  static async moveToStash(accountId, lootId, amount, expectedRevision, sourceSlot) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireSourceSlot(character, sourceSlot);
    if (inventoryVersion(character) >= 2) return transferSlotItems(character, 'loadout', 'stash', lootId, amount, expectedRevision, sourceSlot);
    assertUnambiguousItems(character.inventory.stash, [lootId]);
    assertUnambiguousItems(character.inventory.loadout, [lootId]);
    
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }
    lootId = normalizeLootId(lootId);

    const stash   = character.inventory.stash;
    const loadout = character.inventory.loadout;

    const loadoutIndex = loadout.findIndex(i => matchesLootId(i, lootId));
    const movedEntry = loadoutIndex === -1 ? null : copyEntry(loadout[loadoutIndex]);
    if (loadoutIndex === -1 || loadout[loadoutIndex].amount < amount) {
      throw {
        statusCode: 409,
        errorCode: 'INSUFFICIENT_LOADOUT_ITEMS',
        message: `Loadout does not hold ${amount} unit(s) of '${lootId}'.`
      };
    }

    // Deduct from loadout
    if (loadout[loadoutIndex].amount === amount) {
      loadout.splice(loadoutIndex, 1);
    } else {
      loadout[loadoutIndex].amount -= amount;
    }

    // Add to stash, carrying the complete stored entry when it changes list.
    const stashIndex = stash.findIndex(i => matchesLootId(i, lootId));
    if (stashIndex !== -1) {
      stash[stashIndex].amount += amount;
    } else {
      stash.push({ ...movedEntry, amount });
    }

    const updated = await Character.findOneAndUpdate(
      { accountId, revision: expectedRevision },
      {
        $set: { 'inventory.stash': stash, 'inventory.loadout': loadout },
        $inc: { revision: 1 }
      },
      { new: true }
    );

    if (!updated) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during move to stash.' };
    }

    return {
      stash:   serializeItems(updated.inventory.stash),
      loadout: serializeItems(updated.inventory.loadout),
      revision: updated.revision
    };
  }

  /** Moves a complete entry: empty target, compatible merge, or swap. */
  static async moveSlot(accountId, fromList, fromSlot, toList, toSlot, lootId, expectedRevision) {
    const character = await Character.findOne({ accountId });
    if (!character) throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    requireSlotInventory(character);
    if (character.revision !== expectedRevision) throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    if (character.inventory.pendingReservation) throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot move slots while a reservation is pending.' };
    if (!Object.hasOwn(CAPACITIES, fromList) || !Object.hasOwn(CAPACITIES, toList)) {
      throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'Lists must be stash or loadout.' };
    }
    assertSlot(fromSlot); assertSlot(toSlot);
    if (toSlot >= CAPACITIES[toList]) inventoryFull();
    const from = inventoryItems(character, fromList);
    const to = fromList === toList ? from : inventoryItems(character, toList);
    const source = from.find(item => item.slot === fromSlot && matchesLootId(item, lootId));
    if (!source) throw { statusCode: 422, errorCode: 'ITEM_NOT_FOUND', message: 'Source slot does not contain the guarded item.' };
    if (fromList === toList && fromSlot === toSlot) return listsSnapshot(character);
    const target = to.find(item => item.slot === toSlot);
    if (!target) {
      if (fromList !== toList && to.length >= CAPACITIES[toList]) inventoryFull();
      from.splice(from.indexOf(source), 1);
      to.push({ ...source, slot: toSlot });
    } else if (matchesLootId(target, source.lootId) && !(inventoryVersion(character) === 3 && source.instance)) {
      addAmount(target, source.amount);
      from.splice(from.indexOf(source), 1);
    } else {
      // Swap-back is also a placement: an overflow source cannot accept it.
      if (fromSlot >= CAPACITIES[fromList]) inventoryFull();
      from[from.indexOf(source)] = { ...target, slot: fromSlot };
      to[to.indexOf(target)] = { ...source, slot: toSlot };
    }
    return writeSlotLists(character, expectedRevision, { [fromList]: from, [toList]: to }, true);
  }

  /** Splits one guarded entry into an empty slot in the same list, without merging. */
  static async splitSlot(accountId, list, sourceSlot, amount, destinationSlot, lootId, expectedRevision) {
    requireSplitEnabled();
    const character = await Character.findOne({ accountId });
    if (!character) throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    requireSlotInventory(character);
    if (character.revision !== expectedRevision) throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    if (character.inventory.pendingReservation) throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot split slots while a reservation is pending.' };
    if (!Object.hasOwn(CAPACITIES, list)) {
      throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'List must be stash or loadout.' };
    }
    assertSlot(sourceSlot); assertSlot(destinationSlot);
    if (sourceSlot === destinationSlot) {
      throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'Source and destination slots must differ.' };
    }
    const items = inventoryItems(character, list);
    const source = items.find(item => item.slot === sourceSlot && matchesLootId(item, lootId));
    if (!source) {
      throw { statusCode: 409, errorCode: list === 'stash' ? 'INSUFFICIENT_STASH_ITEMS' : 'INSUFFICIENT_LOADOUT_ITEMS',
        message: 'Source slot does not contain the guarded item.' };
    }
    if (inventoryVersion(character) === 3 && source.instance) {
      throw { statusCode: 409, errorCode: 'INSTANCE_NOT_SPLITTABLE', message: 'Equipment instances cannot be split.' };
    }
    if (!Number.isSafeInteger(amount) || amount < 1 || amount >= source.amount) {
      throw { statusCode: 400, errorCode: 'INVALID_QUANTITY', message: 'Split amount must be an integer between 1 and source amount minus 1.' };
    }
    if (destinationSlot >= CAPACITIES[list]) inventoryFull();
    if (items.some(item => item.slot === destinationSlot)) {
      throw { statusCode: 409, errorCode: 'SLOT_OCCUPIED', message: 'Destination slot is occupied.' };
    }
    // Overflow remains a source only; every new occupied slot counts toward capacity.
    if (items.length >= CAPACITIES[list]) inventoryFull();
    items.push({ ...copyEntry(source), slot: destinationSlot, amount });
    source.amount -= amount;
    return writeSlotLists(character, expectedRevision, { [list]: items }, true);
  }

  /**
   * Replaces the six equipment slot assignments for the character's loadout.
   * Modifies the Loadout (and Stash if needed) by deducting newly assigned items
   * and refunding unassigned items back to the Loadout.
   * @throws 404 if no character found.
   * @throws 422 if a new item is not found in either the Loadout or the Stash.
   */
  static async updatePreparedEquipment(accountId, slots, expectedRevision) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireMigratedInventory(character);
    const version = inventoryVersion(character);
    if (version === 1) {
      assertUnambiguousItems(character.inventory.stash);
      assertUnambiguousItems(character.inventory.loadout);
    }
    
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }
    if (version === 3) return updateInstanceEquipment(character, slots, expectedRevision);
    normalizePreparedEquipment(slots);

    const slotNames = EQUIPMENT_SLOTS;

    const oldEquipment = character.inventory.preparedEquipment || {};
    const removedItems = [];
    const addedItems = [];

    // Find the diff
    for (const slot of slotNames) {
      const oldItem = normalizeLootId(oldEquipment[slot] || '');
      const newItem = slots[slot] || '';
      
      if (oldItem !== newItem) {
        if (oldItem) removedItems.push(oldItem);
        if (newItem) addedItems.push(newItem);
      }
    }

    if (version === 2) {
      // Reassign already equipped units first, then deduct Inventory, then admit
      // remaining refunds against final capacity (not an intermediate full list).
      const refunds = removedItems.slice();
      for (const lootId of addedItems) {
        const refundIndex = refunds.indexOf(lootId);
        if (refundIndex !== -1) { refunds.splice(refundIndex, 1); continue; }
        let index = lowestItemIndex(character.inventory.loadout, lootId);
        const list = index !== -1 ? character.inventory.loadout : character.inventory.stash;
        if (index === -1) index = lowestItemIndex(list, lootId);
        if (index === -1) throw { statusCode: 422, errorCode: 'ITEM_NOT_FOUND', message: `'${lootId}' is not present in the loadout or stash.` };
        if (--list[index].amount === 0) list.splice(index, 1);
      }
      for (const lootId of refunds) receiveItem(character.inventory.loadout, 'loadout', { lootId, amount: 1 }, version);
    } else {
      // Legacy equipment behavior is unchanged: refund before deduction.
      for (const lootId of removedItems) {
        const existing = character.inventory.loadout.find(i => matchesLootId(i, lootId));
        if (existing) {
          existing.amount += 1;
        } else {
          character.inventory.loadout.push({ lootId, amount: 1 });
        }
      }

      for (const lootId of addedItems) {
        let deducted = false;
        const loadoutIndex = character.inventory.loadout.findIndex(i => matchesLootId(i, lootId));
        if (loadoutIndex !== -1 && character.inventory.loadout[loadoutIndex].amount > 0) {
          character.inventory.loadout[loadoutIndex].amount -= 1;
          if (character.inventory.loadout[loadoutIndex].amount === 0) {
            character.inventory.loadout.splice(loadoutIndex, 1);
          }
          deducted = true;
        } else {
          const stashIndex = character.inventory.stash.findIndex(i => matchesLootId(i, lootId));
          if (stashIndex !== -1 && character.inventory.stash[stashIndex].amount > 0) {
            character.inventory.stash[stashIndex].amount -= 1;
            if (character.inventory.stash[stashIndex].amount === 0) {
              character.inventory.stash.splice(stashIndex, 1);
            }
            deducted = true;
          }
        }
        if (!deducted) {
          throw { statusCode: 422, errorCode: 'ITEM_NOT_FOUND', message: `'${lootId}' is not present in the loadout or stash.` };
        }
      }
    }

    // Apply the new equipment assignments
    for (const slot of slotNames) {
      character.inventory.preparedEquipment[slot] = slots[slot] || '';
    }

    const updated = await Character.findOneAndUpdate(
      { accountId, revision: expectedRevision },
      {
        $set: {
          'inventory.stash': character.inventory.stash,
          'inventory.loadout': character.inventory.loadout,
          'inventory.preparedEquipment': character.inventory.preparedEquipment
        },
        $inc: { revision: 1 }
      },
      { new: true }
    );

    if (!updated) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during equipment update.' };
    }

    return {
      preparedEquipment: serializePreparedEquipment(updated.inventory.preparedEquipment),
      stash: inventoryItems(updated, 'stash'),
      loadout: inventoryItems(updated, 'loadout'),
      revision: updated.revision
    };
  }

  /**
   * Persists a raid reservation snapshot.
   * Called when the player commits a loadout for a raid.
   * Only a non-admitted reservation can recover ownership after an aborted launch.
   * An admitted reservation is replaced as a loss unless extraction is pending.
   * @throws 404 if no character found.
   */
  static async savePendingReservation(accountId, reservationId, preparedAbilities = null) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireMigratedInventory(character);
    const version = inventoryVersion(character);
    if (version === 1) {
      assertUnambiguousItems(character.inventory.stash);
      assertUnambiguousItems(character.inventory.loadout);
      assertUnambiguousItems(character.inventory.pendingReservation?.items);
    }
    const existingReservation = character.inventory.pendingReservation;
    if (existingReservation?.admittedAt && existingReservation.reservationId !== reservationId) {
      await assertNoPendingExtractionResult(character.accountId);
    }

    // Idempotency: if already reserved with the same reservationId, ensure recovery weapon invariant and return
    if (character.inventory.pendingReservation && character.inventory.pendingReservation.reservationId === reservationId) {
      const existingEq = character.inventory.pendingReservation.preparedEquipment || {};
      if (!existingEq.weaponSlot1 && !existingEq.weaponSlot2) {
        existingEq.weaponSlot1 = version === 3
          ? takeRecoveryWeapon(character.inventory.pendingReservation.items, character.inventory.stash) : 'arming_sword';
        character.inventory.pendingReservation.preparedEquipment = existingEq;
        if (version >= 2) {
          const updated = await Character.findOneAndUpdate(
            { accountId, revision: character.revision, 'inventory.pendingReservation.reservationId': reservationId },
            { $set: { 'inventory.pendingReservation.preparedEquipment.weaponSlot1': existingEq.weaponSlot1,
                ...(version === 3 ? { 'inventory.pendingReservation.items': character.inventory.pendingReservation.items,
                  'inventory.stash': character.inventory.stash } : {}) }, $inc: { revision: 1 } },
            { returnDocument: 'after' }
          );
          if (!updated) throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during reservation.' };
          return { pendingReservation: serializePendingReservation(updated.inventory.pendingReservation, true, version), revision: updated.revision };
        }
        if (typeof Character.updateOne === 'function') {
          await Character.updateOne(
            { _id: character._id },
            { $set: { 'inventory.pendingReservation.preparedEquipment.weaponSlot1': 'arming_sword' } }
          );
        }
      }
      return {
        pendingReservation: serializePendingReservation(character.inventory.pendingReservation, version >= 2, version),
        revision: character.revision || 0
      };
    }

    // If an unconsumed reservation from an aborted/cancelled launch is still pending while active loadout/equipment are empty,
    // recover its items and equipment before creating the new reservation so items are not lost.
    const currentEq = character.inventory.preparedEquipment || {};
    const hasActiveEquipment = EQUIPMENT_SLOTS.some(slot => Boolean(currentEq[slot]));
    if (
      character.inventory.pendingReservation &&
      !character.inventory.pendingReservation.admittedAt &&
      (version >= 2 || ((!character.inventory.loadout || character.inventory.loadout.length === 0) && !hasActiveEquipment))
    ) {
      if (version >= 2) restoreReservation(character);
      else {
        character.inventory.loadout = serializeItems(character.inventory.pendingReservation.items);
        character.inventory.preparedEquipment = serializePreparedEquipment(character.inventory.pendingReservation.preparedEquipment);
      }
    }
    if (version >= 2 && character.inventory.loadout.length > CAPACITIES.loadout) inventoryFull();

    // Mirror LocalProfileStore.TryPrepareExpeditionEquipment / TryPrepareRecoveryWeapon:
    // Guarantee at least one Main Hand weapon ('arming_sword') before creating the reservation,
    // reusing an owned unit from loadout or stash before granting the recovery weapon.
    if (version === 3 && !character.inventory.preparedEquipment.weaponSlot1 && !character.inventory.preparedEquipment.weaponSlot2) {
      character.inventory.preparedEquipment.weaponSlot1 = takeRecoveryWeapon(character.inventory.loadout, character.inventory.stash);
    } else if (!character.inventory.preparedEquipment.weaponSlot1 && !character.inventory.preparedEquipment.weaponSlot2) {
      const recoveryWeaponId = 'arming_sword';
      const loadoutIndex = version === 2 ? lowestItemIndex(character.inventory.loadout, recoveryWeaponId)
        : character.inventory.loadout.findIndex(i => matchesLootId(i, recoveryWeaponId) && i.amount >= 1);
      if (loadoutIndex !== -1) {
        if (character.inventory.loadout[loadoutIndex].amount === 1) {
          character.inventory.loadout.splice(loadoutIndex, 1);
        } else {
          character.inventory.loadout[loadoutIndex].amount -= 1;
        }
      } else {
        const stashIndex = version === 2 ? lowestItemIndex(character.inventory.stash, recoveryWeaponId)
          : character.inventory.stash.findIndex(i => matchesLootId(i, recoveryWeaponId) && i.amount >= 1);
        if (stashIndex !== -1) {
          if (character.inventory.stash[stashIndex].amount === 1) {
            character.inventory.stash.splice(stashIndex, 1);
          } else {
            character.inventory.stash[stashIndex].amount -= 1;
          }
        }
      }
      character.inventory.preparedEquipment.weaponSlot1 = recoveryWeaponId;
    }

    // Derive the reservation from the current persisted loadout and preparedEquipment
    const items = inventoryItems(character, 'loadout');
    const preparedEquipment = serializePreparedEquipment(character.inventory.preparedEquipment, version);

    const resolvedAbilities = preparedAbilities && typeof preparedAbilities === 'object'
      ? {
          slot1: typeof preparedAbilities.slot1 === 'string' ? preparedAbilities.slot1 : '',
          slot2: typeof preparedAbilities.slot2 === 'string' ? preparedAbilities.slot2 : ''
        }
      : (character.preparedAbilities ? {
          slot1: character.preparedAbilities.slot1 || '',
          slot2: character.preparedAbilities.slot2 || ''
        } : { slot1: '', slot2: '' });

    const pendingReservation = {
      reservationId,
      admittedAt: null,
      items,
      preparedEquipment,
      preparedAbilities: resolvedAbilities
    };

    const updated = await Character.findOneAndUpdate(
      { accountId, revision: character.revision },
      {
        $set: {
          'inventory.stash': character.inventory.stash,
          'inventory.pendingReservation': pendingReservation,
          'inventory.loadout': [],
          'inventory.preparedEquipment': version === 3 ? serializePreparedEquipment({}, version) : {}
        },
        $inc: { revision: 1 }
      },
      { new: true }
    );

    if (!updated) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during reservation.' };
    }

    return {
      pendingReservation: serializePendingReservation(updated.inventory.pendingReservation, version >= 2, version),
      revision: updated.revision
    };
  }

  /** Marks admission without consuming reserved ownership; same-id retries do not write. */
  static async admitPendingReservation(accountId, reservationId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireMigratedInventory(character);
    const reservation = character.inventory.pendingReservation;
    if (!reservation || reservation.reservationId !== reservationId) {
      throw { statusCode: 422, errorCode: 'NO_ACTIVE_RESERVATION', message: 'No active reservation matches this reservation id.' };
    }
    if (reservation.admittedAt) {
      return { pendingReservation: serializePendingReservation(reservation, inventoryVersion(character) >= 2, inventoryVersion(character)), revision: character.revision };
    }

    const updated = await Character.findOneAndUpdate(
      { accountId, revision: character.revision,
        'inventory.pendingReservation.reservationId': reservationId,
        'inventory.pendingReservation.admittedAt': null },
      { $set: { 'inventory.pendingReservation.admittedAt': new Date() }, $inc: { revision: 1 } },
      { new: true }
    );
    if (!updated) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during reservation admission.' };
    }
    return { pendingReservation: serializePendingReservation(updated.inventory.pendingReservation, inventoryVersion(updated) >= 2, inventoryVersion(updated)), revision: updated.revision };
  }

  /**
   * Consumes a reservation as a loss, or rolls back pre-admission ownership.
   * Admitted reservations can never roll back, and pending extraction blocks loss.
   * @throws 404 if no character found.
   */
  static async clearPendingReservation(accountId, { rollback = false, expectedReservationId } = {}) {
    const guarded = expectedReservationId !== undefined;
    if (guarded && (typeof expectedReservationId !== 'string' ||
        expectedReservationId.length < 1 || expectedReservationId.length > 128 ||
        !/^[A-Za-z0-9_-]+$/.test(expectedReservationId))) {
      throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'Invalid expected reservation ID.' };
    }
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireMigratedInventory(character);
    const version = inventoryVersion(character);
    if (character.inventory.pendingReservation?.admittedAt) {
      if (rollback) {
        throw { statusCode: 409, errorCode: 'RESERVATION_ADMITTED', message: 'An admitted reservation cannot be rolled back.' };
      }
      await assertNoPendingExtractionResult(character.accountId);
    }

    if (guarded) {
      if (!character.inventory.pendingReservation)
        return { pendingReservation: null, revision: character.revision };
      if (character.inventory.pendingReservation.reservationId !== expectedReservationId)
        throw { statusCode: 409, errorCode: 'RESERVATION_MISMATCH', message: 'A different reservation is active.' };
    }
    const setFields = { 'inventory.pendingReservation': null };
    if (rollback && character.inventory.pendingReservation) {
      const currentEq = character.inventory.preparedEquipment || {};
      const hasActiveEquipment = EQUIPMENT_SLOTS.some(slot => Boolean(currentEq[slot]));
      if (version >= 2) {
        restoreReservation(character);
        setFields['inventory.loadout'] = character.inventory.loadout;
        setFields['inventory.preparedEquipment'] = character.inventory.preparedEquipment;
      } else if ((!character.inventory.loadout || character.inventory.loadout.length === 0) && !hasActiveEquipment) {
        setFields['inventory.loadout'] = serializeItems(character.inventory.pendingReservation.items);
        setFields['inventory.preparedEquipment'] = serializePreparedEquipment(character.inventory.pendingReservation.preparedEquipment);
      }
    }

    const updated = await Character.findOneAndUpdate(
      { accountId, revision: character.revision,
        ...(guarded ? { 'inventory.pendingReservation.reservationId': expectedReservationId } : {}) },
      {
        $set: setFields,
        $inc: { revision: 1 }
      },
      { new: true }
    );

    if (!updated) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Internal revision conflict clearing reservation.' };
    }

    return { pendingReservation: null, revision: updated.revision };
  }

  /**
   * Persists the loot obtained from a successful raid extraction.
   *
   * The operation is idempotent: if the same (raidId, resultSequence) pair
   * was already applied, the method returns { alreadySecured: true } without
   * modifying the document. This allows Unity to safely retry on network failure.
   *
   * Mirrors the invariant enforced by LocalProfileStore.TryCommitExtraction in Unity:
   * the loadout MUST be empty before a new extraction can be committed. This
   * guarantees ítems are never silently duplicated across multiple extractions.
   *
   * @param {string} accountId
   * @param {string} raidId        - RaidGenerationId from the Fusion session
   * @param {number} resultSequence - ResultSequence from the Fusion participant
   * @param {{ lootId: string, amount: number }[]} items - May be empty (no loot run)
   * @throws 404 if no character found.
   * @throws 409 LOADOUT_NOT_EMPTY if the loadout already has items.
   */
  static async commitExtraction(accountId, raidId, resultSequence, items) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireMigratedInventory(character);
    const version = inventoryVersion(character);
    items = version === 3 ? validateResultItems(items) : normalizeItems(items);

    // Idempotency check: has this exact extraction already been applied?
    const receipts = character.inventory.appliedExtractionReceipts || [];
    const alreadyApplied = receipts.some(
      r => r.raidId === raidId && r.resultSequence === resultSequence
    );
    if (alreadyApplied) {
      return {
        alreadySecured: true,
        loadout: inventoryItems(character, 'loadout')
      };
    }

    if (character.inventory.loadout && character.inventory.loadout.length > 0) {
      throw { statusCode: 409, errorCode: 'LOADOUT_NOT_EMPTY', message: 'Cannot commit extraction while loadout is not empty.' };
    }

    if (version >= 2) {
      const loadout = [];
      for (const item of items) receiveItem(loadout, 'loadout', item, version);
      const updated = await Character.findOneAndUpdate(
        { accountId, revision: character.revision,
          'inventory.appliedExtractionReceipts': { $not: { $elemMatch: { raidId } } } },
        { $set: { 'inventory.loadout': loadout,
            ...(character.inventory.pendingReservation ? { 'inventory.preparedEquipment': version === 3
              ? resultEquipmentToInstances(equipmentToResult(character.inventory.pendingReservation.preparedEquipment))
              : character.inventory.pendingReservation.preparedEquipment } : {}) },
          $unset: { 'inventory.pendingReservation': 1 }, $inc: { revision: 1 },
          $push: { 'inventory.appliedExtractionReceipts': { raidId, resultSequence } } },
        { returnDocument: 'after' }
      );
      if (!updated) throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during extraction commit.' };
      return { alreadySecured: false, loadout: inventoryItems(updated, 'loadout') };
    }

    // Restore prepared equipment exactly as it was when the raid started
    if (character.inventory.pendingReservation) {
      character.inventory.preparedEquipment = character.inventory.pendingReservation.preparedEquipment || {};
      character.markModified('inventory.preparedEquipment');
      
      // Clear the pending reservation so the backend matches the client's confirmed state
      character.inventory.pendingReservation = null;
      character.markModified('inventory.pendingReservation');
    }

    // Add extracted items into the loadout.
    // Items array may be empty (the player extracted but carried no loot).
    // Duplicate protection against double-extraction is guaranteed by the
    // (raidId, resultSequence) idempotency check above.
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

    // Never prune durable replay barriers, including receipts from unified commits.
    character.inventory.appliedExtractionReceipts.push({ raidId, resultSequence });

    character.markModified('inventory.loadout');
    character.markModified('inventory.appliedExtractionReceipts');
    await character.save();

    return {
      alreadySecured: false,
      loadout: serializeItems(character.inventory.loadout)
    };
  }

  /**
  /**
   * Processes a shop sale. Removes items from the loadout and adds authoritative currency.
   * 
   * INVARIANT: Currency balance is calculated strictly from the server's authoritative
   * catalog (shopCatalog.js), ignoring any client declared value.
   */
  static async shopSell(accountId, lootId, amount, declaredSellValue, expectedRevision, transactionId, sourceSlot) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireSourceSlot(character, sourceSlot);
    const version = inventoryVersion(character);
    if (version === 1) assertUnambiguousItems(character.inventory.loadout, [lootId]);

    if (!Number.isInteger(amount) || amount < 1 || amount > MAX_TRANSACTION_AMOUNT) {
      throw {
        statusCode: 400,
        errorCode: 'INVALID_QUANTITY',
        message: `Amount must be an integer between 1 and ${MAX_TRANSACTION_AMOUNT}.`
      };
    }

    lootId = normalizeLootId(lootId);
    
    // 1. Idempotency check with strict payload binding:
    // If already applied with identical intent, return immediately.
    // If transactionId was reused with a different intent, reject as conflict.
    if (character.appliedShopReceipts) {
      const existingReceipt = character.appliedShopReceipts.find(r => r.transactionId === transactionId);
      if (existingReceipt) {
        if (existingReceipt.type !== 'sell' ||
            (version >= 2 && existingReceipt.sourceSlot !== sourceSlot) ||
            (existingReceipt.lootId && existingReceipt.lootId !== lootId) ||
            (existingReceipt.amount && existingReceipt.amount !== amount)) {
          throw {
            statusCode: 409,
            errorCode: 'TRANSACTION_ID_REUSED',
            message: 'Transaction ID was previously applied with a different operation or parameters.'
          };
        }
        return { ...shopSnapshot(character), alreadySecured: true };
      }
    }

    // Applied receipts remain replayable during a reservation; only new mutations are blocked.
    if (character.inventory.pendingReservation) {
      throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot trade while a raid reservation is pending.' };
    }

    // 2. Validate revision for new mutations
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }

    const loadout = character.inventory.loadout;
    const { revenue: authoritativeRevenue, buybackEntry } = applyShopSale(loadout, version, lootId, amount, sourceSlot);
    const buybackPush = buybackEntry ? {
      'inventory.buyback.entries': { $each: [buybackEntry], $slice: -BUYBACK_MAX_ENTRIES }
    } : {};
    const updated = await Character.findOneAndUpdate(
      { accountId, revision: expectedRevision, 'inventory.pendingReservation': null,
        'inventory.buyback.sessionEpoch': buybackEpochFilter(character.inventory) },
      {
        $set: { 'inventory.loadout': loadout },
        $inc: { revision: 1, 'inventory.currency': authoritativeRevenue },
        $push: {
          ...buybackPush,
          appliedShopReceipts: {
            $each: [{ transactionId, type: 'sell', lootId, amount, ...(version >= 2 ? { sourceSlot } : {}), totalAmount: authoritativeRevenue, timestamp: new Date() }],
            $slice: -64
          }
        }
      },
      { new: true, returnDocument: 'after' }
    );

    if (!updated) {
      const latest = await Character.findOne({ accountId });
      if (latest?.inventory?.pendingReservation) {
        throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot trade while a raid reservation is pending.' };
      }
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during shop sale.' };
    }

    return shopSnapshot(updated);
  }

  /**
   * Processes a shop purchase. Removes currency and adds items to the loadout.
   * 
   * INVARIANT: Currency balance is calculated strictly from the server's authoritative
   * catalog (shopCatalog.js), ignoring any client declared price.
   * Enforced locally and atomically by the MongoDB query filter
   * ('inventory.currency': { $gte: authoritativeCost }).
   */
  static async shopBuy(accountId, lootId, amount, declaredPrice, expectedRevision, transactionId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    requireMigratedInventory(character);
    const version = inventoryVersion(character);
    if (version === 1) assertUnambiguousItems(character.inventory.loadout, [lootId]);

    if (!Number.isInteger(amount) || amount < 1 || amount > MAX_TRANSACTION_AMOUNT) {
      throw {
        statusCode: 400,
        errorCode: 'INVALID_QUANTITY',
        message: `Amount must be an integer between 1 and ${MAX_TRANSACTION_AMOUNT}.`
      };
    }

    lootId = normalizeLootId(lootId);
    
    // 1. Idempotency check with strict payload binding:
    // If already applied with identical intent, return immediately.
    // If transactionId was reused with a different intent, reject as conflict.
    if (character.appliedShopReceipts) {
      const existingReceipt = character.appliedShopReceipts.find(r => r.transactionId === transactionId);
      if (existingReceipt) {
        if (existingReceipt.type !== 'buy' ||
            (existingReceipt.lootId && existingReceipt.lootId !== lootId) ||
            (existingReceipt.amount && existingReceipt.amount !== amount)) {
          throw {
            statusCode: 409,
            errorCode: 'TRANSACTION_ID_REUSED',
            message: 'Transaction ID was previously applied with a different operation or parameters.'
          };
        }
        return { ...shopSnapshot(character), alreadySecured: true };
      }
    }

    // Applied receipts remain replayable during a reservation; only new mutations are blocked.
    if (character.inventory.pendingReservation) {
      throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot trade while a raid reservation is pending.' };
    }

    // 2. Validate revision for new mutations
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }

    const authoritativeCost = shopPurchaseCost(lootId, amount);
    
    const currentCurrency = character.inventory.currency || 0;
    if (currentCurrency < authoritativeCost) {
      throw {
        statusCode: 409,
        errorCode: 'INSUFFICIENT_FUNDS',
        message: `Not enough currency to purchase '${lootId}'. Required: ${authoritativeCost}, available: ${currentCurrency}.`
      };
    }

    const loadout = character.inventory.loadout;
    receiveItem(loadout, 'loadout', { lootId, amount }, version);

    const updated = await Character.findOneAndUpdate(
      { accountId, revision: expectedRevision, 'inventory.pendingReservation': null, 'inventory.currency': { $gte: authoritativeCost } },
      {
        $set: { 'inventory.loadout': loadout },
        $inc: { revision: 1, 'inventory.currency': -authoritativeCost },
        $push: {
          appliedShopReceipts: {
            $each: [{ transactionId, type: 'buy', lootId, amount, totalAmount: authoritativeCost, timestamp: new Date() }],
            $slice: -64
          }
        }
      },
      { new: true, returnDocument: 'after' }
    );

    if (!updated) {
      const latest = await Character.findOne({ accountId });
      if (latest?.inventory?.pendingReservation) {
        throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot trade while a raid reservation is pending.' };
      }
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict or insufficient funds during shop purchase.' };
    }

    return shopSnapshot(updated);
  }
  /** Rebuys a sale line at its frozen price, within the current Town epoch. */
  static async shopBuyback(accountId, entryId, amount, expectedRevision, transactionId) {
    const character = await Character.findOne({ accountId });
    if (!character) throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    requireMigratedInventory(character);
    const version = inventoryVersion(character);
    if (!Number.isInteger(amount) || amount < 1 || amount > MAX_TRANSACTION_AMOUNT) {
      throw { statusCode: 400, errorCode: 'INVALID_QUANTITY', message: 'Invalid buyback quantity.' };
    }
    const receipt = (character.appliedShopReceipts || []).find(r => r.transactionId === transactionId);
    if (receipt) {
      if (receipt.type !== 'buyback' || receipt.entryId !== entryId || receipt.amount !== amount) {
        throw { statusCode: 409, errorCode: 'TRANSACTION_ID_REUSED', message: 'Transaction ID was previously applied with different parameters.' };
      }
      return { ...shopSnapshot(character), alreadySecured: true };
    }
    if (character.inventory.pendingReservation) {
      throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot trade while a raid reservation is pending.' };
    }
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }
    const entry = character.inventory.buyback?.entries?.find(e => e.entryId === entryId);
    if (!entry) throw { statusCode: 404, errorCode: 'BUYBACK_ENTRY_NOT_FOUND', message: 'Buyback entry not found in this Town session.' };
    // v3 disables equipment buy-back, including ledger entries sold before the instance migration.
    if (version >= 3 && !isBuybackEligible(entry.lootId, version)) {
      throw { statusCode: 409, errorCode: 'BUYBACK_NOT_ELIGIBLE', message: 'This item cannot be bought back.' };
    }
    if (amount > entry.amount) throw { statusCode: 409, errorCode: 'INSUFFICIENT_BUYBACK_UNITS', message: 'Not enough units in this sale line.' };
    const cost = entry.unitPrice * amount;
    if (!Number.isSafeInteger(cost) || cost < 0) throw { statusCode: 400, errorCode: 'INTEGER_OVERFLOW', message: 'Invalid transaction cost.' };
    if ((character.inventory.currency || 0) < cost) throw { statusCode: 409, errorCode: 'INSUFFICIENT_FUNDS', message: 'Not enough currency.' };
    if (version === 1) assertUnambiguousItems(character.inventory.loadout, [entry.lootId]);
    const loadout = inventoryItems(character, 'loadout');
    receiveItem(loadout, 'loadout', { lootId: entry.lootId, amount }, version);
    const entryUpdate = amount === entry.amount
      ? { $pull: { 'inventory.buyback.entries': { entryId } } }
      : {};
    const increments = { revision: 1, 'inventory.currency': -cost };
    if (amount < entry.amount) increments['inventory.buyback.entries.$.amount'] = -amount;
    const updated = await Character.findOneAndUpdate({
      accountId, revision: expectedRevision, 'inventory.pendingReservation': null,
      'inventory.currency': { $gte: cost },
      'inventory.buyback.sessionEpoch': buybackEpochFilter(character.inventory),
      'inventory.buyback.entries': { $elemMatch: { entryId, amount: { $gte: amount } } }
    }, {
      ...entryUpdate,
      $set: { 'inventory.loadout': loadout },
      $inc: increments,
      $push: { appliedShopReceipts: { $each: [{ transactionId, type: 'buyback', entryId,
        lootId: entry.lootId, amount, totalAmount: cost, timestamp: new Date() }], $slice: -64 } }
    }, { returnDocument: 'after' });
    if (!updated) {
      const latest = await Character.findOne({ accountId });
      if (latest?.inventory?.pendingReservation) throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot trade while a raid reservation is pending.' };
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during buyback.' };
    }
    return shopSnapshot(updated);
  }
}

module.exports = InventoryService;

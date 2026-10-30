// src/services/InventoryService.js
const Character = require('../models/Character');
const AuthoritativeExtractionResult = require('../models/AuthoritativeExtractionResult');
const { randomUUID } = require('crypto');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');
const {
  normalizeCharacterInventory,
  normalizeItems,
  normalizeLootId,
  normalizePreparedEquipment
} = require('./InventoryLootIdNormalizer');
const {
  MAX_TRANSACTION_AMOUNT,
  BUYBACK_MAX_ENTRIES,
  BUYBACK_PRICE_MULTIPLIER,
  isBuybackEligible,
  getBuyOffer,
  getSellOffer
} = require('../config/shopCatalog');

/**
 * Serializes an item array from Mongoose documents to plain DTOs.
 * @param {Array} items - Mongoose subdocument array
 * @returns {{ lootId: string, amount: number }[]}
 */
function serializeItems(items) {
  return (items || []).map(i => ({ lootId: i.lootId, amount: i.amount }));
}

/**
 * Serializes the preparedEquipment subdocument to a plain object.
 * Iterates over EQUIPMENT_SLOTS so new slots are covered automatically.
 */
function serializePreparedEquipment(eq) {
  const clean = (val) => (!val || val === 'null' ? '' : val);
  const result = {};
  for (const slot of EQUIPMENT_SLOTS) {
    result[slot] = clean(eq ? eq[slot] : undefined);
  }
  return result;
}

/**
 * Serializes the pendingReservation subdocument to a plain object, or null.
 */
function serializePendingReservation(res) {
  if (!res || !res.reservationId) return null;
  return {
    reservationId:    res.reservationId,
    admittedAt:       res.admittedAt ? res.admittedAt.toISOString() : null,
    items:            serializeItems(res.items),
    preparedEquipment: serializePreparedEquipment(res.preparedEquipment)
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
    stash: serializeItems(character.inventory.stash),
    loadout: serializeItems(character.inventory.loadout),
    preparedEquipment: serializePreparedEquipment(character.inventory.preparedEquipment),
    buyback: serializeBuyback(character.inventory)
  };
}

class InventoryService {
  /** Reorders one inventory list without changing item identity or amounts. */
  static async updateOrder(accountId, list, lootIds, expectedRevision) {
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
    const items = ['stash', 'loadout'].includes(list) ? character.inventory[list] : null;
    if (!items || !Array.isArray(lootIds) || lootIds.length !== items.length ||
        lootIds.some(id => typeof id !== 'string' || !id.trim()) ||
        new Set(lootIds).size !== lootIds.length ||
        lootIds.some(id => !items.some(item => item.lootId === id))) {
      throw { statusCode: 400, errorCode: 'VALIDATION_FAILED', message: 'Order must be an exact permutation of the target inventory list.' };
    }
    const byId = new Map(items.map(item => [item.lootId, item]));
    const ordered = lootIds.map(id => ({ lootId: id, amount: byId.get(id).amount }));
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

    if (normalizeCharacterInventory(character)) {
      await character.save();
    }

    const extractionReceipts = character.inventory.appliedExtractionReceipts || [];
    const lastExtraction = extractionReceipts.length > 0
      ? {
          raidId: extractionReceipts[extractionReceipts.length - 1].raidId,
          resultSequence: extractionReceipts[extractionReceipts.length - 1].resultSequence
        }
      : null;

    return {
      currency:           character.inventory.currency || 0,
      stash:              serializeItems(character.inventory.stash),
      loadout:            serializeItems(character.inventory.loadout),
      preparedEquipment:  serializePreparedEquipment(character.inventory.preparedEquipment),
      pendingReservation: serializePendingReservation(character.inventory.pendingReservation),
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
  static async moveToLoadout(accountId, lootId, amount, expectedRevision) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    normalizeCharacterInventory(character);
    
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }
    lootId = normalizeLootId(lootId);

    const stash   = character.inventory.stash;
    const loadout = character.inventory.loadout;

    const stashIndex = stash.findIndex(i => i.lootId === lootId);
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

    // Add to loadout
    const loadoutIndex = loadout.findIndex(i => i.lootId === lootId);
    if (loadoutIndex !== -1) {
      loadout[loadoutIndex].amount += amount;
    } else {
      loadout.push({ lootId, amount });
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
  static async moveToStash(accountId, lootId, amount, expectedRevision) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    normalizeCharacterInventory(character);
    
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }
    lootId = normalizeLootId(lootId);

    const stash   = character.inventory.stash;
    const loadout = character.inventory.loadout;

    const loadoutIndex = loadout.findIndex(i => i.lootId === lootId);
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

    // Add to stash
    const stashIndex = stash.findIndex(i => i.lootId === lootId);
    if (stashIndex !== -1) {
      stash[stashIndex].amount += amount;
    } else {
      stash.push({ lootId, amount });
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

    normalizeCharacterInventory(character);
    
    if (character.revision !== expectedRevision) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict.' };
    }
    normalizePreparedEquipment(slots);

    const slotNames = EQUIPMENT_SLOTS;

    const oldEquipment = character.inventory.preparedEquipment || {};
    const removedItems = [];
    const addedItems = [];

    // Find the diff
    for (const slot of slotNames) {
      const oldItem = oldEquipment[slot] || '';
      const newItem = slots[slot] || '';
      
      if (oldItem !== newItem) {
        if (oldItem) removedItems.push(oldItem);
        if (newItem) addedItems.push(newItem);
      }
    }

    // Refund removed items to loadout
    for (const lootId of removedItems) {
      const existing = character.inventory.loadout.find(i => i.lootId === lootId);
      if (existing) {
        existing.amount += 1;
      } else {
        character.inventory.loadout.push({ lootId, amount: 1 });
      }
    }

    // Deduct added items from loadout (or stash)
    for (const lootId of addedItems) {
      let deducted = false;
      const loadoutIndex = character.inventory.loadout.findIndex(i => i.lootId === lootId);
      
      if (loadoutIndex !== -1 && character.inventory.loadout[loadoutIndex].amount > 0) {
        character.inventory.loadout[loadoutIndex].amount -= 1;
        if (character.inventory.loadout[loadoutIndex].amount === 0) {
          character.inventory.loadout.splice(loadoutIndex, 1);
        }
        deducted = true;
      } else {
        const stashIndex = character.inventory.stash.findIndex(i => i.lootId === lootId);
        if (stashIndex !== -1 && character.inventory.stash[stashIndex].amount > 0) {
          character.inventory.stash[stashIndex].amount -= 1;
          if (character.inventory.stash[stashIndex].amount === 0) {
            character.inventory.stash.splice(stashIndex, 1);
          }
          deducted = true;
        }
      }

      if (!deducted) {
        throw {
          statusCode: 422,
          errorCode: 'ITEM_NOT_FOUND',
          message: `'${lootId}' is not present in the loadout or stash.`
        };
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
      stash: serializeItems(updated.inventory.stash),
      loadout: serializeItems(updated.inventory.loadout),
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

    const existingReservation = character.inventory.pendingReservation;
    if (existingReservation?.admittedAt && existingReservation.reservationId !== reservationId) {
      await assertNoPendingExtractionResult(character.accountId);
    }

    normalizeCharacterInventory(character);

    // Idempotency: if already reserved with the same reservationId, ensure recovery weapon invariant and return
    if (character.inventory.pendingReservation && character.inventory.pendingReservation.reservationId === reservationId) {
      const existingEq = character.inventory.pendingReservation.preparedEquipment || {};
      if (!existingEq.weaponSlot1 && !existingEq.weaponSlot2) {
        existingEq.weaponSlot1 = 'arming_sword';
        character.inventory.pendingReservation.preparedEquipment = existingEq;
        if (typeof Character.updateOne === 'function') {
          await Character.updateOne(
            { _id: character._id },
            { $set: { 'inventory.pendingReservation.preparedEquipment.weaponSlot1': 'arming_sword' } }
          );
        }
      }
      return {
        pendingReservation: serializePendingReservation(character.inventory.pendingReservation),
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
      (!character.inventory.loadout || character.inventory.loadout.length === 0) &&
      !hasActiveEquipment
    ) {
      character.inventory.loadout = serializeItems(character.inventory.pendingReservation.items);
      character.inventory.preparedEquipment = serializePreparedEquipment(character.inventory.pendingReservation.preparedEquipment);
    }

    // Mirror LocalProfileStore.TryPrepareExpeditionEquipment / TryPrepareRecoveryWeapon:
    // Guarantee at least one Main Hand weapon ('arming_sword') before creating the reservation,
    // reusing an owned unit from loadout or stash before granting the recovery weapon.
    if (!character.inventory.preparedEquipment.weaponSlot1 && !character.inventory.preparedEquipment.weaponSlot2) {
      const recoveryWeaponId = 'arming_sword';
      const loadoutIndex = character.inventory.loadout.findIndex(i => i.lootId === recoveryWeaponId && i.amount >= 1);
      if (loadoutIndex !== -1) {
        if (character.inventory.loadout[loadoutIndex].amount === 1) {
          character.inventory.loadout.splice(loadoutIndex, 1);
        } else {
          character.inventory.loadout[loadoutIndex].amount -= 1;
        }
      } else {
        const stashIndex = character.inventory.stash.findIndex(i => i.lootId === recoveryWeaponId && i.amount >= 1);
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
    const items = serializeItems(character.inventory.loadout);
    const preparedEquipment = serializePreparedEquipment(character.inventory.preparedEquipment);

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
          'inventory.preparedEquipment': {}
        },
        $inc: { revision: 1 }
      },
      { new: true }
    );

    if (!updated) {
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during reservation.' };
    }

    return {
      pendingReservation: serializePendingReservation(updated.inventory.pendingReservation),
      revision: updated.revision
    };
  }

  /** Marks admission without consuming reserved ownership; same-id retries do not write. */
  static async admitPendingReservation(accountId, reservationId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    const reservation = character.inventory.pendingReservation;
    if (!reservation || reservation.reservationId !== reservationId) {
      throw { statusCode: 422, errorCode: 'NO_ACTIVE_RESERVATION', message: 'No active reservation matches this reservation id.' };
    }
    if (reservation.admittedAt) {
      return { pendingReservation: serializePendingReservation(reservation), revision: character.revision };
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
    return { pendingReservation: serializePendingReservation(updated.inventory.pendingReservation), revision: updated.revision };
  }

  /**
   * Consumes a reservation as a loss, or rolls back pre-admission ownership.
   * Admitted reservations can never roll back, and pending extraction blocks loss.
   * @throws 404 if no character found.
   */
  static async clearPendingReservation(accountId, { rollback = false } = {}) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    if (character.inventory.pendingReservation?.admittedAt) {
      if (rollback) {
        throw { statusCode: 409, errorCode: 'RESERVATION_ADMITTED', message: 'An admitted reservation cannot be rolled back.' };
      }
      await assertNoPendingExtractionResult(character.accountId);
    }

    normalizeCharacterInventory(character);

    const setFields = { 'inventory.pendingReservation': null };
    if (rollback && character.inventory.pendingReservation) {
      const currentEq = character.inventory.preparedEquipment || {};
      const hasActiveEquipment = EQUIPMENT_SLOTS.some(slot => Boolean(currentEq[slot]));
      if ((!character.inventory.loadout || character.inventory.loadout.length === 0) && !hasActiveEquipment) {
        setFields['inventory.loadout'] = serializeItems(character.inventory.pendingReservation.items);
        setFields['inventory.preparedEquipment'] = serializePreparedEquipment(character.inventory.pendingReservation.preparedEquipment);
      }
    }

    const updated = await Character.findOneAndUpdate(
      { accountId, revision: character.revision },
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

    if (normalizeCharacterInventory(character)) {
      await character.save();
    }
    items = normalizeItems(items);

    // Idempotency check: has this exact extraction already been applied?
    const receipts = character.inventory.appliedExtractionReceipts || [];
    const alreadyApplied = receipts.some(
      r => r.raidId === raidId && r.resultSequence === resultSequence
    );
    if (alreadyApplied) {
      return {
        alreadySecured: true,
        loadout: serializeItems(character.inventory.loadout)
      };
    }

    if (character.inventory.loadout && character.inventory.loadout.length > 0) {
      throw { statusCode: 409, errorCode: 'LOADOUT_NOT_EMPTY', message: 'Cannot commit extraction while loadout is not empty.' };
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
  static async shopSell(accountId, lootId, amount, declaredSellValue, expectedRevision, transactionId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    normalizeCharacterInventory(character);

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
            (existingReceipt.lootId && existingReceipt.lootId !== lootId) ||
            (existingReceipt.amount && existingReceipt.amount !== amount)) {
          throw {
            statusCode: 409,
            errorCode: 'TRANSACTION_ID_REUSED',
            message: 'Transaction ID was previously applied with a different operation or parameters.'
          };
        }
        return {
          alreadySecured: true,
          revision: character.revision,
          currency: character.inventory.currency || 0,
          stash: serializeItems(character.inventory.stash),
          loadout: serializeItems(character.inventory.loadout),
          preparedEquipment: serializePreparedEquipment(character.inventory.preparedEquipment),
          buyback: serializeBuyback(character.inventory)
        };
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

    // 3. Resolve authoritative sell offer from catalog
    const sellOffer = getSellOffer(lootId);
    if (!sellOffer) {
      throw {
        statusCode: 404,
        errorCode: 'ITEM_NOT_SELLABLE',
        message: `Item '${lootId}' cannot be sold to the shop.`
      };
    }

    const unitSellValue = sellOffer.sellValue;
    const authoritativeRevenue = unitSellValue * amount;
    if (!Number.isSafeInteger(authoritativeRevenue)) {
      throw {
        statusCode: 400,
        errorCode: 'INTEGER_OVERFLOW',
        message: 'Calculated transaction value causes integer overflow.'
      };
    }

    const loadout = character.inventory.loadout;
    const loadoutIndex = loadout.findIndex(i => i.lootId === lootId);

    if (loadoutIndex === -1 || loadout[loadoutIndex].amount < amount) {
      throw {
        statusCode: 409,
        errorCode: 'INSUFFICIENT_ITEMS',
        message: `Loadout does not hold ${amount} unit(s) of '${lootId}'.`
      };
    }

    if (loadout[loadoutIndex].amount === amount) {
      loadout.splice(loadoutIndex, 1);
    } else {
      loadout[loadoutIndex].amount -= amount;
    }

    const buybackPush = isBuybackEligible(lootId) ? {
      'inventory.buyback.entries': {
        $each: [{ entryId: randomUUID(), lootId, amount,
          unitPrice: Math.ceil(unitSellValue * BUYBACK_PRICE_MULTIPLIER), soldAt: new Date() }],
        $slice: -BUYBACK_MAX_ENTRIES
      }
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
            $each: [{ transactionId, type: 'sell', lootId, amount, totalAmount: authoritativeRevenue, timestamp: new Date() }],
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

    normalizeCharacterInventory(character);

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
        return {
          alreadySecured: true,
          revision: character.revision,
          currency: character.inventory.currency || 0,
          stash: serializeItems(character.inventory.stash),
          loadout: serializeItems(character.inventory.loadout),
          preparedEquipment: serializePreparedEquipment(character.inventory.preparedEquipment),
          buyback: serializeBuyback(character.inventory)
        };
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

    // 3. Resolve authoritative buy offer from catalog
    const buyOffer = getBuyOffer(lootId);
    if (!buyOffer) {
      throw {
        statusCode: 404,
        errorCode: 'ITEM_NOT_OFFERED',
        message: `Item '${lootId}' is not offered in the shop.`
      };
    }

    const unitPrice = buyOffer.buyPrice;
    const authoritativeCost = unitPrice * amount;
    if (!Number.isSafeInteger(authoritativeCost)) {
      throw {
        statusCode: 400,
        errorCode: 'INTEGER_OVERFLOW',
        message: 'Calculated transaction cost causes integer overflow.'
      };
    }
    
    const currentCurrency = character.inventory.currency || 0;
    if (currentCurrency < authoritativeCost) {
      throw {
        statusCode: 409,
        errorCode: 'INSUFFICIENT_FUNDS',
        message: `Not enough currency to purchase '${lootId}'. Required: ${authoritativeCost}, available: ${currentCurrency}.`
      };
    }

    const loadout = character.inventory.loadout;
    const loadoutIndex = loadout.findIndex(i => i.lootId === lootId);

    if (loadoutIndex !== -1) {
      loadout[loadoutIndex].amount += amount;
    } else {
      loadout.push({ lootId, amount });
    }

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
    if (amount > entry.amount) throw { statusCode: 409, errorCode: 'INSUFFICIENT_BUYBACK_UNITS', message: 'Not enough units in this sale line.' };
    const cost = entry.unitPrice * amount;
    if (!Number.isSafeInteger(cost) || cost < 0) throw { statusCode: 400, errorCode: 'INTEGER_OVERFLOW', message: 'Invalid transaction cost.' };
    if ((character.inventory.currency || 0) < cost) throw { statusCode: 409, errorCode: 'INSUFFICIENT_FUNDS', message: 'Not enough currency.' };
    const loadout = normalizeItems(serializeItems(character.inventory.loadout));
    const owned = loadout.find(i => i.lootId === entry.lootId);
    if (owned) owned.amount += amount;
    else loadout.push({ lootId: entry.lootId, amount });
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

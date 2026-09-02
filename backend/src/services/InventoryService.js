// src/services/InventoryService.js
const Character = require('../models/Character');

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
 */
function serializePreparedEquipment(eq) {
  if (!eq) {
    return { weaponSlot1: '', weaponSlot2: '', helmet: '', armor: '', gloves: '', boots: '' };
  }
  return {
    weaponSlot1: eq.weaponSlot1 || '',
    weaponSlot2: eq.weaponSlot2 || '',
    helmet:      eq.helmet      || '',
    armor:       eq.armor       || '',
    gloves:      eq.gloves      || '',
    boots:       eq.boots       || ''
  };
}

/**
 * Serializes the pendingReservation subdocument to a plain object, or null.
 */
function serializePendingReservation(res) {
  if (!res || !res.reservationId) return null;
  return {
    reservationId:    res.reservationId,
    items:            serializeItems(res.items),
    preparedEquipment: serializePreparedEquipment(res.preparedEquipment)
  };
}

class InventoryService {
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

    return {
      stash:              serializeItems(character.inventory.stash),
      loadout:            serializeItems(character.inventory.loadout),
      preparedEquipment:  serializePreparedEquipment(character.inventory.preparedEquipment),
      pendingReservation: serializePendingReservation(character.inventory.pendingReservation)
    };
  }

  /**
   * Moves `amount` units of `lootId` from the stash to the loadout.
   * Triggered only by explicit player action in the Town UI (save-on-move).
   * @throws 404 if no character found.
   * @throws 409 if the stash does not hold enough units of that item.
   */
  static async moveToLoadout(accountId, lootId, amount) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

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

    character.markModified('inventory.stash');
    character.markModified('inventory.loadout');
    await character.save();

    return {
      stash:   serializeItems(stash),
      loadout: serializeItems(loadout)
    };
  }

  /**
   * Moves `amount` units of `lootId` from the loadout to the stash.
   * Triggered only by explicit player action in the Town UI (save-on-move).
   * @throws 404 if no character found.
   * @throws 409 if the loadout does not hold enough units of that item.
   */
  static async moveToStash(accountId, lootId, amount) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

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

    character.markModified('inventory.stash');
    character.markModified('inventory.loadout');
    await character.save();

    return {
      stash:   serializeItems(stash),
      loadout: serializeItems(loadout)
    };
  }

  /**
   * Replaces the six equipment slot assignments for the character's loadout.
   * Each slot must either be empty or reference a lootId present in the loadout.
   * @throws 404 if no character found.
   * @throws 422 if any non-empty slot references an item not in the loadout.
   */
  static async updatePreparedEquipment(accountId, slots) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    const slotNames = ['weaponSlot1', 'weaponSlot2', 'helmet', 'armor', 'gloves', 'boots'];

    // Count how many times each lootId is referenced across slots.
    const usageCount = {};
    for (const slot of slotNames) {
      const lootId = slots[slot] || '';
      if (!lootId) continue;
      usageCount[lootId] = (usageCount[lootId] || 0) + 1;
    }

    for (const [lootId, count] of Object.entries(usageCount)) {
      const ownedItem = character.inventory.loadout.find(i => i.lootId === lootId);
      if (!ownedItem) {
        throw {
          statusCode: 422,
          errorCode: 'ITEM_NOT_IN_LOADOUT',
          message: `'${lootId}' is not present in the loadout.`
        };
      }
      if (ownedItem.amount < count) {
        throw {
          statusCode: 422,
          errorCode: 'INSUFFICIENT_LOADOUT_ITEMS',
          message: `'${lootId}' occupies ${count} slot(s) but only ${ownedItem.amount} unit(s) are owned.`
        };
      }
    }

    for (const slot of slotNames) {
      character.inventory.preparedEquipment[slot] = slots[slot] || '';
    }

    character.markModified('inventory.preparedEquipment');
    await character.save();

    return {
      preparedEquipment: serializePreparedEquipment(character.inventory.preparedEquipment)
    };
  }

  /**
   * Persists a raid reservation snapshot.
   * Called when the player commits a loadout for a raid.
   * The reservation captures the loadout state so it can be restored after a disconnection.
   * @throws 404 if no character found.
   */
  static async savePendingReservation(accountId, reservationId, items, preparedEquipment) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    character.inventory.pendingReservation = {
      reservationId,
      items: items || [],
      preparedEquipment: preparedEquipment || {}
    };

    // Mirror what the Unity client does: the loadout travels inside the reservation.
    // Clearing it here prevents item duplication on extraction and blocks Alt+F4 recovery exploits.
    character.inventory.loadout = [];
    character.inventory.preparedEquipment = {};
    
    character.markModified('inventory.loadout');
    character.markModified('inventory.preparedEquipment');
    character.markModified('inventory.pendingReservation');
    await character.save();

    return {
      pendingReservation: serializePendingReservation(character.inventory.pendingReservation)
    };
  }

  /**
   * Clears the pending reservation after a raid completes (success or voluntary exit).
   * @throws 404 if no character found.
   */
  static async clearPendingReservation(accountId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    character.inventory.pendingReservation = null;
    character.markModified('inventory.pendingReservation');
    await character.save();

    return { pendingReservation: null };
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
    const MAX_EXTRACTION_RECEIPTS = 256;

    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

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

    // Record the receipt, evicting oldest entries beyond the cap.
    character.inventory.appliedExtractionReceipts.push({ raidId, resultSequence });
    while (character.inventory.appliedExtractionReceipts.length > MAX_EXTRACTION_RECEIPTS) {
      character.inventory.appliedExtractionReceipts.shift();
    }

    character.markModified('inventory.loadout');
    character.markModified('inventory.appliedExtractionReceipts');
    await character.save();

    return {
      alreadySecured: false,
      loadout: serializeItems(character.inventory.loadout)
    };
  }
}

module.exports = InventoryService;

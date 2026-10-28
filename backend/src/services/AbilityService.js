// src/services/AbilityService.js
'use strict';

const Character = require('../models/Character');
const { getAbilityOffer } = require('../config/abilityCatalog');

class AbilityService {
  /**
   * Purchases an ability for the authenticated character.
   *
   * Invariants:
   * 1. Idempotent: Replaying the same transactionId returns 200 with alreadySecured: true.
   * 2. Atomic: Currency deduction, ability unlock, receipt logging, and revision increment
   *    are applied in a single database update.
   * 3. Authoritative: Price is resolved server-side from ABILITY_CATALOG.
   * 4. Gated: Blocked if a raid reservation is pending, if insufficient funds,
   *    or if the ability is already unlocked.
   */
  static async purchaseAbility(accountId, { transactionId, abilityId, expectedRevision }) {
    if (!transactionId || typeof transactionId !== 'string' || transactionId.trim() === '') {
      throw { statusCode: 400, errorCode: 'INVALID_TRANSACTION_ID', message: 'transactionId is required.' };
    }
    if (!abilityId || typeof abilityId !== 'string' || abilityId.trim() === '') {
      throw { statusCode: 400, errorCode: 'INVALID_ABILITY_ID', message: 'abilityId is required.' };
    }
    if (typeof expectedRevision !== 'number' || expectedRevision < 0 || !Number.isInteger(expectedRevision)) {
      throw { statusCode: 400, errorCode: 'INVALID_REVISION', message: 'expectedRevision must be a non-negative integer.' };
    }

    const normAbilityId = abilityId.trim().toLowerCase();

    // 1. Fetch character
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    // 2. Check for duplicate receipt (idempotent replay)
    const existingReceipt = (character.appliedShopReceipts || []).find(r => r.transactionId === transactionId);
    if (existingReceipt) {
      if (existingReceipt.type === 'ability_purchase' && existingReceipt.abilityId === normAbilityId) {
        return {
          alreadySecured: true,
          transactionId,
          abilityId: normAbilityId,
          price: existingReceipt.totalAmount,
          currency: character.inventory.currency || 0,
          unlockedAbilities: character.unlockedAbilities || [],
          revision: character.revision || 0
        };
      }
      throw {
        statusCode: 409,
        errorCode: 'TRANSACTION_ID_REUSED',
        message: 'Transaction ID has already been used for a different operation.'
      };
    }

    // 3. Pending reservation guard
    if (character.inventory && character.inventory.pendingReservation) {
      throw {
        statusCode: 409,
        errorCode: 'RESERVATION_PENDING',
        message: 'Cannot purchase abilities while a raid reservation is pending.'
      };
    }

    // 4. Revision guard
    if (character.revision !== expectedRevision) {
      throw {
        statusCode: 409,
        errorCode: 'REVISION_CONFLICT',
        message: `Revision conflict. Expected revision ${expectedRevision}, but current revision is ${character.revision}.`
      };
    }

    // 5. Already unlocked guard
    if (character.unlockedAbilities && character.unlockedAbilities.includes(normAbilityId)) {
      throw {
        statusCode: 409,
        errorCode: 'ALREADY_UNLOCKED',
        message: `Ability '${normAbilityId}' is already unlocked.`
      };
    }

    // 6. Resolve authoritative price from catalog
    const offer = getAbilityOffer(normAbilityId);
    if (!offer) {
      throw {
        statusCode: 404,
        errorCode: 'ABILITY_NOT_OFFERED',
        message: `Ability '${normAbilityId}' is not offered by the vendor.`
      };
    }

    const price = offer.price;
    const currentCurrency = character.inventory.currency || 0;
    if (currentCurrency < price) {
      throw {
        statusCode: 422,
        errorCode: 'INSUFFICIENT_FUNDS',
        message: `Insufficient currency to purchase '${normAbilityId}'. Required: ${price}, available: ${currentCurrency}.`
      };
    }

    // 7. Atomic update
    const updated = await Character.findOneAndUpdate(
      {
        accountId,
        revision: expectedRevision,
        'inventory.pendingReservation': null,
        'inventory.currency': { $gte: price },
        unlockedAbilities: { $ne: normAbilityId },
        'appliedShopReceipts.transactionId': { $ne: transactionId }
      },
      {
        $inc: {
          'inventory.currency': -price,
          revision: 1
        },
        $push: {
          unlockedAbilities: normAbilityId,
          appliedShopReceipts: {
            $each: [{
              transactionId,
              type: 'ability_purchase',
              abilityId: normAbilityId,
              amount: 1,
              totalAmount: price,
              timestamp: new Date()
            }],
            $slice: -64
          }
        }
      },
      { new: true, returnDocument: 'after' }
    );

    if (!updated) {
      // Re-read latest state to diagnose failure
      const latest = await Character.findOne({ accountId });
      if (latest?.inventory?.pendingReservation) {
        throw { statusCode: 409, errorCode: 'RESERVATION_PENDING', message: 'Cannot purchase abilities while a raid reservation is pending.' };
      }
      if (latest?.unlockedAbilities?.includes(normAbilityId)) {
        throw { statusCode: 409, errorCode: 'ALREADY_UNLOCKED', message: `Ability '${normAbilityId}' is already unlocked.` };
      }
      if ((latest?.inventory?.currency || 0) < price) {
        throw { statusCode: 422, errorCode: 'INSUFFICIENT_FUNDS', message: 'Insufficient currency.' };
      }
      throw { statusCode: 409, errorCode: 'REVISION_CONFLICT', message: 'Revision conflict during ability purchase.' };
    }

    return {
      alreadySecured: false,
      transactionId,
      abilityId: normAbilityId,
      price,
      currency: updated.inventory.currency || 0,
      unlockedAbilities: updated.unlockedAbilities || [],
      revision: updated.revision || 0
    };
  }
}

module.exports = AbilityService;

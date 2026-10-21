// src/services/RaidResultService.js
'use strict';

const crypto = require('crypto');
const env = require('../config/env');
const AuthoritativeExtractionResult = require('../models/AuthoritativeExtractionResult');
const { buildCanonicalResultString } = require('../utils/raidResultCanonicalizer');
const { normalizeItems, normalizePreparedEquipment } = require('./InventoryLootIdNormalizer');

function areItemsEquivalent(existingItems, incomingItems) {
  const normA = normalizeItems(existingItems || []);
  const normB = normalizeItems(incomingItems || []);
  if (normA.length !== normB.length) return false;
  const mapA = new Map(normA.map(i => [i.lootId, i.amount]));
  for (const item of normB) {
    if (mapA.get(item.lootId) !== item.amount) return false;
  }
  return true;
}

function areEquipmentEquivalent(existingEq, incomingEq) {
  const normA = normalizePreparedEquipment(existingEq || {});
  const normB = normalizePreparedEquipment(incomingEq || {});
  const slots = ['weaponSlot1', 'weaponSlot2', 'offHand1', 'helmet', 'armor', 'gloves', 'boots', 'offHand2'];
  for (const slot of slots) {
    if ((normA[slot] || '') !== (normB[slot] || '')) return false;
  }
  return true;
}

class RaidResultService {
  /**
   * Verifies the host HMAC signature over the full canonical payload,
   * enforces producer authorization, prevents conflicting overwrites,
   * and creates an AuthoritativeExtractionResult.
   *
   * @param {string} callerAccountId The host / caller account ID
   * @param {Object} payload The result payload
   */
  static async publishHostResult(callerAccountId, payload) {
    if (!payload || typeof payload !== 'object') {
      throw {
        statusCode: 400,
        errorCode: 'INVALID_RESULT_PAYLOAD',
        message: 'Missing result payload.',
      };
    }

    const {
      raidId,
      resultId,
      resultSequence,
      targetAccountId,
      targetCharacterId,
      accountId: bodyAccountId,
      items,
      preparedEquipment,
      experienceGranted,
      hostSignature,
      issuedAt
    } = payload;

    let recipientAccountId = targetAccountId || bodyAccountId;
    const characterId = targetCharacterId;
    const targetIdentifier = targetAccountId || bodyAccountId || targetCharacterId || callerAccountId;
    const effectiveResultId = resultId || (resultSequence !== undefined ? `${raidId}:${targetIdentifier}:${resultSequence}` : `${raidId}:${targetIdentifier}`);

    if (!raidId || !effectiveResultId || !hostSignature) {
      throw {
        statusCode: 400,
        errorCode: 'INVALID_RESULT_PAYLOAD',
        message: 'Missing required fields for authoritative result (raidId, resultId/resultSequence, hostSignature).',
      };
    }

    // Canonical payload verification
    const canonicalMessage = buildCanonicalResultString({
      raidId,
      resultId: effectiveResultId,
      targetAccountId: targetIdentifier,
      items: items || [],
      preparedEquipment: preparedEquipment || {},
      experienceGranted: experienceGranted || 0,
      issuedAt: issuedAt || ''
    });

    const expectedSignature = crypto
      .createHmac('sha256', env.webhookSecret)
      .update(canonicalMessage)
      .digest('hex');

    const signatureBuf = Buffer.from(String(hostSignature), 'hex');
    const expectedBuf = Buffer.from(expectedSignature, 'hex');

    if (
      signatureBuf.length === 0 ||
      expectedBuf.length === 0 ||
      signatureBuf.length !== expectedBuf.length ||
      !crypto.timingSafeEqual(signatureBuf, expectedBuf)
    ) {
      throw {
        statusCode: 401,
        errorCode: 'INVALID_HOST_SIGNATURE',
        message: 'The host signature is invalid or the payload has been tampered with.',
      };
    }

    if (issuedAt !== undefined && issuedAt !== null && issuedAt !== '') {
      const parsedIssuedAt = new Date(issuedAt);
      if (Number.isNaN(parsedIssuedAt.getTime())) {
        throw {
          statusCode: 400,
          errorCode: 'INVALID_RESULT_PAYLOAD',
          message: 'Invalid issuedAt timestamp.',
        };
      }
    }

    if (!recipientAccountId && targetCharacterId) {
      const Character = require('../models/Character');
      let charDoc = null;
      try {
        charDoc = await Character.findById(targetCharacterId);
      } catch {
        charDoc = null;
      }
      if (!charDoc) {
        throw {
          statusCode: 404,
          errorCode: 'CHARACTER_NOT_FOUND',
          message: 'Target character for extraction result was not found.',
        };
      }
      recipientAccountId = charDoc.accountId.toString();
    }
    if (!recipientAccountId) {
      recipientAccountId = callerAccountId;
    }

    // Check for existing result (immutability and conflict prevention)
    const existing = await AuthoritativeExtractionResult.findOne({
      $or: [
        { resultId: effectiveResultId },
        { raidId, accountId: recipientAccountId }
      ]
    });

    if (existing) {
      if (existing.consumed) {
        throw {
          statusCode: 409,
          errorCode: 'RESULT_ALREADY_CONSUMED',
          message: 'This authoritative extraction result has already been consumed and cannot be overwritten.',
        };
      }

      const isIdentical = (
        existing.experienceGranted === (experienceGranted || 0) &&
        areItemsEquivalent(existing.items, items || []) &&
        areEquipmentEquivalent(existing.preparedEquipment, preparedEquipment || {})
      );

      if (!isIdentical) {
        throw {
          statusCode: 409,
          errorCode: 'RESULT_CONFLICT',
          message: 'An authoritative extraction result already exists with conflicting rewards.',
        };
      }

      return {
        status: 'published',
        alreadyPublished: true,
        raidId,
        accountId: recipientAccountId,
        resultId: effectiveResultId
      };
    }

    // Create the immutable authoritative result
    await AuthoritativeExtractionResult.create({
      resultId: effectiveResultId,
      raidId,
      accountId: recipientAccountId,
      ...(characterId ? { characterId } : {}),
      items: items || [],
      preparedEquipment: preparedEquipment || {},
      experienceGranted: experienceGranted || 0,
      hostSignature,
      consumed: false,
      consumedAt: null,
      issuedAt: issuedAt ? new Date(issuedAt) : new Date()
    });

    return {
      status: 'published',
      alreadyPublished: false,
      raidId,
      accountId: recipientAccountId,
      resultId: effectiveResultId
    };
  }
}

module.exports = RaidResultService;

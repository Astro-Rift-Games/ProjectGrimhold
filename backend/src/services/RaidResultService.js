// src/services/RaidResultService.js
'use strict';

const crypto = require('crypto');
const env = require('../config/env');
const AuthoritativeExtractionResult = require('../models/AuthoritativeExtractionResult');
const Character = require('../models/Character');
const { buildCanonicalResultString } = require('../utils/raidResultCanonicalizer');
const { normalizeItems, normalizePreparedEquipment } = require('./InventoryLootIdNormalizer');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');

const MAX_EXTRACTION_ITEMS = 30;
const MAX_RAID_EXPERIENCE_BUDGET = 5000;

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
   * Verifies the host HMAC signature or enforces Bounded Player-Host Authority (TASK-223 / SEC-223),
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

    if (!raidId || !effectiveResultId) {
      throw {
        statusCode: 400,
        errorCode: 'INVALID_RESULT_PAYLOAD',
        message: 'Missing required fields for authoritative result (raidId, resultId/resultSequence).',
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

    // Determine authority verification mode:
    // 1. If hostSignature is provided and non-empty, verify against env.webhookSecret (Dedicated Server / tool worker).
    // 2. If hostSignature is absent / empty, enforce Bounded Player-Host Authority (MVP client-hosted topology).
    let isDedicatedServerVerified = false;
    const rawSignature = hostSignature !== undefined && hostSignature !== null ? String(hostSignature).trim() : '';

    if (rawSignature !== '') {
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

      const signatureBuf = Buffer.from(rawSignature, 'hex');
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

      isDedicatedServerVerified = true;
    }

    // Resolve recipient Character and recipientAccountId
    let recipientChar = null;
    if (targetCharacterId) {
      try {
        recipientChar = await Character.findById(targetCharacterId);
      } catch {
        recipientChar = null;
      }
      if (!recipientChar) {
        throw {
          statusCode: 404,
          errorCode: 'CHARACTER_NOT_FOUND',
          message: 'Target character for extraction result was not found.',
        };
      }
      recipientAccountId = recipientChar.accountId.toString();
    } else if (recipientAccountId) {
      try {
        recipientChar = await Character.findOne({ accountId: recipientAccountId });
      } catch {
        recipientChar = null;
      }
      if (!recipientChar && !isDedicatedServerVerified) {
        throw {
          statusCode: 404,
          errorCode: 'CHARACTER_NOT_FOUND',
          message: 'Target character for extraction result was not found.',
        };
      }
    } else {
      recipientAccountId = callerAccountId;
      try {
        recipientChar = await Character.findOne({ accountId: callerAccountId });
      } catch {
        recipientChar = null;
      }
      if (!recipientChar && !isDedicatedServerVerified) {
        throw {
          statusCode: 404,
          errorCode: 'CHARACTER_NOT_FOUND',
          message: 'Host caller character was not found.',
        };
      }
    }

    // Under Bounded Player-Host Authority (TASK-223 / SEC-223):
    if (!isDedicatedServerVerified) {
      // 1. Caller identity
      if (!callerAccountId) {
        throw {
          statusCode: 401,
          errorCode: 'UNAUTHORIZED',
          message: 'Authentication required for player-hosted extraction result.',
        };
      }

      const callerChar = await Character.findOne({ accountId: callerAccountId });
      if (!callerChar) {
        throw {
          statusCode: 404,
          errorCode: 'CHARACTER_NOT_FOUND',
          message: 'Host caller character not found.',
        };
      }

      // 2. Active reservation on target character (must have entered a raid)
      if (!recipientChar || !recipientChar.inventory || !recipientChar.inventory.pendingReservation) {
        throw {
          statusCode: 422,
          errorCode: 'NO_ACTIVE_RESERVATION',
          message: 'Target character does not have an active raid reservation.',
        };
      }

      // 3. Host raid membership validation
      if (callerAccountId !== recipientAccountId) {
        const callerInRaid = (callerChar.inventory && callerChar.inventory.pendingReservation) ||
          (callerChar.inventory && callerChar.inventory.appliedExtractionReceipts &&
            callerChar.inventory.appliedExtractionReceipts.some(r => r.raidId === raidId));
        if (!callerInRaid) {
          throw {
            statusCode: 403,
            errorCode: 'HOST_NOT_IN_RAID',
            message: 'Host is not a participant in this raid.',
          };
        }
      }

      // 4. Loot bounds and structure validation
      if (items !== undefined && items !== null) {
        if (!Array.isArray(items)) {
          throw {
            statusCode: 400,
            errorCode: 'INVALID_RESULT_PAYLOAD',
            message: 'items must be an array.',
          };
        }
        if (items.length > MAX_EXTRACTION_ITEMS) {
          throw {
            statusCode: 422,
            errorCode: 'EXCEEDS_INVENTORY_CAPACITY',
            message: `Item list exceeds maximum capacity of ${MAX_EXTRACTION_ITEMS} slots.`,
          };
        }
        for (const item of items) {
          if (!item || typeof item !== 'object') {
            throw {
              statusCode: 422,
              errorCode: 'INVALID_ITEM_SPECIFICATION',
              message: 'Invalid item entry.',
            };
          }
          if (typeof item.lootId !== 'string' || !/^[a-zA-Z0-9_-]{1,64}$/.test(item.lootId.trim())) {
            throw {
              statusCode: 422,
              errorCode: 'INVALID_ITEM_SPECIFICATION',
              message: `Invalid lootId: ${item.lootId}`,
            };
          }
          if (!Number.isInteger(item.amount) || item.amount < 1 || item.amount > 999) {
            throw {
              statusCode: 422,
              errorCode: 'INVALID_ITEM_SPECIFICATION',
              message: `Invalid item amount for ${item.lootId}: ${item.amount}`,
            };
          }
        }
      }

      // 5. Equipment slot format check
      if (preparedEquipment && typeof preparedEquipment === 'object') {
        for (const slot of EQUIPMENT_SLOTS) {
          const val = preparedEquipment[slot];
          if (val !== undefined && val !== null && val !== '') {
            if (typeof val !== 'string' || !/^[a-zA-Z0-9_-]{1,64}$/.test(val.trim())) {
              throw {
                statusCode: 422,
                errorCode: 'INVALID_EQUIPMENT_SPECIFICATION',
                message: `Invalid equipment lootId for slot ${slot}: ${val}`,
              };
            }
          }
        }
      }

      // 6. Experience budget validation
      const exp = Number(experienceGranted || 0);
      if (!Number.isInteger(exp) || exp < 0) {
        throw {
          statusCode: 422,
          errorCode: 'INVALID_EXPERIENCE',
          message: 'experienceGranted must be a non-negative integer.',
        };
      }
      if (exp > MAX_RAID_EXPERIENCE_BUDGET) {
        throw {
          statusCode: 422,
          errorCode: 'EXCEEDS_EXPERIENCE_BUDGET',
          message: `Experience granted (${exp}) exceeds allowable raid budget (${MAX_RAID_EXPERIENCE_BUDGET} XP).`,
        };
      }
    }

    // Results expire, but the character's atomic reward receipt must never expire.
    const consumedRaid = await Character.exists({
      accountId: recipientAccountId,
      'inventory.appliedExtractionReceipts.raidId': raidId
    });
    if (consumedRaid) {
      throw {
        statusCode: 409,
        errorCode: 'RESULT_ALREADY_CONSUMED',
        message: 'This raid has already been consumed by the recipient.',
      };
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
      hostSignature: rawSignature,
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

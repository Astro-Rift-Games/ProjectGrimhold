// src/services/RaidResultService.js
'use strict';

const crypto = require('crypto');
const env = require('../config/env');
const AuthoritativeExtractionResult = require('../models/AuthoritativeExtractionResult');

class RaidResultService {
  /**
   * Verifies the host HMAC signature and creates/updates the AuthoritativeExtractionResult.
   * @param {string} accountId The host's account ID (from JWT)
   * @param {Object} payload The result payload
   */
  static async publishHostResult(accountId, payload) {
    const { raidId, resultSequence, items, preparedEquipment, experienceGranted, hostSignature } = payload;

    if (!raidId || resultSequence === undefined || !hostSignature) {
      throw {
        statusCode: 400,
        errorCode: 'INVALID_RESULT_PAYLOAD',
        message: 'Missing required fields for authoritative result.',
      };
    }

    // Verify signature
    // We sign: raidId + resultSequence
    const messageToSign = `${raidId}:${resultSequence}`;
    
    const expectedSignature = crypto
      .createHmac('sha256', env.webhookSecret)
      .update(messageToSign)
      .digest('hex');

    if (hostSignature !== expectedSignature) {
      throw {
        statusCode: 401,
        errorCode: 'INVALID_HOST_SIGNATURE',
        message: 'The host signature is invalid.',
      };
    }

    // Signature matches, this is a trusted host.
    // Upsert the authoritative result.
    await AuthoritativeExtractionResult.findOneAndUpdate(
      { raidId, accountId },
      { 
        items: items || [], 
        experienceGranted: experienceGranted || 0,
        ...(preparedEquipment ? { preparedEquipment } : {})
      },
      { upsert: true, new: true }
    );

    return { status: 'published', raidId, accountId };
  }
}

module.exports = RaidResultService;

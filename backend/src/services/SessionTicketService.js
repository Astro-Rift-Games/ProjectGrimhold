// src/services/SessionTicketService.js
const crypto = require('crypto');
const Character = require('../models/Character');
const env = require('../config/env');

const TOWN_TICKET_TTL_SECONDS = 1800; // 30 minutes
const RAID_TICKET_TTL_SECONDS = 600;  // 10 minutes

class SessionTicketService {
  /**
   * Helper to sign a payload object using HMAC-SHA256 and base64url encoding.
   */
  static signPayload(payload) {
    const json = JSON.stringify(payload);
    const payloadB64 = Buffer.from(json, 'utf8').toString('base64url');
    const signature = crypto
      .createHmac('sha256', env.jwtSecret)
      .update(payloadB64)
      .digest('hex');
    return `${payloadB64}.${signature}`;
  }

  /**
   * Helper to verify and decode a signed ticket string.
   */
  static verifyAndDecode(ticketString) {
    if (typeof ticketString !== 'string' || !ticketString.includes('.')) {
      return { valid: false, error: 'MALFORMED_TICKET' };
    }

    const parts = ticketString.split('.');
    if (parts.length !== 2) {
      return { valid: false, error: 'MALFORMED_TICKET' };
    }

    const [payloadB64, providedSigHex] = parts;
    const expectedSigHex = crypto
      .createHmac('sha256', env.jwtSecret)
      .update(payloadB64)
      .digest('hex');

    const providedBuf = Buffer.from(providedSigHex, 'hex');
    const expectedBuf = Buffer.from(expectedSigHex, 'hex');

    if (providedBuf.length !== expectedBuf.length || !crypto.timingSafeEqual(providedBuf, expectedBuf)) {
      return { valid: false, error: 'INVALID_SIGNATURE' };
    }

    try {
      const json = Buffer.from(payloadB64, 'base64url').toString('utf8');
      const payload = JSON.parse(json);
      return { valid: true, payload };
    } catch {
      return { valid: false, error: 'MALFORMED_PAYLOAD' };
    }
  }

  /**
   * Issues a signed Town Session Ticket for the account's character.
   * @throws 404 if no character exists.
   */
  static async issueTownTicket(accountId) {
    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    const now = Math.floor(Date.now() / 1000);
    const expiresAt = now + TOWN_TICKET_TTL_SECONDS;

    const payload = {
      characterId: character._id.toString(),
      characterName: character.name,
      sessionType: 'Town',
      sessionName: 'Lobby-Town',
      issuedAt: now,
      expiresAt
    };

    const ticket = this.signPayload(payload);

    return {
      ticket,
      characterId: payload.characterId,
      characterName: payload.characterName,
      sessionName: payload.sessionName,
      expiresAt: payload.expiresAt
    };
  }

  /**
   * Verifies a Town Session Ticket.
   */
  static verifyTownTicket(ticketString) {
    const decoded = this.verifyAndDecode(ticketString);
    if (!decoded.valid) {
      return decoded;
    }

    const payload = decoded.payload;
    const now = Math.floor(Date.now() / 1000);

    if (payload.sessionType !== 'Town' || payload.sessionName !== 'Lobby-Town') {
      return { valid: false, error: 'INVALID_SESSION' };
    }

    if (!payload.expiresAt || now > payload.expiresAt) {
      return { valid: false, error: 'TICKET_EXPIRED' };
    }

    return {
      valid: true,
      characterId: payload.characterId,
      characterName: payload.characterName,
      sessionName: payload.sessionName,
      expiresAt: payload.expiresAt
    };
  }

  /**
   * Issues an Authoritative Raid Admission Ticket backed by the active pendingReservation.
   * @throws 404 if character not found.
   * @throws 422 if reservationId does not match active pendingReservation.
   */
  static async issueRaidAdmissionTicket(accountId, raidCode, reservationId) {
    if (!raidCode || typeof raidCode !== 'string' || raidCode.trim().length !== 6) {
      throw { statusCode: 400, errorCode: 'INVALID_RAID_CODE', message: 'raidCode must be a 6-character string.' };
    }

    if (!reservationId || typeof reservationId !== 'string' || reservationId.trim() === '') {
      throw { statusCode: 400, errorCode: 'INVALID_RESERVATION_ID', message: 'reservationId is required.' };
    }

    const character = await Character.findOne({ accountId });
    if (!character) {
      throw { statusCode: 404, errorCode: 'CHARACTER_NOT_FOUND', message: 'No character found for this account.' };
    }

    const reservation = character.inventory && character.inventory.pendingReservation;
    if (!reservation || reservation.reservationId !== reservationId) {
      throw {
        statusCode: 422,
        errorCode: 'NO_ACTIVE_RESERVATION',
        message: 'No active reservation matches the specified reservationId.'
      };
    }

    const now = Math.floor(Date.now() / 1000);
    const expiresAt = now + RAID_TICKET_TTL_SECONDS;

    const characterAttributes = character.characterAttributes || {
      vitality: 5, resistance: 5, strength: 5, dexterity: 5, intelligence: 5, luck: 5, availablePoints: 10
    };

    const preparedAbilities = (reservation.preparedAbilities && (reservation.preparedAbilities.slot1 || reservation.preparedAbilities.slot2))
      ? { slot1: reservation.preparedAbilities.slot1 || '', slot2: reservation.preparedAbilities.slot2 || '' }
      : (character.preparedAbilities ? { slot1: character.preparedAbilities.slot1 || '', slot2: character.preparedAbilities.slot2 || '' } : { slot1: '', slot2: '' });

    const snapshot = {
      level: character.level || 1,
      experience: character.experience || 0,
      characterAttributes: {
        vitality: characterAttributes.vitality ?? 5,
        resistance: characterAttributes.resistance ?? 5,
        strength: characterAttributes.strength ?? 5,
        dexterity: characterAttributes.dexterity ?? 5,
        intelligence: characterAttributes.intelligence ?? 5,
        luck: characterAttributes.luck ?? 5,
        availablePoints: characterAttributes.availablePoints ?? 0
      },
      preparedAbilities,
      items: (reservation.items || []).map(i => ({ lootId: i.lootId, amount: i.amount })),
      preparedEquipment: {
        weaponSlot1: reservation.preparedEquipment?.weaponSlot1 || '',
        weaponSlot2: reservation.preparedEquipment?.weaponSlot2 || '',
        offHand1:    reservation.preparedEquipment?.offHand1 || '',
        helmet:      reservation.preparedEquipment?.helmet || '',
        armor:       reservation.preparedEquipment?.armor || '',
        gloves:      reservation.preparedEquipment?.gloves || '',
        boots:       reservation.preparedEquipment?.boots || '',
        offHand2:    reservation.preparedEquipment?.offHand2 || ''
      },
      lastAppliedProgressionResultSequence: character.lastAppliedProgressionResultSequence || 0
    };

    const payload = {
      characterId: character._id.toString(),
      characterName: character.name,
      raidCode,
      reservationId,
      snapshot,
      issuedAt: now,
      expiresAt
    };

    const ticket = this.signPayload(payload);

    return {
      ticket,
      characterId: payload.characterId,
      raidCode: payload.raidCode,
      reservationId: payload.reservationId,
      snapshot: payload.snapshot,
      expiresAt: payload.expiresAt
    };
  }

  /**
   * Verifies an Authoritative Raid Admission Ticket against the expected raidCode.
   */
  static verifyRaidAdmissionTicket(ticketString, expectedRaidCode) {
    const decoded = this.verifyAndDecode(ticketString);
    if (!decoded.valid) {
      return decoded;
    }

    const payload = decoded.payload;
    const now = Math.floor(Date.now() / 1000);

    if (expectedRaidCode && payload.raidCode !== expectedRaidCode) {
      return { valid: false, error: 'RAID_CODE_MISMATCH' };
    }

    if (!payload.expiresAt || now > payload.expiresAt) {
      return { valid: false, error: 'TICKET_EXPIRED' };
    }

    return {
      valid: true,
      characterId: payload.characterId,
      raidCode: payload.raidCode,
      reservationId: payload.reservationId,
      snapshot: payload.snapshot,
      expiresAt: payload.expiresAt
    };
  }

  /**
   * Helper for tests to generate custom/expired tickets.
   */
  static buildSignedTicketForTesting(payload) {
    return this.signPayload(payload);
  }
}

module.exports = SessionTicketService;

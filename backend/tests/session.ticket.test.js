// tests/session.ticket.test.js
const { test, describe, before, after } = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { MongoMemoryServer } = require('mongodb-memory-server');
const Character = require('../src/models/Character');
const Account = require('../src/models/Account');
const SessionTicketService = require('../src/services/SessionTicketService');

describe('SessionTicketService (US-59 / SEC-59.1)', () => {
  let mongoServer;
  let accountId;
  let characterId;

  before(async () => {
    mongoServer = await MongoMemoryServer.create();
    await mongoose.connect(mongoServer.getUri());

    const account = await Account.create({
      username: 'ticket_tester',
      passwordHash: 'dummy_hash'
    });
    accountId = account._id;

    const character = await Character.create({
      accountId: accountId,
      name: 'TicketHero',
      level: 12,
      experience: 2500,
      lastAppliedProgressionResultSequence: 3,
      characterAttributes: {
        vitality: 15,
        resistance: 10,
        strength: 20,
        dexterity: 12,
        intelligence: 8,
        luck: 7,
        availablePoints: 2
      },
      preparedAbilities: {
        slot1: 'ability_whirlwind',
        slot2: 'ability_dash'
      },
      inventory: {
        currency: 500,
        stash: [],
        loadout: [],
        preparedEquipment: {
          weaponSlot1: 'sword_iron',
          helmet: 'helm_iron'
        },
        pendingReservation: {
          reservationId: 'res_test_12345',
          items: [
            { lootId: 'potion_healing', amount: 3 },
            { lootId: 'sword_iron', amount: 1 },
            { lootId: 'helm_iron', amount: 1 }
          ],
          preparedEquipment: {
            weaponSlot1: 'sword_iron',
            helmet: 'helm_iron'
          }
        }
      }
    });
    characterId = character._id.toString();
  });

  after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  // -------------------------------------------------------------------------
  // Town Session Ticket
  // -------------------------------------------------------------------------
  describe('Town Session Ticket', () => {
    let issuedTicket;

    test('issues a signed town session ticket with correct claims', async () => {
      const result = await SessionTicketService.issueTownTicket(accountId);

      assert.ok(result.ticket, 'Should return a ticket string');
      assert.equal(result.characterId, characterId);
      assert.equal(result.characterName, 'TicketHero');
      assert.ok(result.expiresAt > Math.floor(Date.now() / 1000));
      issuedTicket = result.ticket;
    });

    test('verifies a valid town session ticket', async () => {
      const verification = SessionTicketService.verifyTownTicket(issuedTicket);

      assert.equal(verification.valid, true);
      assert.equal(verification.characterId, characterId);
      assert.equal(verification.characterName, 'TicketHero');
      assert.equal(verification.sessionName, 'Lobby-Town');
    });

    test('rejects a ticket with tampered payload', async () => {
      const parts = issuedTicket.split('.');
      const payload = JSON.parse(Buffer.from(parts[0], 'base64url').toString('utf8'));
      payload.characterName = 'ImpostorHero';
      payload.characterId = new mongoose.Types.ObjectId().toString();
      const tamperedParts = `${Buffer.from(JSON.stringify(payload)).toString('base64url')}.${parts[1]}`;

      const verification = SessionTicketService.verifyTownTicket(tamperedParts);
      assert.equal(verification.valid, false);
      assert.equal(verification.error, 'INVALID_SIGNATURE');
    });

    test('rejects a ticket with invalid signature', async () => {
      const parts = issuedTicket.split('.');
      const badTicket = `${parts[0]}.0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef`;

      const verification = SessionTicketService.verifyTownTicket(badTicket);
      assert.equal(verification.valid, false);
      assert.equal(verification.error, 'INVALID_SIGNATURE');
    });

    test('rejects an expired ticket', async () => {
      const expiredTicket = SessionTicketService.buildSignedTicketForTesting({
        characterId,
        characterName: 'TicketHero',
        sessionType: 'Town',
        sessionName: 'Lobby-Town',
        issuedAt: Math.floor(Date.now() / 1000) - 3600,
        expiresAt: Math.floor(Date.now() / 1000) - 1800
      });

      const verification = SessionTicketService.verifyTownTicket(expiredTicket);
      assert.equal(verification.valid, false);
      assert.equal(verification.error, 'TICKET_EXPIRED');
    });

    test('rejects ticket with wrong sessionType or sessionName', async () => {
      const wrongSessionTicket = SessionTicketService.buildSignedTicketForTesting({
        characterId,
        characterName: 'TicketHero',
        sessionType: 'Raid',
        sessionName: 'Not-Lobby-Town',
        issuedAt: Math.floor(Date.now() / 1000),
        expiresAt: Math.floor(Date.now() / 1000) + 1800
      });

      const verification = SessionTicketService.verifyTownTicket(wrongSessionTicket);
      assert.equal(verification.valid, false);
      assert.equal(verification.error, 'INVALID_SESSION');
    });
  });

  // -------------------------------------------------------------------------
  // Authoritative Raid Admission Ticket
  // -------------------------------------------------------------------------
  describe('Authoritative Raid Admission Ticket', () => {
    let raidTicket;
    const testRaidCode = '123456';
    const testReservationId = 'res_test_12345';

    test('issues an authoritative raid admission ticket matching active reservation', async () => {
      const result = await SessionTicketService.issueRaidAdmissionTicket(
        accountId,
        testRaidCode,
        testReservationId
      );

      assert.ok(result.ticket, 'Should return admission ticket string');
      assert.equal(result.characterId, characterId);
      assert.equal(result.raidCode, testRaidCode);
      assert.equal(result.reservationId, testReservationId);
      assert.equal(result.snapshot.level, 12);
      assert.equal(result.snapshot.experience, 2500);
      assert.equal(result.snapshot.characterAttributes.strength, 20);
      assert.equal(result.snapshot.characterAttributes.vitality, 15);
      assert.equal(result.snapshot.items.length, 3);
      assert.equal(result.snapshot.preparedEquipment.weaponSlot1, 'sword_iron');
      assert.equal(result.snapshot.preparedAbilities.slot1, 'ability_whirlwind');

      raidTicket = result.ticket;
    });

    test('verifies an authentic raid admission ticket against expected raidCode', async () => {
      const verification = SessionTicketService.verifyRaidAdmissionTicket(raidTicket, testRaidCode);

      assert.equal(verification.valid, true);
      assert.equal(verification.characterId, characterId);
      assert.equal(verification.raidCode, testRaidCode);
      assert.equal(verification.reservationId, testReservationId);
      assert.equal(verification.snapshot.level, 12);
      assert.equal(verification.snapshot.characterAttributes.strength, 20);
      assert.equal(verification.snapshot.items.length, 3);
    });

    test('rejects ticket when expected raidCode does not match', async () => {
      const verification = SessionTicketService.verifyRaidAdmissionTicket(raidTicket, '999999');

      assert.equal(verification.valid, false);
      assert.equal(verification.error, 'RAID_CODE_MISMATCH');
    });

    test('throws 422 if reservationId does not match active reservation', async () => {
      await assert.rejects(
        async () => {
          await SessionTicketService.issueRaidAdmissionTicket(accountId, testRaidCode, 'wrong_res_id');
        },
        (err) => err.statusCode === 422 && err.errorCode === 'NO_ACTIVE_RESERVATION'
      );
    });

    test('rejects admission ticket if signature is tampered', async () => {
      const parts = raidTicket.split('.');
      const payload = JSON.parse(Buffer.from(parts[0], 'base64url').toString('utf8'));
      // Atacante intenta inflar fuerza a 99
      payload.snapshot.characterAttributes.strength = 99;
      const tamperedTicket = `${Buffer.from(JSON.stringify(payload)).toString('base64url')}.${parts[1]}`;

      const verification = SessionTicketService.verifyRaidAdmissionTicket(tamperedTicket, testRaidCode);
      assert.equal(verification.valid, false);
      assert.equal(verification.error, 'INVALID_SIGNATURE');
    });

    test('rejects an expired raid admission ticket', async () => {
      const expiredRaidTicket = SessionTicketService.buildSignedTicketForTesting({
        characterId,
        raidCode: testRaidCode,
        reservationId: testReservationId,
        snapshot: { level: 12 },
        issuedAt: Math.floor(Date.now() / 1000) - 1000,
        expiresAt: Math.floor(Date.now() / 1000) - 400
      });

      const verification = SessionTicketService.verifyRaidAdmissionTicket(expiredRaidTicket, testRaidCode);
      assert.equal(verification.valid, false);
      assert.equal(verification.error, 'TICKET_EXPIRED');
    });
  });
});

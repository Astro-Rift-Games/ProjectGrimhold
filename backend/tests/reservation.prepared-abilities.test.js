'use strict';
// FIX-32: raid reservations and admission tickets must only carry catalog abilities the character owns.
const { test, describe, before, after } = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const request = require('supertest');
const jwt = require('jsonwebtoken');
const { startMemoryMongo } = require('./support/memoryMongo');
const app = require('../src/app');
const env = require('../src/config/env');
const Account = require('../src/models/Account');
const Character = require('../src/models/Character');
const InventoryService = require('../src/services/InventoryService');
const SessionTicketService = require('../src/services/SessionTicketService');
const { validatePreparedAbilities } = require('../src/services/PreparedAbilityValidation');

describe('Prepared ability validation (FIX-32)', () => {
  let mongoServer;
  let sequence = 0;

  before(async () => {
    mongoServer = await startMemoryMongo();
    await mongoose.connect(mongoServer.getUri());
  });

  after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  async function createCharacter({ unlockedAbilities = [], pendingReservation = null } = {}) {
    sequence++;
    const account = await Account.create({ username: `ability_tester_${sequence}`, passwordHash: 'dummy_hash' });
    await Character.create({
      accountId: account._id,
      name: `AbilityHero${sequence}`,
      unlockedAbilities,
      inventory: {
        stash: [],
        loadout: [],
        preparedEquipment: { weaponSlot1: 'sword_iron' },
        pendingReservation
      }
    });
    return account._id.toString();
  }

  const snapshot = async accountId => {
    const stored = await Character.findOne({ accountId }).lean();
    return { revision: stored.revision, inventory: stored.inventory, preparedAbilities: stored.preparedAbilities };
  };

  async function assertRejectedUnchanged(accountId, abilities, expected) {
    const before = await snapshot(accountId);
    await assert.rejects(
      () => InventoryService.savePendingReservation(accountId, 'res-new', abilities),
      err => {
        assert.equal(err.statusCode, expected.statusCode);
        assert.equal(err.errorCode, expected.errorCode);
        return true;
      }
    );
    assert.deepEqual(await snapshot(accountId), before);
    assert.equal((await Character.findOne({ accountId }).lean()).inventory.pendingReservation, null);
  }

  describe('InventoryService.savePendingReservation', () => {
    test('rejects an ability that is not in the catalog with 422 UNKNOWN_ABILITY', async () => {
      const accountId = await createCharacter({ unlockedAbilities: ['charge'] });
      await assertRejectedUnchanged(accountId, { slot1: 'charge', slot2: 'ability_whirlwind' },
        { statusCode: 422, errorCode: 'UNKNOWN_ABILITY' });
    });

    test('does not treat Object.prototype names as catalog abilities', async () => {
      const accountId = await createCharacter();
      await assertRejectedUnchanged(accountId, { slot1: 'constructor', slot2: '' },
        { statusCode: 422, errorCode: 'UNKNOWN_ABILITY' });
    });

    test('rejects a catalog ability the character has not unlocked with 422 ABILITY_NOT_UNLOCKED', async () => {
      const accountId = await createCharacter({ unlockedAbilities: ['charge'] });
      await assertRejectedUnchanged(accountId, { slot1: 'trap', slot2: '' },
        { statusCode: 422, errorCode: 'ABILITY_NOT_UNLOCKED' });
    });

    test('rejects the same ability in both slots with 400 INVALID_PREPARED_ABILITIES', async () => {
      const accountId = await createCharacter({ unlockedAbilities: ['charge'] });
      await assertRejectedUnchanged(accountId, { slot1: 'charge', slot2: ' Charge ' },
        { statusCode: 400, errorCode: 'INVALID_PREPARED_ABILITIES' });
    });

    for (const [label, abilities] of [
      ['non-string slot', { slot1: 5, slot2: '' }],
      ['over-long id', { slot1: 'a'.repeat(65), slot2: '' }],
      ['array body', ['charge']],
      ['string body', 'charge']
    ]) {
      test(`rejects malformed prepared abilities (${label}) with 400 INVALID_PREPARED_ABILITIES`, async () => {
        const accountId = await createCharacter({ unlockedAbilities: ['charge'] });
        await assertRejectedUnchanged(accountId, abilities,
          { statusCode: 400, errorCode: 'INVALID_PREPARED_ABILITIES' });
      });
    }

    test('accepts unlocked abilities and stores them normalized', async () => {
      const accountId = await createCharacter({ unlockedAbilities: ['charge', 'trap'] });
      await InventoryService.savePendingReservation(accountId, 'res-ok', { slot1: 'Charge', slot2: 'trap' });
      const stored = await Character.findOne({ accountId }).lean();
      assert.deepEqual(stored.inventory.pendingReservation.preparedAbilities, { slot1: 'charge', slot2: 'trap' });
    });

    test('accepts empty slots (regression)', async () => {
      const accountId = await createCharacter();
      await InventoryService.savePendingReservation(accountId, 'res-empty', { slot1: '', slot2: '' });
      await InventoryService.savePendingReservation(accountId, 'res-omitted');
      const stored = await Character.findOne({ accountId }).lean();
      assert.equal(stored.inventory.pendingReservation.reservationId, 'res-omitted');
      assert.deepEqual(stored.inventory.pendingReservation.preparedAbilities, { slot1: '', slot2: '' });
    });

    test('a same-id retry keeps returning the stored reservation without re-validating', async () => {
      const accountId = await createCharacter({ unlockedAbilities: ['charge'] });
      await InventoryService.savePendingReservation(accountId, 'res-same', { slot1: 'charge', slot2: '' });
      const retry = await InventoryService.savePendingReservation(accountId, 'res-same', { slot1: 'trap', slot2: '' });
      assert.equal(retry.pendingReservation.reservationId, 'res-same');
      const stored = await Character.findOne({ accountId }).lean();
      assert.deepEqual(stored.inventory.pendingReservation.preparedAbilities, { slot1: 'charge', slot2: '' });
    });

    test('POST /character/me/inventory/reservation answers 422 and leaves the character untouched', async () => {
      const accountId = await createCharacter({ unlockedAbilities: ['charge'] });
      const before = await snapshot(accountId);
      const token = jwt.sign({ sub: accountId }, env.jwtSecret);
      const response = await request(app).post('/character/me/inventory/reservation')
        .set('Authorization', `Bearer ${token}`)
        .send({ reservationId: 'res-http', preparedAbilities: { slot1: 'trap', slot2: '' } });
      assert.equal(response.status, 422);
      assert.equal(response.body.error, 'ABILITY_NOT_UNLOCKED');
      assert.deepEqual(await snapshot(accountId), before);
    });
  });

  describe('SessionTicketService.issueRaidAdmissionTicket', () => {
    const reservation = abilities => ({
      reservationId: 'res-stored',
      items: [],
      preparedEquipment: { weaponSlot1: 'sword_iron' },
      preparedAbilities: abilities
    });

    test('carries unlocked prepared abilities into the ticket snapshot', async () => {
      const accountId = await createCharacter({
        unlockedAbilities: ['charge', 'trap'],
        pendingReservation: reservation({ slot1: 'charge', slot2: 'trap' })
      });
      const result = await SessionTicketService.issueRaidAdmissionTicket(accountId, '123456', 'res-stored');
      assert.deepEqual(result.snapshot.preparedAbilities, { slot1: 'charge', slot2: 'trap' });
    });

    test('issues a ticket for a reservation without prepared abilities', async () => {
      const accountId = await createCharacter({ pendingReservation: reservation({ slot1: '', slot2: '' }) });
      const result = await SessionTicketService.issueRaidAdmissionTicket(accountId, '123456', 'res-stored');
      assert.deepEqual(result.snapshot.preparedAbilities, { slot1: '', slot2: '' });
    });

    for (const [label, abilities, unlocked, expected] of [
      ['an unowned catalog ability', { slot1: 'trap', slot2: '' }, ['charge'], 'ABILITY_NOT_UNLOCKED'],
      ['an unknown ability', { slot1: 'ability_whirlwind', slot2: '' }, ['charge'], 'UNKNOWN_ABILITY']
    ]) {
      test(`rejects a stored reservation holding ${label} and leaves the character untouched`, async () => {
        const accountId = await createCharacter({ unlockedAbilities: unlocked, pendingReservation: reservation(abilities) });
        const before = await snapshot(accountId);
        await assert.rejects(
          () => SessionTicketService.issueRaidAdmissionTicket(accountId, '123456', 'res-stored'),
          err => err.statusCode === 422 && err.errorCode === expected
        );
        assert.deepEqual(await snapshot(accountId), before);
      });
    }
  });
});

describe('validatePreparedAbilities (FIX-32)', () => {
  for (const name of ['__proto__', 'constructor']) {
    test(`rejects the Object.prototype name '${name}' with 422 UNKNOWN_ABILITY`, () => {
      assert.throws(() => validatePreparedAbilities({ slot1: name }, ['charge']),
        error => error.statusCode === 422 && error.errorCode === 'UNKNOWN_ABILITY');
    });
  }

  test('normalizes a trimmed, mixed-case unlocked ability and fills the omitted slot', () => {
    assert.deepEqual(validatePreparedAbilities({ slot1: 'Charge ' }, ['charge']), { slot1: 'charge', slot2: '' });
  });
});

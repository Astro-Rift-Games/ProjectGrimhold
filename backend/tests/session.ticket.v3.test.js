'use strict';
const { test, describe, before, after } = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');
const { startMemoryMongo } = require('./support/memoryMongo');
const Character = require('../src/models/Character');
const Account = require('../src/models/Account');
const SessionTicketService = require('../src/services/SessionTicketService');
const { createInstance } = require('../src/services/ItemInstances');

// A freshly created character persists the v3 inventory shape; its reservation's
// preparedEquipment is a Mongoose subdocument, not a plain object.
describe('Raid admission ticket for a v3 inventory', () => {
  let mongoServer;
  let accountId;
  const instanceIdRef = { value: null };

  before(async () => {
    mongoServer = await startMemoryMongo();
    await mongoose.connect(mongoServer.getUri());
    const instance = createInstance('arming_sword', { primary: { attribute: 'strength', grade: 'E' }, gemSocketCount: 2, gems: [null, null] });
    accountId = (await Account.create({ username: 'v3_ticket', passwordHash: 'x' }))._id;
    await Character.create({
      accountId,
      name: 'FreshHero',
      inventory: {
        inventorySchemaVersion: 3,
        pendingReservation: {
          reservationId: 'res_v3',
          items: [{ slot: 0, lootId: 'RES_RUBY_GEM', amount: 2 }],
          preparedEquipment: {
            weaponSlot1: { lootId: 'arming_sword', instance }
          }
        }
      }
    });
  });

  after(async () => {
    await mongoose.disconnect();
    await mongoServer.stop();
  });

  test('signs every equipment slot by name, with the weapon and empty slots as null', async () => {
    const result = await SessionTicketService.issueRaidAdmissionTicket(accountId, '123456', 'res_v3');

    assert.deepEqual(result.snapshot.preparedEquipment, {
      weaponSlot1: { lootId: 'arming_sword', modifiers: { primary: { attribute: 'strength', grade: 'E' }, gemSocketCount: 2, gems: [null, null] } },
      weaponSlot2: null,
      offHand1: null,
      helmet: null,
      armor: null,
      gloves: null,
      boots: null,
      offHand2: null
    });
    instanceIdRef.value = (await Character.findOne({ accountId })).inventory.pendingReservation.preparedEquipment.weaponSlot1.instance.instanceId;
    const verified = SessionTicketService.verifyRaidAdmissionTicket(result.ticket, '123456');
    assert.equal(verified.valid, true);
    assert.deepEqual(verified.snapshot.preparedEquipment, result.snapshot.preparedEquipment);
    assert.equal(JSON.stringify(verified.snapshot).includes(instanceIdRef.value), false, 'no durable instance id is signed');
  });
});

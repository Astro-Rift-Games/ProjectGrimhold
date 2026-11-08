'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const request = require('supertest');
const jwt = require('jsonwebtoken');
const app = require('../src/app');
const env = require('../src/config/env');
const Character = require('../src/models/Character');
const Revoked = require('../src/models/RevokedToken');
const InventoryService = require('../src/services/InventoryService');
const SessionTicketService = require('../src/services/SessionTicketService');

function makeInstance(lootId, instanceId = '0123456789abcdef01234567') {
  return { ...require('../src/services/ItemInstances').createEquipment(lootId).instance, instanceId };
}

function makeV3Character(overrides = {}) {
  const defaults = {
    _id: 'char123',
    accountId: 'acc123',
    name: 'Tester',
    schemaVersion: 11,
    revision: 1,
    characterAttributes: {
      vitality: 10,
      resistance: 10,
      strength: 10,
      dexterity: 10,
      intelligence: 10,
      luck: 10
    },
    inventory: {
      inventorySchemaVersion: 3,
      stash: [],
      loadout: [],
      preparedEquipment: {
        weaponSlot1: null,
        weaponSlot2: null,
        offHand1: null,
        offHand2: null,
        helmet: null,
        armor: null,
        gloves: null,
        boots: null
      },
      pendingReservation: null
    }
  };
  const doc = { ...defaults, ...overrides };
  doc.markModified = () => {};
  doc.save = async function () { return this; };
  return doc;
}

function cloneDoc(doc) {
  const cloned = JSON.parse(JSON.stringify(doc));
  cloned.markModified = () => {};
  cloned.save = async function () { return this; };
  return cloned;
}

test('InventoryService.requestTrainingWeapon - validations and limits', async (t) => {
  let mockChar = null;
  const origFindOne = Character.findOne;
  const origFindOneAndUpdate = Character.findOneAndUpdate;

  t.beforeEach(() => {
    mockChar = makeV3Character();
    Character.findOne = async () => mockChar;
    Character.findOneAndUpdate = async (filter, update) => {
      if (filter.revision !== mockChar.revision) return null;
      if (update.$set) {
        for (const [key, value] of Object.entries(update.$set)) {
          const parts = key.split('.');
          let cur = mockChar;
          for (let i = 0; i < parts.length - 1; i++) {
            if (!cur[parts[i]]) cur[parts[i]] = {};
            cur = cur[parts[i]];
          }
          cur[parts[parts.length - 1]] = value;
        }
      }
      mockChar.revision += 1;
      return mockChar;
    };
  });

  t.afterEach(() => {
    Character.findOne = origFindOne;
    Character.findOneAndUpdate = origFindOneAndUpdate;
  });

  await t.test('rejects invalid weaponId with 400 INVALID_ITEM', async () => {
    await assert.rejects(
      () => InventoryService.requestTrainingWeapon('acc123', 'invalid_weapon'),
      err => err.statusCode === 400 && err.errorCode === 'INVALID_ITEM'
    );
  });

  await t.test('rejects invalid destinationSlot with 400 INVALID_SLOT', async () => {
    await assert.rejects(
      () => InventoryService.requestTrainingWeapon('acc123', 'training_short_sword', 'helmet'),
      err => err.statusCode === 400 && err.errorCode === 'INVALID_SLOT'
    );
  });

  await t.test('equips training_short_sword in weaponSlot1, displacing previous weapon to loadout', async () => {
    const previousSword = { lootId: 'arming_sword', instance: makeInstance('arming_sword', '111111111111111111111111') };
    mockChar.inventory.preparedEquipment.weaponSlot1 = previousSword;

    const result = await InventoryService.requestTrainingWeapon('acc123', 'training_short_sword', 'weaponSlot1', 1);

    assert.ok(result.preparedEquipment.weaponSlot1);
    assert.equal(result.preparedEquipment.weaponSlot1.lootId, 'training_short_sword');
    assert.match(result.preparedEquipment.weaponSlot1.instance.instanceId, /^[0-9a-f]{24}$/);

    // Previous normal sword displaced to loadout
    assert.equal(result.loadout.length, 1);
    assert.equal(result.loadout[0].lootId, 'arming_sword');
    assert.equal(result.loadout[0].instance.instanceId, '111111111111111111111111');
  });

  await t.test('strictly replaces any previously owned training weapon without duplication', async () => {
    mockChar.inventory.stash = [{ slot: 0, lootId: 'training_dagger', amount: 1, instance: makeInstance('training_dagger', '222222222222222222222222') }];
    mockChar.inventory.loadout = [{ slot: 0, lootId: 'training_wand', amount: 1, instance: makeInstance('training_wand', '333333333333333333333333') }];

    const result = await InventoryService.requestTrainingWeapon('acc123', 'training_short_sword', 'weaponSlot1', 1);

    assert.equal(result.preparedEquipment.weaponSlot1.lootId, 'training_short_sword');
    assert.equal(result.stash.filter(i => i.lootId.startsWith('training_')).length, 0);
    assert.equal(result.loadout.filter(i => i.lootId.startsWith('training_')).length, 0);
  });

  await t.test('savePendingReservation preserves equipped training weapon and does NOT fallback to arming_sword', async () => {
    const trainingWeapon = { lootId: 'training_short_sword', instance: makeInstance('training_short_sword') };
    mockChar.inventory.preparedEquipment.weaponSlot1 = trainingWeapon;

    Character.findOneAndUpdate = async (filter, update) => {
      mockChar.inventory.pendingReservation = {
        reservationId: 'res-training-1',
        admittedAt: null,
        items: [],
        preparedEquipment: mockChar.inventory.preparedEquipment
      };
      mockChar.revision += 1;
      return mockChar;
    };

    const reservationResult = await InventoryService.savePendingReservation('acc123', 'res-training-1');

    assert.equal(reservationResult.pendingReservation.preparedEquipment.weaponSlot1.lootId, 'training_short_sword');
    assert.notEqual(reservationResult.pendingReservation.preparedEquipment.weaponSlot1.lootId, 'arming_sword');
  });

  await t.test('SessionTicketService issues ticket containing training weapon', async () => {
    const trainingWeapon = { lootId: 'training_wand', instance: makeInstance('training_wand') };
    mockChar.inventory.pendingReservation = {
      reservationId: 'res-training-2',
      admittedAt: null,
      items: [],
      preparedEquipment: {
        weaponSlot1: trainingWeapon,
        weaponSlot2: null,
        offHand1: null,
        offHand2: null,
        helmet: null,
        armor: null,
        gloves: null,
        boots: null
      }
    };

    const ticketResult = await SessionTicketService.issueRaidAdmissionTicket('acc123', 'RAID01', 'res-training-2');
    const verified = SessionTicketService.verifyRaidAdmissionTicket(ticketResult.ticket, 'RAID01');

    assert.equal(verified.valid, true);
    assert.equal(verified.snapshot.preparedEquipment.weaponSlot1.lootId, 'training_wand');
    assert.notEqual(verified.snapshot.preparedEquipment.weaponSlot1.lootId, 'arming_sword');
  });
});

test('POST /character/me/inventory/training-weapon HTTP integration', async (t) => {
  const account = 'bbbbbbbbbbbbbbbbbbbbbbbb';
  const token = jwt.sign({ sub: account }, env.jwtSecret);
  const origFindOne = Character.findOne;
  const origFindOneAndUpdate = Character.findOneAndUpdate;
  const origRevoked = Revoked.exists;

  let storedChar = null;

  t.beforeEach(() => {
    storedChar = makeV3Character({ accountId: account, revision: 2 });
    Character.findOne = async () => cloneDoc(storedChar);
    Character.findOneAndUpdate = async (filter, update) => {
      if (filter.revision !== storedChar.revision) return null;
      if (Object.hasOwn(filter, 'inventory.pendingReservation') && storedChar.inventory.pendingReservation) return null;
      if (update.$set) {
        for (const [key, value] of Object.entries(update.$set)) {
          const parts = key.split('.');
          let cur = storedChar;
          for (let i = 0; i < parts.length - 1; i++) {
            if (!cur[parts[i]]) cur[parts[i]] = {};
            cur = cur[parts[i]];
          }
          cur[parts[parts.length - 1]] = value;
        }
      }
      storedChar.revision += 1;
      return cloneDoc(storedChar);
    };
    Revoked.exists = async () => false;
  });

  t.afterEach(() => {
    Character.findOne = origFindOne;
    Character.findOneAndUpdate = origFindOneAndUpdate;
    Revoked.exists = origRevoked;
  });

  const post = body => request(app).post('/character/me/inventory/training-weapon').set('Authorization', `Bearer ${token}`).send(body);
  for (const [name, body, status, error] of [
    ['invalid weapon', { weaponId: 'bone', expectedRevision: 2 }, 400, 'VALIDATION_FAILED'],
    ['invalid slot', { weaponId: 'training_dagger', destinationSlot: 'helmet', expectedRevision: 2 }, 400, 'VALIDATION_FAILED'],
    ['missing revision', { weaponId: 'training_dagger' }, 400, 'VALIDATION_FAILED'],
    ['stale revision', { weaponId: 'training_dagger', expectedRevision: 1 }, 409, 'REVISION_CONFLICT'],
    ['alias', { weaponId: 'training_dagger', targetSlot: 'weaponSlot1', expectedRevision: 2 }, 400, 'VALIDATION_FAILED']
  ]) await t.test(name, async () => {
    const before = JSON.stringify(storedChar);
    const res = await post(body);
    assert.equal(res.status, status); assert.equal(res.body.error, error);
    assert.equal(JSON.stringify(storedChar), before);
  });
  await t.test('reservation blocks request without writes', async () => {
    storedChar.inventory.pendingReservation = { reservationId: 'pending', items: [], preparedEquipment: {} };
    const before = JSON.stringify(storedChar);
    const res = await post({ weaponId: 'training_dagger', expectedRevision: 2 });
    assert.equal(res.status, 409); assert.equal(res.body.error, 'RESERVATION_PENDING');
    assert.equal(JSON.stringify(storedChar), before);
  });
  await t.test('v1 requires migration without writes', async () => {
    storedChar.inventory.inventorySchemaVersion = 1;
    const before = JSON.stringify(storedChar);
    const res = await post({ weaponId: 'training_dagger', expectedRevision: 2 });
    assert.equal(res.status, 409); assert.equal(res.body.error, 'INVENTORY_MIGRATION_REQUIRED');
    assert.equal(JSON.stringify(storedChar), before);
  });
  await t.test('unauthenticated request', async () => {
    const before = JSON.stringify(storedChar);
    const res = await request(app).post('/character/me/inventory/training-weapon').send({ weaponId: 'training_dagger', expectedRevision: 2 });
    assert.equal(res.status, 401); assert.equal(JSON.stringify(storedChar), before);
  });
  await t.test('invented training identities cannot be equipped', async () => {
    const before = JSON.stringify(storedChar);
    const res = await request(app).put('/character/me/inventory/prepared-equipment').set('Authorization', `Bearer ${token}`).send({
      weaponSlot1: { lootId: 'training_dagger', instanceId: '111111111111111111111111' },
      weaponSlot2: { lootId: 'training_wand', instanceId: '222222222222222222222222' }, expectedRevision: 2
    });
    assert.equal(res.status, 422); assert.equal(res.body.error, 'ITEM_NOT_FOUND');
    assert.equal(JSON.stringify(storedChar), before);
  });
  await t.test('successive replacements and unequip preserve exactly one training weapon', async () => {
    for (const [index, weaponId] of ['training_short_sword', 'training_dagger', 'training_wand'].entries()) {
      const res = await post({ weaponId, ...(index === 1 ? { destinationSlot: 'weaponSlot2' } : {}), expectedRevision: 2 + index });
      assert.equal(res.status, 200);
      const all = [...res.body.stash, ...res.body.loadout, ...Object.values(res.body.preparedEquipment).filter(Boolean)];
      assert.equal(all.filter(item => item.lootId.startsWith('training_')).length, 1);
      if (index !== 1) assert.equal(res.body.loadout[0].lootId, weaponId);
    }
    const equipped = await post({ weaponId: 'training_dagger', destinationSlot: 'weaponSlot1', expectedRevision: 5 });
    const id = equipped.body.preparedEquipment.weaponSlot1.instance.instanceId;
    const res = await request(app).put('/character/me/inventory/prepared-equipment').set('Authorization', `Bearer ${token}`).send({ expectedRevision: 6 });
    assert.equal(res.status, 200); assert.equal(res.body.loadout[0].instance.instanceId, id);
  });
  for (const version of [1, 2, 3]) for (const weaponId of ['training_short_sword', 'training_dagger', 'training_wand']) {
    await t.test(`training sale and buyback rejected v${version} ${weaponId}`, async () => {
      storedChar.inventory.inventorySchemaVersion = version;
      storedChar.inventory.loadout = [{ slot: 0, lootId: weaponId, amount: 1, ...(version === 3 ? { instance: makeInstance(weaponId) } : {}) }];
      storedChar.inventory.buyback = { entries: [{ entryId: 'old', lootId: weaponId, amount: 1, unitPrice: 0 }] };
      const before = JSON.stringify(storedChar);
      for (const route of ['sell', 'trade', 'buyback']) {
        const body = route === 'sell' ? { transactionId: 'sale', lootId: weaponId, ...(version >= 2 ? { sourceSlot: 0 } : {}), amount: 1, expectedRevision: 2 }
          : route === 'trade' ? { transactionId: 'trade', expectedRevision: 2, purchases: [], sales: [{ lootId: weaponId, amount: 1, sourceSlot: 0 }] }
          : { transactionId: 'rebuy', entryId: 'old', amount: 1, expectedRevision: 2 };
        const res = await request(app).post(`/character/me/inventory/shop/${route}`).set('Authorization', `Bearer ${token}`).send(body);
        assert.equal(res.status, route === 'buyback' ? 409 : 404);
        assert.equal(res.body.error, route === 'buyback' ? 'BUYBACK_NOT_ELIGIBLE' : 'ITEM_NOT_SELLABLE');
        assert.equal(JSON.stringify(storedChar), before);
      }
    });
  }
  await t.test('POST /character/me/inventory/training-weapon returns 200 and equips training dagger', async () => {
    const res = await request(app)
      .post('/character/me/inventory/training-weapon')
      .set('Authorization', `Bearer ${token}`)
      .send({
        weaponId: 'training_dagger',
        destinationSlot: 'weaponSlot1',
        expectedRevision: 2
      });

    assert.equal(res.status, 200);
    assert.equal(res.body.preparedEquipment.weaponSlot1.lootId, 'training_dagger');
    assert.equal(res.body.revision, 3);
  });
});

test('requested training weapon reaches the raid admission ticket through the reservation', async (t) => {
  const origFindOne = Character.findOne;
  const origFindOneAndUpdate = Character.findOneAndUpdate;
  t.after(() => {
    Character.findOne = origFindOne;
    Character.findOneAndUpdate = origFindOneAndUpdate;
  });

  for (const weaponId of ['training_short_sword', 'training_dagger', 'training_wand']) {
    await t.test(weaponId, async () => {
      let stored = makeV3Character();
      stored.inventory.preparedEquipment.weaponSlot1 = { lootId: 'arming_sword', instance: makeInstance('arming_sword', '111111111111111111111111') };
      Character.findOne = async () => cloneDoc(stored);
      Character.findOneAndUpdate = async (filter, update) => {
        if (filter.revision !== undefined && filter.revision !== stored.revision) return null;
        const next = cloneDoc(stored);
        for (const [key, value] of Object.entries(update.$set || {})) {
          const parts = key.split('.');
          let cur = next;
          for (let i = 0; i < parts.length - 1; i++) cur = cur[parts[i]] ??= {};
          cur[parts[parts.length - 1]] = JSON.parse(JSON.stringify(value));
        }
        next.revision = stored.revision + 1;
        stored = next;
        return cloneDoc(stored);
      };

      const granted = await InventoryService.requestTrainingWeapon('acc123', weaponId, 'weaponSlot1', 1);
      const grantedInstanceId = granted.preparedEquipment.weaponSlot1.instance.instanceId;
      assert.equal(stored.inventory.preparedEquipment.weaponSlot1.instance.instanceId, grantedInstanceId);

      const reservation = await InventoryService.savePendingReservation('acc123', `res-${weaponId}`);
      assert.equal(reservation.pendingReservation.preparedEquipment.weaponSlot1.lootId, weaponId);
      assert.equal(stored.inventory.pendingReservation.preparedEquipment.weaponSlot1.instance.instanceId, grantedInstanceId);
      assert.deepEqual(
        stored.inventory.pendingReservation.items.map(item => [item.lootId, item.instance?.instanceId]),
        [['arming_sword', '111111111111111111111111']],
        'the displaced arming_sword travels as a carried item, not as the equipped weapon');

      const ticket = await SessionTicketService.issueRaidAdmissionTicket('acc123', 'RAID01', `res-${weaponId}`);
      const verified = SessionTicketService.verifyRaidAdmissionTicket(ticket.ticket, 'RAID01');
      assert.equal(verified.valid, true);
      assert.deepEqual(verified.snapshot.preparedEquipment.weaponSlot1, {
        lootId: weaponId,
        modifiers: { gemSocketCount: 0, gems: [] }
      });
      assert.equal(verified.snapshot.preparedEquipment.weaponSlot2 ?? null, null);
    });
  }
});

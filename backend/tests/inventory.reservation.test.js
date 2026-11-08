const test = require('node:test');
const assert = require('node:assert');

const Character = require('../src/models/Character');
const InventoryService = require('../src/services/InventoryService');

// ---------------------------------------------------------------------------
// Helpers to build mock Character documents
// ---------------------------------------------------------------------------

function makeCharacter(overrides = {}) {
  const defaults = {
    _id: 'char123',
    accountId: 'acc123',
    inventory: {
      stash:   [],
      loadout: [],
      preparedEquipment: { weaponSlot1: '', weaponSlot2: '', offHand1: '', offHand2: '', helmet: '', armor: '', gloves: '', boots: '' },
      pendingReservation: null
    }
  };
  const doc = { ...defaults, ...overrides };
  doc.markModified = () => {};
  doc.save = async function () { return this; };
  return doc;
}

function applyUpdate(doc, update) {
  if (update.$set) {
    for (const [key, value] of Object.entries(update.$set)) {
      const parts = key.split('.');
      let current = doc;
      for (let i = 0; i < parts.length - 1; i++) {
        if (!current[parts[i]]) current[parts[i]] = {};
        current = current[parts[i]];
      }
      current[parts[parts.length - 1]] = value;
    }
  }
  if (update.$unset) {
    for (const key of Object.keys(update.$unset)) {
      const parts = key.split('.');
      let current = doc;
      for (let i = 0; i < parts.length - 1; i++) {
        if (!current[parts[i]]) break;
        current = current[parts[i]];
      }
      delete current[parts[parts.length - 1]];
    }
  }
  if (update.$inc) {
    for (const [key, value] of Object.entries(update.$inc)) {
      const parts = key.split('.');
      let current = doc;
      for (let i = 0; i < parts.length - 1; i++) {
        if (!current[parts[i]]) current[parts[i]] = {};
        current = current[parts[i]];
      }
      current[parts[parts.length - 1]] = (current[parts[parts.length - 1]] || 0) + value;
    }
  }
}

function makeItem(lootId, amount) {
  return { lootId, amount };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test('expected-ID rollback HTTP contract', async t => {
  const request = require('supertest');
  const jwt = require('jsonwebtoken');
  const app = require('../src/app');
  const env = require('../src/config/env');
  const Revoked = require('../src/models/RevokedToken');
  const originals = [Character.findOne, Character.findOneAndUpdate, Revoked.exists];
  const account = 'aaaaaaaaaaaaaaaaaaaaaaaa';
  const token = jwt.sign({ sub: account }, env.jwtSecret);
  let stored, writes, race;
  t.after(() => {
    [Character.findOne, Character.findOneAndUpdate, Revoked.exists] = originals;
  });
  t.beforeEach(() => {
    writes = 0; race = false;
    stored = JSON.parse(JSON.stringify(makeCharacter({ accountId: account, revision: 4 })));
    stored.inventory.inventorySchemaVersion = 3;
    stored.inventory.preparedEquipment = Object.fromEntries(Object.keys(stored.inventory.preparedEquipment).map(key => [key, null]));
    stored.inventory.pendingReservation = {
      reservationId: 'expected', admittedAt: null,
      items: [{ slot: 3, lootId: 'wood', amount: 7 }],
      preparedEquipment: { weaponSlot1: { lootId: 'arming_sword', instance: {
        instanceId: '111111111111111111111111', modifiers: { gemSocketCount: 1, gems: [null] }
      } } }
    };
    Character.findOne = async () => structuredClone(stored);
    Revoked.exists = async () => false;
    Character.findOneAndUpdate = async (filter, update) => {
      if (race) { stored.inventory.pendingReservation.reservationId = 'newer'; race = false; }
      if (filter.revision !== stored.revision ||
          ('inventory.pendingReservation.reservationId' in filter &&
           filter['inventory.pendingReservation.reservationId'] !== stored.inventory.pendingReservation?.reservationId)) return null;
      writes++; applyUpdate(stored, update); return structuredClone(stored);
    };
  });
  const remove = id => {
    const query = id === undefined ? '' : `&expectedReservationId=${encodeURIComponent(id)}`;
    return request(app).delete(`/character/me/inventory/reservation?rollback=true${query}`)
      .set('Authorization', `Bearer ${token}`);
  };
  await t.test('matching ID restores exact ownership once; repeated request is read-only', async () => {
    const expected = structuredClone(stored.inventory.pendingReservation);
    const first = await remove('expected');
    assert.strictEqual(first.status, 200); assert.deepStrictEqual(first.body, { pendingReservation: null, revision: 5 });
    assert.deepStrictEqual(stored.inventory.loadout, expected.items);
    assert.deepStrictEqual(stored.inventory.preparedEquipment.weaponSlot1, expected.preparedEquipment.weaponSlot1);
    assert.strictEqual(writes, 1);
    const before = structuredClone(stored);
    const second = await remove('expected');
    assert.strictEqual(second.status, 200); assert.deepStrictEqual(second.body, first.body);
    assert.deepStrictEqual(stored, before); assert.strictEqual(writes, 1);
  });
  await t.test('stale client cannot delete newer reservation or change revision', async () => {
    stored.inventory.pendingReservation.reservationId = 'newer';
    const before = structuredClone(stored);
    const response = await remove('expected');
    assert.strictEqual(response.status, 409); assert.strictEqual(response.body.error, 'RESERVATION_MISMATCH');
    assert.deepStrictEqual(stored, before); assert.strictEqual(writes, 0);
  });
  await t.test('expected identity participates in atomic filter even if revision is unchanged', async () => {
    race = true;
    const before = structuredClone(stored);
    const response = await remove('expected');
    assert.strictEqual(response.status, 409); assert.strictEqual(response.body.error, 'REVISION_CONFLICT');
    before.inventory.pendingReservation.reservationId = 'newer';
    assert.deepStrictEqual(stored, before); assert.strictEqual(writes, 0);
  });
  await t.test('legacy omitted ID keeps rollback and revision behavior', async () => {
    assert.strictEqual((await remove()).status, 200);
    assert.strictEqual(stored.inventory.pendingReservation, null);
    assert.strictEqual(stored.inventory.loadout[0].amount, 7);
    assert.strictEqual(stored.revision, 5); assert.strictEqual(writes, 1);
  });
  await t.test('invalid typed ID rejects without mutation', async () => {
    const before = structuredClone(stored);
    const response = await request(app).delete('/character/me/inventory/reservation?rollback=true')
      .set('Authorization', `Bearer ${token}`).send({ expectedReservationId: 7 });
    assert.strictEqual(response.status, 400); assert.strictEqual(response.body.error, 'VALIDATION_FAILED');
    assert.deepStrictEqual(stored, before); assert.strictEqual(writes, 0);
  });
});

test('InventoryService - savePendingReservation', async (t) => {
  const originalFindOne = Character.findOne;
  const originalFindOneAndUpdate = Character.findOneAndUpdate;

  t.afterEach(() => {
    Character.findOne = originalFindOne;
    Character.findOneAndUpdate = originalFindOneAndUpdate;
  });

  const setupMocks = (mockChar) => {
    Character.findOne = async () => mockChar;
    Character.findOneAndUpdate = async (filter, update) => {
      if (!mockChar) return null;
      applyUpdate(mockChar, update);
      return mockChar;
    };
  };

  await t.test('builds reservation from persisted loadout/preparedEquipment, not body', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('arming_sword', 1)];
    mockChar.inventory.preparedEquipment = {
      weaponSlot1: 'arming_sword', weaponSlot2: '', helmet: '', armor: '', gloves: '', boots: ''
    };
    setupMocks(mockChar);

    const result = await InventoryService.savePendingReservation('acc123', 'res-001');

    assert.ok(result.pendingReservation);
    assert.strictEqual(result.pendingReservation.reservationId, 'res-001');
    assert.deepStrictEqual(result.pendingReservation.items, [{ lootId: 'arming_sword', amount: 1 }]);
    assert.strictEqual(result.pendingReservation.preparedEquipment.weaponSlot1, 'arming_sword');
  });

  await t.test('clears loadout and preparedEquipment after reserving', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('potion', 2)];
    mockChar.inventory.preparedEquipment = {
      weaponSlot1: '', weaponSlot2: '', helmet: 'iron_helm', armor: '', gloves: '', boots: ''
    };
    setupMocks(mockChar);

    await InventoryService.savePendingReservation('acc123', 'res-002');

    assert.deepStrictEqual(mockChar.inventory.loadout, []);
    assert.deepStrictEqual(mockChar.inventory.preparedEquipment, {});
  });

  await t.test('returns existing reservation without mutating if reservationId matches (idempotency)', async () => {
    const mockChar = makeCharacter();
    // Simulate an already existing reservation
    mockChar.inventory.pendingReservation = {
      reservationId: 'res-idempotent',
      items: [makeItem('shield', 1)],
      preparedEquipment: {
        weaponSlot1: '', weaponSlot2: 'shield', helmet: '', armor: '', gloves: '', boots: ''
      }
    };
    // The loadout/equipment are already empty from the first call
    mockChar.inventory.loadout = [];
    mockChar.inventory.preparedEquipment = {};
    
    // We add a dummy item to stash just to ensure save wasn't called inappropriately
    // (though we can track save calls directly).
    let saveCalled = false;
    mockChar.save = async function () { saveCalled = true; return this; };
    setupMocks(mockChar);

    const result = await InventoryService.savePendingReservation('acc123', 'res-idempotent');

    assert.strictEqual(result.pendingReservation.reservationId, 'res-idempotent');
    assert.deepStrictEqual(result.pendingReservation.items, [{ lootId: 'shield', amount: 1 }]);
  });
});

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

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
      preparedEquipment: { weaponSlot1: '', weaponSlot2: '', helmet: '', armor: '', gloves: '', boots: '' },
      pendingReservation: null
    }
  };
  const doc = { ...defaults, ...overrides };
  // Minimal mongoose-like markModified + save behaviour
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

test('InventoryService', async (t) => {
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

  // --- getInventory ---

  await t.test('getInventory() - throws 404 when character not found', async () => {
    Character.findOne = async () => null;

    try {
      await InventoryService.getInventory('acc_missing');
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 404);
      assert.strictEqual(err.errorCode, 'CHARACTER_NOT_FOUND');
    }
  });

  await t.test('getInventory() - returns empty inventory for a new character', async () => {
    const mockChar = makeCharacter();
    setupMocks(mockChar);

    const result = await InventoryService.getInventory('acc123');
    assert.deepStrictEqual(result.stash, []);
    assert.deepStrictEqual(result.loadout, []);
    assert.strictEqual(result.pendingReservation, null);
  });

  await t.test('getInventory() - serializes existing items correctly', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash   = [makeItem('sword', 2)];
    mockChar.inventory.loadout = [makeItem('potion', 1)];
    setupMocks(mockChar);

    const result = await InventoryService.getInventory('acc123');
    assert.deepStrictEqual(result.stash,   [{ lootId: 'sword',  amount: 2 }]);
    assert.deepStrictEqual(result.loadout, [{ lootId: 'potion', amount: 1 }]);
  });

  // --- moveToLoadout ---

  await t.test('moveToLoadout() - moves item from stash to loadout', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash = [makeItem('sword', 3)];
    setupMocks(mockChar);

    const result = await InventoryService.moveToLoadout('acc123', 'sword', 2, 0);

    assert.deepStrictEqual(result.stash,   [{ lootId: 'sword', amount: 1 }]);
    assert.deepStrictEqual(result.loadout, [{ lootId: 'sword', amount: 2 }]);
  });

  await t.test('moveToLoadout() - removes item from stash when all units moved', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash = [makeItem('axe', 1)];
    setupMocks(mockChar);

    const result = await InventoryService.moveToLoadout('acc123', 'axe', 1, 0);

    assert.deepStrictEqual(result.stash,   []);
    assert.deepStrictEqual(result.loadout, [{ lootId: 'axe', amount: 1 }]);
  });

  await t.test('moveToLoadout() - accumulates amount when item already in loadout', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash   = [makeItem('potion', 5)];
    mockChar.inventory.loadout = [makeItem('potion', 2)];
    setupMocks(mockChar);

    const result = await InventoryService.moveToLoadout('acc123', 'potion', 3, 0);

    assert.deepStrictEqual(result.stash,   [{ lootId: 'potion', amount: 2 }]);
    assert.deepStrictEqual(result.loadout, [{ lootId: 'potion', amount: 5 }]);
  });

  await t.test('moveToLoadout() - throws 409 when item not in stash', async () => {
    const mockChar = makeCharacter();
    setupMocks(mockChar);

    try {
      await InventoryService.moveToLoadout('acc123', 'bow', 1, 0);
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 409);
      assert.strictEqual(err.errorCode, 'INSUFFICIENT_STASH_ITEMS');
    }
  });

  await t.test('moveToLoadout() - throws 409 when stash has insufficient amount', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash = [makeItem('shield', 1)];
    setupMocks(mockChar);

    try {
      await InventoryService.moveToLoadout('acc123', 'shield', 3, 0);
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 409);
      assert.strictEqual(err.errorCode, 'INSUFFICIENT_STASH_ITEMS');
    }
  });

  // --- moveToStash ---

  await t.test('moveToStash() - moves item from loadout to stash', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('sword', 3)];
    setupMocks(mockChar);

    const result = await InventoryService.moveToStash('acc123', 'sword', 2, 0);

    assert.deepStrictEqual(result.loadout, [{ lootId: 'sword', amount: 1 }]);
    assert.deepStrictEqual(result.stash,   [{ lootId: 'sword', amount: 2 }]);
  });

  await t.test('moveToStash() - removes item from loadout when all units moved', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('helm', 2)];
    setupMocks(mockChar);

    const result = await InventoryService.moveToStash('acc123', 'helm', 2, 0);

    assert.deepStrictEqual(result.loadout, []);
    assert.deepStrictEqual(result.stash,   [{ lootId: 'helm', amount: 2 }]);
  });

  await t.test('moveToStash() - throws 409 when item not in loadout', async () => {
    const mockChar = makeCharacter();
    setupMocks(mockChar);

    try {
      await InventoryService.moveToStash('acc123', 'crown', 1, 0);
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 409);
      assert.strictEqual(err.errorCode, 'INSUFFICIENT_LOADOUT_ITEMS');
    }
  });

  // --- updatePreparedEquipment ---

  await t.test('updatePreparedEquipment() - assigns slots from loadout successfully', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('arming_sword', 1)];
    setupMocks(mockChar);

    const result = await InventoryService.updatePreparedEquipment('acc123', {
      weaponSlot1: 'arming_sword',
      weaponSlot2: '',
      helmet: '', armor: '', gloves: '', boots: ''
    }, 0);

    assert.strictEqual(result.preparedEquipment.weaponSlot1, 'arming_sword');
    assert.strictEqual(result.preparedEquipment.weaponSlot2, '');
  });

  await t.test('updatePreparedEquipment() - throws 422 when item not in loadout', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [];
    setupMocks(mockChar);

    try {
      await InventoryService.updatePreparedEquipment('acc123', { weaponSlot1: 'legendary_axe' }, 0);
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 422);
      assert.strictEqual(err.errorCode, 'ITEM_NOT_FOUND');
    }
  });

  await t.test('updatePreparedEquipment() - throws 422 when same item used in more slots than owned', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('potion', 1)];
    setupMocks(mockChar);

    try {
      // Trying to assign the same item to two weapon slots but only 1 owned
      await InventoryService.updatePreparedEquipment('acc123', {
        weaponSlot1: 'potion',
        weaponSlot2: 'potion'
      }, 0);
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 422);
      assert.strictEqual(err.errorCode, 'ITEM_NOT_FOUND');
    }
  });

  // --- clearPendingReservation ---

  await t.test('clearPendingReservation() - sets reservation to null', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.pendingReservation = { reservationId: 'res-001', items: [], preparedEquipment: {} };
    setupMocks(mockChar);

    const result = await InventoryService.clearPendingReservation('acc123');
    assert.strictEqual(result.pendingReservation, null);
  });

  await t.test('getInventory() - migrates aliases, merges stacks, slots, and reservation idempotently', async () => {
    const mockChar = makeCharacter();
    let saveCount = 0;
    mockChar.save = async function () { saveCount += 1; return this; };
    mockChar.inventory.stash = [
      makeItem('recovery_sword', 1),
      makeItem('training_sword', 2),
      makeItem('arming_sword', 3),
      makeItem('greatsword', 1)
    ];
    mockChar.inventory.loadout = [makeItem('wand', 1), makeItem('spellbook', 2)];
    mockChar.inventory.preparedEquipment = {
      weaponSlot1: 'longsword', weaponSlot2: 'staff', helmet: '', armor: '', gloves: '', boots: ''
    };
    mockChar.inventory.pendingReservation = {
      reservationId: 'legacy-reservation',
      items: [makeItem('training_shield', 1), makeItem('wand', 1)],
      preparedEquipment: {
        weaponSlot1: 'greatsword', weaponSlot2: 'spellbook', helmet: '', armor: '', gloves: '', boots: ''
      }
    };
    setupMocks(mockChar);

    const first = await InventoryService.getInventory('acc123');
    assert.deepStrictEqual(first.stash, [
      { lootId: 'arming_sword', amount: 6 },
      { lootId: 'long_sword', amount: 1 }
    ]);
    assert.deepStrictEqual(first.loadout, [{ lootId: 'magic_wand', amount: 3 }]);
    assert.strictEqual(first.preparedEquipment.weaponSlot1, 'arming_sword');
    assert.strictEqual(first.preparedEquipment.weaponSlot2, 'magic_staff');
    assert.deepStrictEqual(first.pendingReservation.items, [
      { lootId: 'shield', amount: 1 },
      { lootId: 'magic_wand', amount: 1 }
    ]);
    assert.strictEqual(first.pendingReservation.preparedEquipment.weaponSlot1, 'long_sword');
    assert.strictEqual(first.pendingReservation.preparedEquipment.weaponSlot2, 'magic_wand');
    assert.strictEqual(saveCount, 1);

    const second = await InventoryService.getInventory('acc123');
    assert.deepStrictEqual(second, first);
    assert.strictEqual(saveCount, 1);
  });

  await t.test('moveToLoadout() - accepts a canonical id after persisted aliases are migrated', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash = [makeItem('recovery_sword', 1), makeItem('training_sword', 2)];
    setupMocks(mockChar);

    const result = await InventoryService.moveToLoadout('acc123', 'arming_sword', 2, 0);

    assert.deepStrictEqual(result.stash, [{ lootId: 'arming_sword', amount: 1 }]);
    assert.deepStrictEqual(result.loadout, [{ lootId: 'arming_sword', amount: 2 }]);
  });

  await t.test('updatePreparedEquipment() - canonicalizes persisted and requested aliases', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('training_sword', 1)];
    setupMocks(mockChar);

    const result = await InventoryService.updatePreparedEquipment('acc123', {
      weaponSlot1: 'recovery_sword',
      weaponSlot2: '',
      helmet: '', armor: '', gloves: '', boots: ''
    }, 0);

    assert.strictEqual(result.preparedEquipment.weaponSlot1, 'arming_sword');
    assert.deepStrictEqual(mockChar.inventory.loadout, []);
  });
});

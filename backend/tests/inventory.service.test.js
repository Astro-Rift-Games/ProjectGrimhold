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

function makeItem(lootId, amount) {
  return { lootId, amount };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test('InventoryService', async (t) => {
  const originalFindOne = Character.findOne;

  t.afterEach(() => {
    Character.findOne = originalFindOne;
  });

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
    Character.findOne = async () => mockChar;

    const result = await InventoryService.getInventory('acc123');
    assert.deepStrictEqual(result.stash, []);
    assert.deepStrictEqual(result.loadout, []);
    assert.strictEqual(result.pendingReservation, null);
  });

  await t.test('getInventory() - serializes existing items correctly', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash   = [makeItem('sword', 2)];
    mockChar.inventory.loadout = [makeItem('potion', 1)];
    Character.findOne = async () => mockChar;

    const result = await InventoryService.getInventory('acc123');
    assert.deepStrictEqual(result.stash,   [{ lootId: 'sword',  amount: 2 }]);
    assert.deepStrictEqual(result.loadout, [{ lootId: 'potion', amount: 1 }]);
  });

  // --- moveToLoadout ---

  await t.test('moveToLoadout() - moves item from stash to loadout', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash = [makeItem('sword', 3)];
    Character.findOne = async () => mockChar;

    const result = await InventoryService.moveToLoadout('acc123', 'sword', 2);

    assert.deepStrictEqual(result.stash,   [{ lootId: 'sword', amount: 1 }]);
    assert.deepStrictEqual(result.loadout, [{ lootId: 'sword', amount: 2 }]);
  });

  await t.test('moveToLoadout() - removes item from stash when all units moved', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash = [makeItem('axe', 1)];
    Character.findOne = async () => mockChar;

    const result = await InventoryService.moveToLoadout('acc123', 'axe', 1);

    assert.deepStrictEqual(result.stash,   []);
    assert.deepStrictEqual(result.loadout, [{ lootId: 'axe', amount: 1 }]);
  });

  await t.test('moveToLoadout() - accumulates amount when item already in loadout', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash   = [makeItem('potion', 5)];
    mockChar.inventory.loadout = [makeItem('potion', 2)];
    Character.findOne = async () => mockChar;

    const result = await InventoryService.moveToLoadout('acc123', 'potion', 3);

    assert.deepStrictEqual(result.stash,   [{ lootId: 'potion', amount: 2 }]);
    assert.deepStrictEqual(result.loadout, [{ lootId: 'potion', amount: 5 }]);
  });

  await t.test('moveToLoadout() - throws 409 when item not in stash', async () => {
    const mockChar = makeCharacter();
    Character.findOne = async () => mockChar;

    try {
      await InventoryService.moveToLoadout('acc123', 'bow', 1);
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 409);
      assert.strictEqual(err.errorCode, 'INSUFFICIENT_STASH_ITEMS');
    }
  });

  await t.test('moveToLoadout() - throws 409 when stash has insufficient amount', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.stash = [makeItem('shield', 1)];
    Character.findOne = async () => mockChar;

    try {
      await InventoryService.moveToLoadout('acc123', 'shield', 3);
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
    Character.findOne = async () => mockChar;

    const result = await InventoryService.moveToStash('acc123', 'sword', 2);

    assert.deepStrictEqual(result.loadout, [{ lootId: 'sword', amount: 1 }]);
    assert.deepStrictEqual(result.stash,   [{ lootId: 'sword', amount: 2 }]);
  });

  await t.test('moveToStash() - removes item from loadout when all units moved', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('helm', 2)];
    Character.findOne = async () => mockChar;

    const result = await InventoryService.moveToStash('acc123', 'helm', 2);

    assert.deepStrictEqual(result.loadout, []);
    assert.deepStrictEqual(result.stash,   [{ lootId: 'helm', amount: 2 }]);
  });

  await t.test('moveToStash() - throws 409 when item not in loadout', async () => {
    const mockChar = makeCharacter();
    Character.findOne = async () => mockChar;

    try {
      await InventoryService.moveToStash('acc123', 'crown', 1);
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 409);
      assert.strictEqual(err.errorCode, 'INSUFFICIENT_LOADOUT_ITEMS');
    }
  });

  // --- updatePreparedEquipment ---

  await t.test('updatePreparedEquipment() - assigns slots from loadout successfully', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('training_sword', 1)];
    Character.findOne = async () => mockChar;

    const result = await InventoryService.updatePreparedEquipment('acc123', {
      weaponSlot1: 'training_sword',
      weaponSlot2: '',
      helmet: '', armor: '', gloves: '', boots: ''
    });

    assert.strictEqual(result.preparedEquipment.weaponSlot1, 'training_sword');
    assert.strictEqual(result.preparedEquipment.weaponSlot2, '');
  });

  await t.test('updatePreparedEquipment() - throws 422 when item not in loadout', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [];
    Character.findOne = async () => mockChar;

    try {
      await InventoryService.updatePreparedEquipment('acc123', { weaponSlot1: 'legendary_axe' });
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 422);
      assert.strictEqual(err.errorCode, 'ITEM_NOT_IN_LOADOUT');
    }
  });

  await t.test('updatePreparedEquipment() - throws 422 when same item used in more slots than owned', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.loadout = [makeItem('potion', 1)];
    Character.findOne = async () => mockChar;

    try {
      // Trying to assign the same item to two weapon slots but only 1 owned
      await InventoryService.updatePreparedEquipment('acc123', {
        weaponSlot1: 'potion',
        weaponSlot2: 'potion'
      });
      assert.fail('Should have thrown');
    } catch (err) {
      assert.strictEqual(err.statusCode, 422);
      assert.strictEqual(err.errorCode, 'INSUFFICIENT_LOADOUT_ITEMS');
    }
  });

  // --- savePendingReservation / clearPendingReservation ---

  await t.test('savePendingReservation() - persists reservation data', async () => {
    const mockChar = makeCharacter();
    Character.findOne = async () => mockChar;

    const result = await InventoryService.savePendingReservation(
      'acc123',
      'res-001',
      [{ lootId: 'sword', amount: 1 }],
      { weaponSlot1: 'sword', weaponSlot2: '', helmet: '', armor: '', gloves: '', boots: '' }
    );

    assert.ok(result.pendingReservation);
    assert.strictEqual(result.pendingReservation.reservationId, 'res-001');
    assert.deepStrictEqual(result.pendingReservation.items, [{ lootId: 'sword', amount: 1 }]);
  });

  await t.test('clearPendingReservation() - sets reservation to null', async () => {
    const mockChar = makeCharacter();
    mockChar.inventory.pendingReservation = { reservationId: 'res-001', items: [], preparedEquipment: {} };
    Character.findOne = async () => mockChar;

    const result = await InventoryService.clearPendingReservation('acc123');
    assert.strictEqual(result.pendingReservation, null);
  });
});

'use strict';

const test = require('node:test');
const assert = require('node:assert');

const Character = require('../src/models/Character');
const AbilityService = require('../src/services/AbilityService');

function makeCharacter(overrides = {}) {
  const defaults = {
    _id: 'char123',
    accountId: 'acc123',
    inventory: {
      currency: 500,
      stash: [],
      loadout: [],
      pendingReservation: null
    },
    unlockedAbilities: [],
    appliedShopReceipts: [],
    revision: 2
  };
  const doc = { ...defaults, ...overrides };
  doc.markModified = () => {};
  doc.save = async function () { return this; };
  return doc;
}

function applyUpdate(doc, update) {
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
  if (update.$push) {
    if (update.$push.unlockedAbilities) {
      doc.unlockedAbilities = (doc.unlockedAbilities || []).concat(update.$push.unlockedAbilities);
    }
    if (update.$push.appliedShopReceipts) {
      const each = update.$push.appliedShopReceipts.$each || [];
      doc.appliedShopReceipts = (doc.appliedShopReceipts || []).concat(each);
      if (update.$push.appliedShopReceipts.$slice) {
        doc.appliedShopReceipts = doc.appliedShopReceipts.slice(update.$push.appliedShopReceipts.$slice);
      }
    }
  }
}

test('AbilityService - purchaseAbility authoritative & durable verification (TASK-224)', async (t) => {
  const originalFindOne = Character.findOne;
  const originalFindOneAndUpdate = Character.findOneAndUpdate;

  t.afterEach(() => {
    Character.findOne = originalFindOne;
    Character.findOneAndUpdate = originalFindOneAndUpdate;
  });

  const setupMocks = (mockChar) => {
    Character.findOne = async (filter) => {
      if (!mockChar) return null;
      return mockChar;
    };
    Character.findOneAndUpdate = async (filter, update) => {
      if (!mockChar) return null;
      if (filter.revision !== undefined && mockChar.revision !== filter.revision) {
        return null;
      }
      if (filter['inventory.pendingReservation'] === null && mockChar.inventory.pendingReservation !== null) {
        return null;
      }
      if (filter['inventory.currency'] && filter['inventory.currency'].$gte !== undefined) {
        if ((mockChar.inventory.currency || 0) < filter['inventory.currency'].$gte) {
          return null;
        }
      }
      if (filter.unlockedAbilities && filter.unlockedAbilities.$ne !== undefined) {
        if ((mockChar.unlockedAbilities || []).includes(filter.unlockedAbilities.$ne)) {
          return null;
        }
      }
      applyUpdate(mockChar, update);
      return mockChar;
    };
  };

  await t.test('successful purchase debits currency, unlocks ability, records receipt, and increments revision', async () => {
    const char = makeCharacter({
      inventory: { currency: 300, pendingReservation: null },
      unlockedAbilities: ['trap'],
      revision: 1
    });
    setupMocks(char);

    const res = await AbilityService.purchaseAbility('acc123', {
      transactionId: 'tx-1001',
      abilityId: 'arcane_projectile',
      expectedRevision: 1
    });

    assert.strictEqual(res.alreadySecured, false);
    assert.strictEqual(res.transactionId, 'tx-1001');
    assert.strictEqual(res.abilityId, 'arcane_projectile');
    assert.strictEqual(res.price, 100);
    assert.strictEqual(res.currency, 200);
    assert.strictEqual(res.revision, 2);
    assert.deepStrictEqual(res.unlockedAbilities, ['trap', 'arcane_projectile']);

    assert.strictEqual(char.inventory.currency, 200);
    assert.strictEqual(char.revision, 2);
    assert.deepStrictEqual(char.unlockedAbilities, ['trap', 'arcane_projectile']);
    assert.strictEqual(char.appliedShopReceipts.length, 1);
    assert.strictEqual(char.appliedShopReceipts[0].transactionId, 'tx-1001');
    assert.strictEqual(char.appliedShopReceipts[0].type, 'ability_purchase');
    assert.strictEqual(char.appliedShopReceipts[0].abilityId, 'arcane_projectile');
    assert.strictEqual(char.appliedShopReceipts[0].totalAmount, 100);
  });

  await t.test('idempotent replay of same transactionId returns alreadySecured: true without mutating', async () => {
    const char = makeCharacter({
      inventory: { currency: 200, pendingReservation: null },
      unlockedAbilities: ['arcane_projectile'],
      appliedShopReceipts: [{
        transactionId: 'tx-1001',
        type: 'ability_purchase',
        abilityId: 'arcane_projectile',
        totalAmount: 100
      }],
      revision: 2
    });
    setupMocks(char);

    const before = JSON.stringify(char);
    const res = await AbilityService.purchaseAbility('acc123', {
      transactionId: 'tx-1001',
      abilityId: 'arcane_projectile',
      expectedRevision: 2
    });

    assert.strictEqual(res.alreadySecured, true);
    assert.strictEqual(res.transactionId, 'tx-1001');
    assert.strictEqual(res.abilityId, 'arcane_projectile');
    assert.strictEqual(res.currency, 200);
    assert.strictEqual(res.revision, 2);
    assert.strictEqual(JSON.stringify(char), before);
  });

  await t.test('transactionId reused for different ability rejects with 409 TRANSACTION_ID_REUSED', async () => {
    const char = makeCharacter({
      appliedShopReceipts: [{
        transactionId: 'tx-1001',
        type: 'ability_purchase',
        abilityId: 'charge',
        totalAmount: 100
      }]
    });
    setupMocks(char);

    await assert.rejects(
      AbilityService.purchaseAbility('acc123', {
        transactionId: 'tx-1001',
        abilityId: 'empower',
        expectedRevision: 2
      }),
      err => err.statusCode === 409 && err.errorCode === 'TRANSACTION_ID_REUSED'
    );
  });

  await t.test('pending reservation rejects with 409 RESERVATION_PENDING without mutating', async () => {
    const char = makeCharacter({
      inventory: { currency: 500, pendingReservation: { raidId: 'raid-123' } }
    });
    setupMocks(char);

    const before = JSON.stringify(char);
    await assert.rejects(
      AbilityService.purchaseAbility('acc123', {
        transactionId: 'tx-1002',
        abilityId: 'empower',
        expectedRevision: 2
      }),
      err => err.statusCode === 409 && err.errorCode === 'RESERVATION_PENDING'
    );
    assert.strictEqual(JSON.stringify(char), before);
  });

  await t.test('revision conflict rejects with 409 REVISION_CONFLICT', async () => {
    const char = makeCharacter({ revision: 5 });
    setupMocks(char);

    await assert.rejects(
      AbilityService.purchaseAbility('acc123', {
        transactionId: 'tx-1003',
        abilityId: 'empower',
        expectedRevision: 4
      }),
      err => err.statusCode === 409 && err.errorCode === 'REVISION_CONFLICT'
    );
  });

  await t.test('already unlocked ability rejects with 409 ALREADY_UNLOCKED', async () => {
    const char = makeCharacter({
      unlockedAbilities: ['empower'],
      revision: 2
    });
    setupMocks(char);

    await assert.rejects(
      AbilityService.purchaseAbility('acc123', {
        transactionId: 'tx-1004',
        abilityId: 'empower',
        expectedRevision: 2
      }),
      err => err.statusCode === 409 && err.errorCode === 'ALREADY_UNLOCKED'
    );
  });

  await t.test('ability not offered rejects with 404 ABILITY_NOT_OFFERED', async () => {
    const char = makeCharacter({ revision: 2 });
    setupMocks(char);

    await assert.rejects(
      AbilityService.purchaseAbility('acc123', {
        transactionId: 'tx-1005',
        abilityId: 'non_existent_spell',
        expectedRevision: 2
      }),
      err => err.statusCode === 404 && err.errorCode === 'ABILITY_NOT_OFFERED'
    );
  });

  await t.test('insufficient funds rejects with 422 INSUFFICIENT_FUNDS', async () => {
    const char = makeCharacter({
      inventory: { currency: 50, pendingReservation: null },
      revision: 2
    });
    setupMocks(char);

    await assert.rejects(
      AbilityService.purchaseAbility('acc123', {
        transactionId: 'tx-1006',
        abilityId: 'empower',
        expectedRevision: 2
      }),
      err => err.statusCode === 422 && err.errorCode === 'INSUFFICIENT_FUNDS'
    );
  });

  await t.test('input validation rejects invalid arguments', async () => {
    const char = makeCharacter();
    setupMocks(char);

    await assert.rejects(
      AbilityService.purchaseAbility('acc123', { transactionId: '', abilityId: 'empower', expectedRevision: 2 }),
      err => err.statusCode === 400 && err.errorCode === 'INVALID_TRANSACTION_ID'
    );

    await assert.rejects(
      AbilityService.purchaseAbility('acc123', { transactionId: 'tx-1', abilityId: '', expectedRevision: 2 }),
      err => err.statusCode === 400 && err.errorCode === 'INVALID_ABILITY_ID'
    );

    await assert.rejects(
      AbilityService.purchaseAbility('acc123', { transactionId: 'tx-1', abilityId: 'empower', expectedRevision: -1 }),
      err => err.statusCode === 400 && err.errorCode === 'INVALID_REVISION'
    );
  });
});

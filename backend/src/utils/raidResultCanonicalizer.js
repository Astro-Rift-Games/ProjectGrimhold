// backend/src/utils/raidResultCanonicalizer.js
'use strict';

/**
 * Deterministically constructs a canonical string representation of an authoritative extraction result
 * for cryptographic HMAC signing and verification.
 *
 * Ensures that modifying loot items, amounts, XP, equipment, recipient account, raid session,
 * result identity or issuance timestamp invalidates the signature.
 *
 * @param {Object} payload
 * @returns {string} Deterministic pipe-delimited canonical string
 */
function buildCanonicalResultString(payload) {
  if (!payload || typeof payload !== 'object') {
    throw new Error('Payload must be a non-null object');
  }

  const raidId = String(payload.raidId || '').trim();
  const resultId = String(payload.resultId || '').trim();
  const targetAccountId = String(payload.targetAccountId || payload.accountId || payload.targetCharacterId || '').trim();

  // Normalize and sort items deterministically by lootId ascending
  const items = Array.isArray(payload.items) ? payload.items : [];
  const canonicalItems = items
    .map(item => ({
      lootId: String(item.lootId || '').trim().toLowerCase(),
      amount: Number.isSafeInteger(item.amount) && item.amount > 0 ? item.amount : 0
    }))
    .filter(item => item.lootId.length > 0 && item.amount > 0)
    .sort((a, b) => a.lootId.localeCompare(b.lootId))
    .map(item => `${item.lootId}:${item.amount}`)
    .join(',');

  // Fixed order of all 8 equipment slots
  const eq = payload.preparedEquipment || {};
  const slotOrder = ['armor', 'boots', 'gloves', 'helmet', 'offHand1', 'offHand2', 'weaponSlot1', 'weaponSlot2'];
  const canonicalEquipment = slotOrder
    .map(slot => `${slot}=${String(eq[slot] || '').trim().toLowerCase()}`)
    .join(';');

  const experienceGranted = Number.isSafeInteger(payload.experienceGranted) && payload.experienceGranted >= 0
    ? payload.experienceGranted
    : 0;

  let issuedAt = '';
  if (payload.issuedAt) {
    const d = payload.issuedAt instanceof Date ? payload.issuedAt : new Date(payload.issuedAt);
    if (!isNaN(d.getTime())) {
      issuedAt = d.toISOString();
    }
  }

  return `${raidId}|${resultId}|${targetAccountId}|${canonicalItems}|${canonicalEquipment}|${experienceGranted}|${issuedAt}`;
}

module.exports = {
  buildCanonicalResultString
};

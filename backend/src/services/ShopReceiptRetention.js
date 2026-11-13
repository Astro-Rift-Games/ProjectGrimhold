// src/services/ShopReceiptRetention.js
//
// Idempotency beyond the receipt window (FIX-31). Writers keep only the newest SHOP_RECEIPT_WINDOW
// receipts, so a replay of an evicted id would otherwise be applied a second time. Each write that
// evicts receipts also advances `Character.shopReceiptWatermark` (single-document, same atomic
// update) to the newest evicted id timestamp; a timestamped id at or below it is rejected.
//
// Timestamped ids use the Unity wire format `<unix ms>_<32 hex>` (ShopTransactionId.ToString()).
// Legacy ids (bare GUID, free-form strings) carry no timestamp and keep the window-only behavior.
'use strict';

const SHOP_RECEIPT_WINDOW = 64;
const TIMESTAMPED_ID = /^(\d{1,15})_[0-9a-f]{32}$/i;

/** Unix milliseconds embedded in a timestamped transaction id, or null for any other id. */
function parseTransactionTimestamp(transactionId) {
  if (typeof transactionId !== 'string') return null;
  const match = TIMESTAMPED_ID.exec(transactionId);
  return match ? Number(match[1]) : null;
}

/** `$push` operand appending one receipt and keeping the newest SHOP_RECEIPT_WINDOW. */
function receiptPush(receipt) {
  return { $each: [receipt], $slice: -SHOP_RECEIPT_WINDOW };
}

/**
 * `$max` update advancing the watermark past the receipts the next receiptPush will evict.
 * Valid only while the write is guarded by the revision read with `character`.
 * The evicted receipt's server time caps its id timestamp so a skewed or forged client clock
 * cannot lock the character out of the shop.
 */
function watermarkUpdate(character) {
  const receipts = character.appliedShopReceipts || [];
  const evicted = receipts.slice(0, Math.max(0, receipts.length + 1 - SHOP_RECEIPT_WINDOW));
  let watermark = 0;
  for (const receipt of evicted) {
    const issuedAt = parseTransactionTimestamp(receipt.transactionId);
    if (issuedAt === null) continue;
    const storedAt = receipt.timestamp ? new Date(receipt.timestamp).getTime() : NaN;
    watermark = Math.max(watermark, Number.isFinite(storedAt) ? Math.min(issuedAt, storedAt) : issuedAt);
  }
  return watermark > 0 ? { $max: { shopReceiptWatermark: watermark } } : {};
}

/**
 * Call after no receipt matched `transactionId` and before any mutation: a timestamped id at or
 * below the watermark can only be a replay of a receipt that was already evicted.
 */
function assertWithinRetention(character, transactionId) {
  const issuedAt = parseTransactionTimestamp(transactionId);
  if (issuedAt === null || issuedAt > (character.shopReceiptWatermark || 0)) return;
  throw {
    statusCode: 409,
    errorCode: 'TRANSACTION_RETENTION_EXCEEDED',
    message: `This transaction id is older than the last ${SHOP_RECEIPT_WINDOW} transactions kept by the server ` +
      'and can no longer be confirmed or retried. Refresh your state and issue a new transaction.'
  };
}

module.exports = { SHOP_RECEIPT_WINDOW, parseTransactionTimestamp, receiptPush, watermarkUpdate, assertWithinRetention };

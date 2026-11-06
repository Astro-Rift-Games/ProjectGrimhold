// src/validators/inventory.validators.js
const { body, validationResult } = require('express-validator');
const { EQUIPMENT_SLOTS } = require('../config/equipmentSlots');

// Shared validation error handler — mirrors the pattern from other validators.
function handleValidationErrors(req, res, next) {
  const errors = validationResult(req);
  if (!errors.isEmpty()) {
    return next({
      statusCode: 400,
      errorCode: 'VALIDATION_FAILED',
      message: 'Invalid input parameters.',
      details: errors.array().map(err => ({ field: err.path, msg: err.msg }))
    });
  }
  next();
}

const inventoryOrderValidator = [
  body('list').isIn(['stash', 'loadout']).withMessage('must be stash or loadout'),
  body('lootIds').if((value, { req }) => Object.hasOwn(req.body, 'lootIds') || !Object.hasOwn(req.body, 'slotOrder'))
    .isArray().withMessage('must be an array'),
  body('lootIds.*')
    .isString().withMessage('must be a string')
    .bail()
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('slotOrder').optional().isArray().withMessage('must be an array'),
  body('slotOrder.*').custom(value => Number.isSafeInteger(value) && value >= 0).withMessage('must be a non-negative integer'),
  body('expectedRevision')
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  handleValidationErrors
];

const sourceSlotValidator = () => body('sourceSlot').optional()
  .custom(value => Number.isSafeInteger(value) && value >= 0).withMessage('must be a non-negative integer');

const slotMoveValidator = [
  body('fromList').isIn(['stash', 'loadout']),
  body('toList').isIn(['stash', 'loadout']),
  ...['fromSlot', 'toSlot'].map(key => body(key).custom(value => Number.isSafeInteger(value) && value >= 0)),
  body('lootId').isString().bail().trim().notEmpty(),
  body('expectedRevision').isInt({ min: 0 }),
  handleValidationErrors
];

const slotSplitValidator = [
  body('list').isIn(['stash', 'loadout']),
  ...['sourceSlot', 'destinationSlot'].map(key => body(key).custom(value => Number.isSafeInteger(value) && value >= 0)),
  body('lootId').isString().bail().trim().notEmpty(),
  body('expectedRevision').custom(value => Number.isSafeInteger(value) && value >= 0),
  // Quantity (including type) is validated against the guarded source by the service.
  handleValidationErrors
];

// Validates the body for move-to-loadout and move-to-stash operations.
const moveItemValidator = [
  sourceSlotValidator(),
  body('lootId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('amount')
    .isInt({ min: 1 }).withMessage('must be a positive integer'),
  body('expectedRevision')
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  handleValidationErrors
];

// Validates the body for the update-prepared-equipment operation.
// All slots are optional. Legacy strings and v3 {lootId, instanceId} intentions
// coexist; the service validates the format against the character's schema version.
const preparedEquipmentValidator = [
  ...EQUIPMENT_SLOTS.map(slot =>
    body(slot).optional().custom(async (value, { req }) => {
      if (typeof value === 'string') return true;
      const character = await require('../models/Character').findOne({ accountId: req.accountId });
      if (character?.inventory?.inventorySchemaVersion !== 3 ||
          (value !== null && (typeof value !== 'object' || Array.isArray(value)))) throw new Error('must be a string');
      return true;
    }).withMessage('must be a string')
  ),
  body('expectedRevision')
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  handleValidationErrors
];

// Validates the body for saving a pending reservation.
const pendingReservationValidator = [
  body('reservationId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  handleValidationErrors
];

// Admission accepts the same reservation identity, without changing save validation.
const admitReservationValidator = [
  body('reservationId')
    .isString().withMessage('must be a string')
    .bail()
    .trim()
    .notEmpty().withMessage('must not be empty'),
  handleValidationErrors
];

// Validates the body for an extraction loot commit.
// `items` may be empty (the player extracted but carried no loot).
const commitExtractionValidator = [
  body('raidId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('resultSequence')
    .isInt({ min: 1 }).withMessage('must be a positive integer'),
  body('items')
    .isArray().withMessage('must be an array'),
  body('items.*.lootId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('items.*.amount')
    .isInt({ min: 1 }).withMessage('must be a positive integer'),
  handleValidationErrors
];

const { MAX_TRANSACTION_AMOUNT } = require('../config/shopCatalog');

// Validates the body for shop sale operations.
const shopSellValidator = [
  sourceSlotValidator(),
  body('transactionId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('lootId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('amount')
    .isInt({ min: 1, max: MAX_TRANSACTION_AMOUNT }).withMessage(`must be an integer between 1 and ${MAX_TRANSACTION_AMOUNT}`),
  body('declaredSellValue')
    .optional()
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('expectedRevision')
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  handleValidationErrors
];

// Validates the body for shop purchase operations.
const shopBuyValidator = [
  body('transactionId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('lootId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('amount')
    .isInt({ min: 1, max: MAX_TRANSACTION_AMOUNT }).withMessage(`must be an integer between 1 and ${MAX_TRANSACTION_AMOUNT}`),
  body('declaredPrice')
    .optional()
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('expectedRevision')
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  handleValidationErrors
];

const shopBuybackValidator = [
  body('transactionId').isString().bail().trim().notEmpty().isLength({ max: 128 }),
  body('entryId').isString().bail().trim().notEmpty(),
  body('amount').isInt({ min: 1, max: MAX_TRANSACTION_AMOUNT }),
  body('expectedRevision').isInt({ min: 0 }),
  handleValidationErrors
];

// Trade accepts intentions only; reject unknown fields rather than silently trusting prices.
const { normalizeLootId } = require('../services/InventoryLootIdNormalizer');
const { MAX_TRADE_PURCHASE_LINES, MAX_TRADE_SALE_LINES } = require('../config/shopCatalog');
const shopTradeValidator = [
  body().custom(value => {
    if (!value || Object.keys(value).some(key => !['transactionId', 'expectedRevision', 'purchases', 'sales'].includes(key)) ||
        typeof value.transactionId !== 'string' || !value.transactionId.trim() || value.transactionId.length > 128 ||
        !Number.isSafeInteger(value.expectedRevision) || value.expectedRevision < 0 ||
        !Array.isArray(value.purchases) || !Array.isArray(value.sales) ||
        value.purchases.length > MAX_TRADE_PURCHASE_LINES || value.sales.length > MAX_TRADE_SALE_LINES ||
        value.purchases.length + value.sales.length === 0) return false;
    for (const side of ['purchases', 'sales']) {
      const ids = new Set(); const slots = new Set();
      for (const line of value[side]) {
        if (!line || typeof line !== 'object' || Array.isArray(line) ||
            Object.keys(line).some(key => !(side === 'sales' ? ['lootId', 'amount', 'sourceSlot'] : ['lootId', 'amount']).includes(key)) ||
            typeof line.lootId !== 'string' || !line.lootId.trim() ||
            !Number.isSafeInteger(line.amount) || line.amount < 1 || line.amount > MAX_TRANSACTION_AMOUNT) return false;
        const id = normalizeLootId(line.lootId.trim());
        if (ids.has(id)) return false;
        ids.add(id);
        if (line.sourceSlot !== undefined) {
          if (!Number.isSafeInteger(line.sourceSlot) || line.sourceSlot < 0 || slots.has(line.sourceSlot)) return false;
          slots.add(line.sourceSlot);
        }
      }
    }
    return true;
  }).withMessage('Invalid trade ticket.'),
  handleValidationErrors
];

module.exports = { shopTradeValidator, slotSplitValidator, slotMoveValidator, shopBuybackValidator, inventoryOrderValidator, moveItemValidator, preparedEquipmentValidator, pendingReservationValidator, admitReservationValidator, commitExtractionValidator, shopSellValidator, shopBuyValidator };

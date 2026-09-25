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

// Validates the body for move-to-loadout and move-to-stash operations.
const moveItemValidator = [
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
// All equipment slots are optional; if provided they must be strings.
// Generated from EQUIPMENT_SLOTS so new slots are covered automatically.
const preparedEquipmentValidator = [
  ...EQUIPMENT_SLOTS.map(slot =>
    body(slot).optional().isString().withMessage('must be a string')
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

// Validates the body for shop sale operations.
const shopSellValidator = [
  body('transactionId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('lootId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('amount')
    .isInt({ min: 1 }).withMessage('must be a positive integer'),
  body('declaredSellValue')
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
    .isInt({ min: 1 }).withMessage('must be a positive integer'),
  body('declaredPrice')
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('expectedRevision')
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  handleValidationErrors
];

module.exports = { moveItemValidator, preparedEquipmentValidator, pendingReservationValidator, commitExtractionValidator, shopSellValidator, shopBuyValidator };

// src/validators/ability.validators.js
'use strict';

const { body } = require('express-validator');
const validate = require('../middleware/validate');

const purchaseAbilityValidator = [
  body('transactionId')
    .isString().withMessage('transactionId must be a string')
    .trim().notEmpty().withMessage('transactionId is required')
    .isLength({ max: 64 }).withMessage('transactionId exceeds maximum length of 64 characters'),
  body('abilityId')
    .isString().withMessage('abilityId must be a string')
    .trim().notEmpty().withMessage('abilityId is required')
    .isLength({ max: 64 }).withMessage('abilityId exceeds maximum length of 64 characters'),
  body('expectedRevision')
    .isInt({ min: 0 }).withMessage('expectedRevision must be a non-negative integer'),
  validate
];

module.exports = { purchaseAbilityValidator };

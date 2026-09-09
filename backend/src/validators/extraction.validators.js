// src/validators/extraction.validators.js
//
// Validates the body for the unified extraction commit endpoint.
// POST /character/me/extraction/commit

'use strict';

const { body, validationResult } = require('express-validator');

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

const MAX_ITEMS = 64;

const commitExtractionUnifiedValidator = [
  body('raidId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),

  body('resultSequence')
    .isInt({ min: 1 }).withMessage('must be a positive integer'),



  handleValidationErrors,
];

module.exports = { commitExtractionUnifiedValidator };

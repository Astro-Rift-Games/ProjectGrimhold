// src/validators/progression.validators.js
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

const commitProgressionValidator = [
  body('raidId')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  body('resultSequence')
    .isInt({ min: 1 }).withMessage('must be a positive integer'),
  body('consolidatedExperience')
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('resultingLevel')
    .isInt({ min: 1 }).withMessage('must be a positive integer'),
  body('newLevel')
    .isInt({ min: 1 }).withMessage('must be a positive integer'),
  body('newExperience')
    .isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('characterAttributes')
    .optional()
    .isObject().withMessage('must be an object'),
  body('characterAttributes.vitality')
    .optional().isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('characterAttributes.resistance')
    .optional().isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('characterAttributes.strength')
    .optional().isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('characterAttributes.dexterity')
    .optional().isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('characterAttributes.intelligence')
    .optional().isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('characterAttributes.luck')
    .optional().isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  body('characterAttributes.availablePoints')
    .optional().isInt({ min: 0 }).withMessage('must be a non-negative integer'),
  handleValidationErrors
];

module.exports = { commitProgressionValidator };

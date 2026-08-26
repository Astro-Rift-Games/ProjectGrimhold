// src/validators/profile.validators.js
const { body, validationResult } = require('express-validator');

// Validates the update profile request body.
const updateProfileValidator = [
  body('customNote')
    .optional()
    .isString().withMessage('must be a string')
    .isLength({ max: 256 }).withMessage('cannot exceed 256 characters'),
  
  (req, res, next) => {
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
];

module.exports = { updateProfileValidator };

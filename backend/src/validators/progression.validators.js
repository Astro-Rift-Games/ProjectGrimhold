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
  body('attribute')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('must not be empty'),
  handleValidationErrors
];

module.exports = { commitProgressionValidator };

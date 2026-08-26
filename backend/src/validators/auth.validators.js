// src/validators/auth.validators.js
const { body, validationResult } = require('express-validator');

// Validates the login request body.
const loginValidator = [
  body('username')
    .isString().withMessage('must be a string')
    .trim()
    .notEmpty().withMessage('cannot be empty'),
  body('password')
    .isString().withMessage('must be a string')
    .notEmpty().withMessage('cannot be empty'),
  
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


const registerValidator = [
  body('username')
    .isString().withMessage('must be a string')
    .trim()
    .isLength({ min: 3, max: 20 }).withMessage('must be between 3 and 20 characters')
    .matches(/^[a-zA-Z0-9 ]+$/).withMessage('can only contain alfanumeric characters and spaces'),
  body('password')
    .isString().withMessage('must be a string')
    .isLength({ min: 6 }).withMessage('must be at least 6 characters long'),
  
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

module.exports = { loginValidator, registerValidator };

const { body, validationResult } = require('express-validator');

const createCharacterValidator = [
  body('name')
    .isString().withMessage('must be a string')
    .trim()
    .isLength({ min: 3, max: 16 }).withMessage('must be between 3 and 16 characters')
    .matches(/^[a-zA-Z0-9 ]+$/).withMessage('can only contain letters, numbers, and spaces')
    .custom(value => !/\s{2,}/.test(value)).withMessage('cannot contain consecutive spaces'),
  
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

module.exports = { createCharacterValidator };

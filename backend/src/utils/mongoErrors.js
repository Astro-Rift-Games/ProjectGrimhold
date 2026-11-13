'use strict';

// MongoDB reports unique-index violations with code 11000 (E11000 duplicate key).
function isDuplicateKeyError(error) {
  return Boolean(error) && error.code === 11000;
}

module.exports = { isDuplicateKeyError };

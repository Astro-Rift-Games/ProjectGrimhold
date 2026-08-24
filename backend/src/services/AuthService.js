// src/services/AuthService.js
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const Account = require('../models/Account');
const config = require('../config/env');

class AuthService {
  /**
   * Authenticates a user and returns a JWT token.
   * @throws 401 error if credentials are invalid.
   */
  static async login(username, password) {
    const account = await Account.findOne({ username: username.toLowerCase() });
    if (!account) {
      throw {
        statusCode: 401,
        errorCode: 'INVALID_CREDENTIALS',
        message: 'Invalid username or password.'
      };
    }

    const isMatch = await bcrypt.compare(password, account.passwordHash);
    if (!isMatch) {
      throw {
        statusCode: 401,
        errorCode: 'INVALID_CREDENTIALS',
        message: 'Invalid username or password.'
      };
    }

    // Generate JWT. 'sub' contains the account ID.
    const token = jwt.sign({ sub: account._id }, config.jwtSecret, {
      expiresIn: config.jwtExpiresIn + 's'
    });

    return { token, expiresIn: parseInt(config.jwtExpiresIn, 10) };
  }
}

module.exports = AuthService;

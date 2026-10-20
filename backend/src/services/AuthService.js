// src/services/AuthService.js
const crypto = require('crypto');
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const Account = require('../models/Account');
const RevokedToken = require('../models/RevokedToken');
const config = require('../config/env');

class AuthService {
  /**
   * Authenticates a user and returns a JWT token with unique jti.
   * @throws 401 error if credentials are invalid.
   */
  static async register(username, password) {
    const existingAccount = await Account.findOne({ username: username.toLowerCase().trim() });
    if (existingAccount) {
      throw {
        statusCode: 409,
        errorCode: 'USERNAME_TAKEN',
        message: 'Username already exists'
      };
    }

    const passwordHash = await bcrypt.hash(password, 10);
    const account = new Account({ username, passwordHash });
    await account.save();

    const jti = crypto.randomUUID();
    const token = jwt.sign({ sub: account._id, jti }, config.jwtSecret, {
      expiresIn: config.jwtExpiresIn + 's'
    });

    return { token, expiresIn: parseInt(config.jwtExpiresIn, 10) };
  }

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

    // Generate JWT. 'sub' contains the account ID, 'jti' uniquely identifies this session.
    const jti = crypto.randomUUID();
    const token = jwt.sign({ sub: account._id, jti }, config.jwtSecret, {
      expiresIn: config.jwtExpiresIn + 's'
    });

    return { token, expiresIn: parseInt(config.jwtExpiresIn, 10) };
  }

  /**
   * Revokes the specified session token persistently.
   * Other sessions of the same account remain unaffected.
   */
  static async logout(accountId, token) {
    if (!token) {
      return false;
    }

    const tokenHash = crypto.createHash('sha256').update(token).digest('hex');
    let expiresAt;
    let jti = null;

    try {
      const decoded = jwt.decode(token);
      if (decoded && decoded.exp) {
        expiresAt = new Date(decoded.exp * 1000);
        jti = decoded.jti || null;
      }
    } catch (_err) {
      // ignore decode error, will use fallback expiry
    }

    if (!expiresAt || isNaN(expiresAt.getTime())) {
      expiresAt = new Date(Date.now() + (parseInt(config.jwtExpiresIn, 10) || 3600) * 1000);
    }

    await RevokedToken.findOneAndUpdate(
      { tokenHash },
      {
        tokenHash,
        jti,
        accountId,
        expiresAt,
        revokedAt: new Date(),
        reason: 'logout'
      },
      { upsert: true, returnDocument: 'after' }
    );

    return true;
  }
}

module.exports = AuthService;

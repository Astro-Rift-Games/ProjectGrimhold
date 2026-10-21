// src/middleware/authenticate.js
const crypto = require('crypto');
const jwt = require('jsonwebtoken');
const config = require('../config/env');
const RevokedToken = require('../models/RevokedToken');

// Middleware to protect routes by requiring a valid JWT token.
// The token is opaque to the client (Unity), but contains the accountId
// in the 'sub' field, which is extracted here and injected into the request.
// Also verifies that the token has not been revoked (e.g. via logout).
async function authenticate(req, res, next) {
  const authHeader = req.headers.authorization;
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return next({
      statusCode: 401,
      errorCode: 'UNAUTHORIZED',
      message: 'Missing or invalid Authorization header.'
    });
  }

  const token = authHeader.split(' ')[1];

  try {
    const decoded = jwt.verify(token, config.jwtSecret);

    const tokenHash = crypto.createHash('sha256').update(token).digest('hex');
    const isRevoked = await RevokedToken.exists({ tokenHash });
    if (isRevoked) {
      return next({
        statusCode: 401,
        errorCode: 'UNAUTHORIZED',
        message: 'Token has been revoked.'
      });
    }

    // Inject accountId, token, and jti into request for downstream handlers
    req.accountId = decoded.sub;
    req.token = token;
    req.jti = decoded.jti || null;
    next();
  } catch (err) {
    if (err && err.statusCode) {
      return next(err);
    }
    return next({
      statusCode: 401,
      errorCode: 'UNAUTHORIZED',
      message: 'Token is invalid or expired.'
    });
  }
}

module.exports = authenticate;


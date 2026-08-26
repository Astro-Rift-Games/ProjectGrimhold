// src/middleware/authenticate.js
const jwt = require('jsonwebtoken');
const config = require('../config/env');

// Middleware to protect routes by requiring a valid JWT token.
// The token is opaque to the client (Unity), but contains the accountId
// in the 'sub' field, which is extracted here and injected into the request.
function authenticate(req, res, next) {
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
    // Inject accountId into request for downstream handlers
    req.accountId = decoded.sub;
    next();
  } catch (err) {
    return next({
      statusCode: 401,
      errorCode: 'UNAUTHORIZED',
      message: 'Token is invalid or expired.'
    });
  }
}

module.exports = authenticate;

// src/routes/auth.routes.js
const express = require('express');
const router = express.Router();
const AuthService = require('../services/AuthService');
const { loginValidator, registerValidator } = require('../validators/auth.validators');
const authenticate = require('../middleware/authenticate');
const { MemoryRateLimiter, UserFailedLoginLimiter } = require('../middleware/rateLimiter');
const config = require('../config/env');

const ipRateLimiter = new MemoryRateLimiter({
  windowMs: config.rateLimitWindowMs,
  max: config.rateLimitIpMax,
  message: 'Too many requests from this IP. Please try again later.'
});

const userLoginLimiter = new UserFailedLoginLimiter({
  windowMs: config.rateLimitWindowMs,
  max: config.rateLimitUserMax,
  message: 'Too many failed login attempts for this account. Please try again later.'
});

// Expose rate limiters on router for testing and administrative inspection
router.ipRateLimiter = ipRateLimiter;
router.userLoginLimiter = userLoginLimiter;

// POST /auth/login
router.post('/login', ipRateLimiter.middleware(), userLoginLimiter.preCheck(), loginValidator, async (req, res, next) => {
  const username = req.body && req.body.username;
  try {
    const { password } = req.body;
    const result = await AuthService.login(username, password);
    userLoginLimiter.clearFailures(username);
    res.json(result);
  } catch (err) {
    if (err && err.statusCode === 401) {
      userLoginLimiter.recordFailure(username);
    }
    next(err);
  }
});

// POST /auth/register
router.post('/register', ipRateLimiter.middleware(), registerValidator, async (req, res, next) => {
  try {
    const { username, password } = req.body;
    const result = await AuthService.register(username, password);
    res.status(201).json(result);
  } catch (err) {
    next(err);
  }
});

// POST /auth/logout
router.post('/logout', authenticate, async (req, res, next) => {
  try {
    const token = req.headers.authorization.split(' ')[1];
    await AuthService.logout(req.accountId, token);
    res.status(204).send(); // 204 No Content
  } catch (err) {
    next(err);
  }
});

module.exports = router;


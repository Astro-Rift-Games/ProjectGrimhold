// src/routes/auth.routes.js
const express = require('express');
const router = express.Router();
const AuthService = require('../services/AuthService');
const { loginValidator, registerValidator } = require('../validators/auth.validators');
const authenticate = require('../middleware/authenticate');

// POST /auth/login
router.post('/login', loginValidator, async (req, res, next) => {
  try {
    const { username, password } = req.body;
    const result = await AuthService.login(username, password);
    res.json(result);
  } catch (err) {
    next(err);
  }
});


// POST /auth/register
router.post('/register', registerValidator, async (req, res, next) => {
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

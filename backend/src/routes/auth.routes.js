// src/routes/auth.routes.js
const express = require('express');
const router = express.Router();
const AuthService = require('../services/AuthService');
const { loginValidator } = require('../validators/auth.validators');

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

module.exports = router;

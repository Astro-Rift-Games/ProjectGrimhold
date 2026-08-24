// src/routes/character.routes.js
const express = require('express');
const router = express.Router();
const CharacterService = require('../services/CharacterService');
const authenticate = require('../middleware/authenticate');
const { updateProfileValidator } = require('../validators/profile.validators');

// Apply authentication middleware to all routes in this router
router.use(authenticate);

// GET /character/me
router.get('/me', async (req, res, next) => {
  try {
    const character = await CharacterService.getByAccountId(req.accountId);
    res.json({
      characterId: character._id.toString(),
      name: character.name
    });
  } catch (err) {
    next(err);
  }
});

// GET /character/me/profile
router.get('/me/profile', async (req, res, next) => {
  try {
    const character = await CharacterService.getByAccountId(req.accountId);
    res.json({
      characterId: character._id.toString(),
      profile: character.profile
    });
  } catch (err) {
    next(err);
  }
});

// PATCH /character/me/profile
router.patch('/me/profile', updateProfileValidator, async (req, res, next) => {
  try {
    const character = await CharacterService.updateProfile(req.accountId, req.body);
    res.json({
      characterId: character._id.toString(),
      profile: character.profile
    });
  } catch (err) {
    next(err);
  }
});

module.exports = router;

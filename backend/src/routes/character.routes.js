// src/routes/character.routes.js
const express = require('express');
const router = express.Router();
const CharacterService = require('../services/CharacterService');
const authenticate = require('../middleware/authenticate');
const { updateProfileValidator } = require('../validators/profile.validators');
const { createCharacterValidator } = require('../validators/character.validators');

// Apply authentication middleware to all routes in this router
router.use(authenticate);


// POST /character/me
router.post('/me', createCharacterValidator, async (req, res, next) => {
  try {
    const { name } = req.body;
    const result = await CharacterService.createForAccount(req.accountId, name);
    res.status(201).json(result);
  } catch (err) {
    next(err);
  }
});

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

const SessionTicketService = require('../services/SessionTicketService');

// POST /character/me/session-ticket/town
// Issues a verified Town Session Ticket for the authenticated account's character.
router.post('/me/session-ticket/town', async (req, res, next) => {
  try {
    const result = await SessionTicketService.issueTownTicket(req.accountId);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/verify-session-ticket
// Verifies a Town Session Ticket.
router.post('/verify-session-ticket', async (req, res, next) => {
  try {
    const { ticket } = req.body;
    const result = SessionTicketService.verifyTownTicket(ticket);
    if (!result.valid) {
      return res.status(401).json(result);
    }
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/me/raid/admission-ticket
// Issues an Authoritative Raid Admission Ticket for the active reservation.
// Body: { raidCode: string, reservationId: string }
router.post('/me/raid/admission-ticket', async (req, res, next) => {
  try {
    const { raidCode, reservationId } = req.body;
    const result = await SessionTicketService.issueRaidAdmissionTicket(req.accountId, raidCode, reservationId);
    res.json(result);
  } catch (err) {
    next(err);
  }
});

// POST /character/raid/verify-admission
// Verifies an Authoritative Raid Admission Ticket against expected raidCode.
// Body: { ticket: string, raidCode: string }
router.post('/raid/verify-admission', async (req, res, next) => {
  try {
    const { ticket, raidCode } = req.body;
    const result = SessionTicketService.verifyRaidAdmissionTicket(ticket, raidCode);
    if (!result.valid) {
      return res.status(401).json(result);
    }
    res.json(result);
  } catch (err) {
    next(err);
  }
});

module.exports = router;

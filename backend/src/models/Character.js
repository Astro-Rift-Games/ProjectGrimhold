const mongoose = require('mongoose');

// Embedded schema for a single item slot (stash or loadout entry).
const itemSchema = new mongoose.Schema({
  lootId: { type: String, required: true },
  amount: { type: Number, required: true, min: 1 }
}, { _id: false });

// Embedded schema for the six equipment assignment slots.
// Each field stores a lootId string; empty string means unassigned.
const preparedEquipmentSchema = new mongoose.Schema({
  weaponSlot1: { type: String, default: '' },
  weaponSlot2: { type: String, default: '' },
  helmet:      { type: String, default: '' },
  armor:       { type: String, default: '' },
  gloves:      { type: String, default: '' },
  boots:       { type: String, default: '' }
}, { _id: false });

// Embedded schema for the pending raid reservation snapshot.
// Captures the loadout items and equipment committed at the moment the player
// queued for a raid. Used to restore state after a disconnection.
const pendingReservationSchema = new mongoose.Schema({
  reservationId:    { type: String, required: true },
  items:            { type: [itemSchema], default: [] },
  preparedEquipment: { type: preparedEquipmentSchema, default: () => ({}) }
}, { _id: false });

// Embedded schema for a single applied extraction receipt.
// Acts as an idempotency key: (raidId, resultSequence) is unique per character.
// profileId is intentionally omitted — it is implicit in the document owner.
const extractionReceiptSchema = new mongoose.Schema({
  raidId:         { type: String, required: true },
  resultSequence: { type: Number, required: true }
}, { _id: false });

const progressionReceiptSchema = new mongoose.Schema({
  raidId:                 { type: String, required: true },
  resultSequence:         { type: Number, required: true },
  consolidatedExperience: { type: Number, required: true },
  resultingLevel:         { type: Number, required: true }
}, { _id: false });

const characterAttributeStateSchema = new mongoose.Schema({
  vitality:        { type: Number, default: 0, min: 0 },
  resistance:      { type: Number, default: 0, min: 0 },
  strength:        { type: Number, default: 0, min: 0 },
  dexterity:       { type: Number, default: 0, min: 0 },
  intelligence:    { type: Number, default: 0, min: 0 },
  luck:            { type: Number, default: 0, min: 0 },
  availablePoints: { type: Number, default: 0, min: 0 }
}, { _id: false });

const characterSchema = new mongoose.Schema({
  accountId: {
    type: mongoose.Schema.Types.ObjectId,
    ref: 'Account',
    required: true,
    unique: true // Garantiza: 1 cuenta = 1 personaje
  },
  name: {
    type: String,
    required: true
  },
  profile: {
    lastSeen:   { type: Date, default: null },
    customNote: { type: String, maxlength: 256, default: '' }
  },
  level:      { type: Number, default: 1 },
  experience: { type: Number, default: 0 },
  lastAppliedProgressionResultSequence: { type: Number, default: 0 },
  lastProgressionReceipt:               { type: progressionReceiptSchema, default: null },
  appliedProgressionReceipts:           { type: [progressionReceiptSchema], default: [] },
  characterAttributes:                  { type: characterAttributeStateSchema, default: () => ({}) },
  // Durable inventory state.
  // Persisted only when the player moves an item between containers (save-on-move).
  inventory: {
    stash:              { type: [itemSchema],             default: [] },
    loadout:            { type: [itemSchema],             default: [] },
    preparedEquipment:  { type: preparedEquipmentSchema,  default: () => ({}) },
    // Null when no raid reservation is active; set when the player queues for a raid.
    pendingReservation: { type: pendingReservationSchema, default: null },
    // Idempotency log for extraction commits (mirrors LocalProfileSnapshot.MaxAppliedExtractionReceipts = 256).
    // Capped at 256 entries; oldest entries are evicted when the cap is exceeded.
    appliedExtractionReceipts: { type: [extractionReceiptSchema], default: [] }
  },
  createdAt: {
    type: Date,
    default: Date.now
  }
});

module.exports = mongoose.model('Character', characterSchema);

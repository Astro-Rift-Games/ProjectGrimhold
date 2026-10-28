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
  offHand1:    { type: String, default: '' },
  helmet:      { type: String, default: '' },
  armor:       { type: String, default: '' },
  gloves:      { type: String, default: '' },
  boots:       { type: String, default: '' },
  offHand2:    { type: String, default: '' }
}, { _id: false });

const preparedAbilitiesSchema = new mongoose.Schema({
  slot1: { type: String, default: '' },
  slot2: { type: String, default: '' }
}, { _id: false });

// Embedded schema for the pending raid reservation snapshot.
// Captures the loadout items and equipment committed at the moment the player
// queued for a raid. Used to restore state after a disconnection.
const pendingReservationSchema = new mongoose.Schema({
  reservationId:     { type: String, required: true },
  items:             { type: [itemSchema], default: [] },
  preparedEquipment: { type: preparedEquipmentSchema, default: () => ({}) },
  preparedAbilities: { type: preparedAbilitiesSchema, default: () => ({}) }
}, { _id: false });

// Embedded schema for a single applied extraction receipt.
// Acts as an idempotency key: (raidId, resultSequence) is unique per character.
// profileId is intentionally omitted — it is implicit in the document owner.
const extractionReceiptSchema = new mongoose.Schema({
  raidId:         { type: String, required: true },
  resultSequence: { type: Number, required: true },
  resultId:       { type: String },
  timestamp:      { type: Date, default: Date.now }
}, { _id: false });

const progressionReceiptSchema = new mongoose.Schema({
  raidId:                 { type: String, required: true },
  resultSequence:         { type: Number, required: true },
  consolidatedExperience: { type: Number, required: true },
  resultingLevel:         { type: Number, required: true }
}, { _id: false });

const characterAttributeStateSchema = new mongoose.Schema({
  vitality:        { type: Number, default: 5, min: 0 },
  resistance:      { type: Number, default: 5, min: 0 },
  strength:        { type: Number, default: 5, min: 0 },
  dexterity:       { type: Number, default: 5, min: 0 },
  intelligence:    { type: Number, default: 5, min: 0 },
  luck:            { type: Number, default: 5, min: 0 },
  availablePoints: { type: Number, default: 10, min: 0 }
}, { _id: false });

const buybackEntrySchema = new mongoose.Schema({
  entryId: { type: String, required: true },
  lootId: { type: String, required: true },
  amount: { type: Number, required: true, min: 1 },
  unitPrice: { type: Number, required: true, min: 0 },
  soldAt: { type: Date, default: Date.now }
}, { _id: false });
const buybackSchema = new mongoose.Schema({
  sessionEpoch: { type: String, default: '' },
  entries: { type: [buybackEntrySchema], default: [] }
}, { _id: false });

const shopReceiptSchema = new mongoose.Schema({
  transactionId: { type: String, required: true },
  type:          { type: String, enum: ['buy', 'sell', 'buyback', 'ability_purchase'], required: true },
  entryId:       { type: String },
  lootId:        { type: String },
  abilityId:     { type: String, default: '' },
  amount:        { type: Number },
  totalAmount:   { type: Number },
  timestamp:     { type: Date, default: Date.now }
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
  preparedAbilities:                    { type: preparedAbilitiesSchema,       default: () => ({}) },
  unlockedAbilities:                    { type: [String],                      default: [] },
  inventory: {
    buyback: { type: buybackSchema, default: () => ({}) },
    currency:           { type: Number, default: 0 },
    stash:              { type: [itemSchema],             default: [] },
    loadout:            { type: [itemSchema],             default: [] },
    preparedEquipment:  { type: preparedEquipmentSchema,  default: () => ({}) },
    // Null when no raid reservation is active; set when the player queues for a raid.
    pendingReservation: { type: pendingReservationSchema, default: null },
    // Durable backend replay barriers: unlike the client cache, these must not be pruned.
    // Authoritative results have a TTL, so these receipts preserve single consumption.
    appliedExtractionReceipts: { type: [extractionReceiptSchema], default: [] }
  },
  createdAt: {
    type: Date,
    default: Date.now
  },
  // Capped at 64 entries (at-most-once window for Shop transactions)
  appliedShopReceipts: { type: [shopReceiptSchema], default: [] },
  revision: {
    type: Number,
    default: 0
  }
});

module.exports = mongoose.model('Character', characterSchema);

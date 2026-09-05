require('dotenv').config();
const mongoose = require('mongoose');
const Character = require('./src/models/Character');

async function test() {
  await mongoose.connect(process.env.MONGODB_URI || 'mongodb://localhost:27017/grimhold');
  
  const char = await Character.findOne({});
  if (!char) {
    console.log('No character found');
    process.exit(1);
  }

  console.log('Before update:', char.characterAttributes);

  // simulate payload
  const payload = {
    vitality: 9,
    resistance: 8,
    strength: 7,
    dexterity: 6,
    intelligence: 5,
    luck: 4,
    availablePoints: 3
  };

  char.characterAttributes = payload;
  char.markModified('characterAttributes');
  
  await char.save();
  console.log('Saved.');

  const char2 = await Character.findOne({});
  console.log('After update:', char2.characterAttributes);

  process.exit(0);
}

test();

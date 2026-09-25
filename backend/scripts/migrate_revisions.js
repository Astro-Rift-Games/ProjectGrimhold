const mongoose = require('mongoose');
require('dotenv').config();
const Character = require('../src/models/Character');

async function migrate() {
  try {
    await mongoose.connect(process.env.MONGODB_URI);
    console.log('Connected to MongoDB.');

    const result = await Character.updateMany(
      { revision: { $exists: false } },
      { $set: { revision: 0 } }
    );

    console.log(`Migration complete. Modified ${result.modifiedCount} characters.`);
  } catch (err) {
    console.error('Migration failed:', err);
  } finally {
    await mongoose.disconnect();
    console.log('Disconnected from MongoDB.');
    process.exit(0);
  }
}

migrate();

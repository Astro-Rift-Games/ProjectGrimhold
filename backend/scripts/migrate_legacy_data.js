const mongoose = require('mongoose');
require('dotenv').config();

async function migrate() {
  const isDryRun = process.argv.includes('--dry-run');
  console.log(`Starting migration...${isDryRun ? ' [DRY RUN]' : ''}`);

  try {
    await mongoose.connect(process.env.MONGODB_URI);
    console.log('Connected to MongoDB.');

    const db = mongoose.connection.db;
    const collection = db.collection('characters');

    const noAttrsCount = await collection.countDocuments({ characterAttributes: { $exists: false } });
    const noPointsCount = await collection.countDocuments({ 'characterAttributes.availablePoints': { $exists: false } });
    const noRevisionCount = await collection.countDocuments({ revision: { $exists: false } });

    console.log(`Documents missing characterAttributes: ${noAttrsCount}`);
    console.log(`Documents missing characterAttributes.availablePoints: ${noPointsCount}`);
    console.log(`Documents missing revision: ${noRevisionCount}`);

    if (isDryRun) {
      console.log('Dry run complete. No documents were modified.');
      return;
    }

    // Use updateMany to safely set defaults using $set for fields that don't exist
    const updateResult = await collection.updateMany(
      {
        $or: [
          { revision: { $exists: false } },
          { characterAttributes: { $exists: false } },
          { 'characterAttributes.availablePoints': { $exists: false } }
        ]
      },
      [
        {
          $set: {
            revision: { $ifNull: ["$revision", 0] },
            characterAttributes: {
              $cond: {
                if: { $eq: [{ $type: "$characterAttributes" }, "missing"] },
                then: {
                  vitality: 5, resistance: 5, strength: 5,
                  dexterity: 5, intelligence: 5, luck: 5,
                  availablePoints: 10
                },
                else: {
                  vitality: { $ifNull: ["$characterAttributes.vitality", 5] },
                  resistance: { $ifNull: ["$characterAttributes.resistance", 5] },
                  strength: { $ifNull: ["$characterAttributes.strength", 5] },
                  dexterity: { $ifNull: ["$characterAttributes.dexterity", 5] },
                  intelligence: { $ifNull: ["$characterAttributes.intelligence", 5] },
                  luck: { $ifNull: ["$characterAttributes.luck", 5] },
                  availablePoints: { $ifNull: ["$characterAttributes.availablePoints", 10] }
                }
              }
            }
          }
        }
      ]
    );

    console.log(`Migration complete. Modified ${updateResult.modifiedCount} characters.`);
    
    // Verify remaining
    const remainingNoAttrs = await collection.countDocuments({ characterAttributes: { $exists: false } });
    const remainingNoPoints = await collection.countDocuments({ 'characterAttributes.availablePoints': { $exists: false } });
    const remainingNoRevision = await collection.countDocuments({ revision: { $exists: false } });
    
    console.log(`Verification:`);
    console.log(`- missing characterAttributes: ${remainingNoAttrs}`);
    console.log(`- missing availablePoints: ${remainingNoPoints}`);
    console.log(`- missing revision: ${remainingNoRevision}`);

  } catch (err) {
    console.error('Migration failed:', err);
  } finally {
    await mongoose.disconnect();
    console.log('Disconnected from MongoDB.');
    process.exit(0);
  }
}

migrate();

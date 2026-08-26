require('dotenv').config();
const mongoose = require('mongoose');
const bcrypt = require('bcryptjs');
const Account = require('../src/models/Account');
const Character = require('../src/models/Character');

const DB_URI = process.env.MONGODB_URI;
if (!DB_URI) {
  console.error('[Seed] Error: MONGODB_URI no está definido en las variables de entorno.');
  process.exit(1);
}

// Configuración de las cuentas semilla
const SEED_PASSWORD = process.argv[2] || 'test1234';
const ACCOUNTS = [
  { username: 'tester_a', charName: 'Alpha' },
  { username: 'tester_b', charName: 'Bravo' }
];

async function runSeed() {
  try {
    await mongoose.connect(DB_URI);
    console.log('[Seed] Conectado a MongoDB.');

    const saltRounds = 10;
    const passwordHash = await bcrypt.hash(SEED_PASSWORD, saltRounds);

    for (const data of ACCOUNTS) {
      // 1. Upsert Account (Idempotente)
      const account = await Account.findOneAndUpdate(
        { username: data.username },
        { $set: { passwordHash } },
        { upsert: true, returnDocument: 'after' }
      );
      console.log(`[Seed] Account '${account.username}' lista (ID: ${account._id}).`);

      // 2. Upsert Character (Idempotente)
      const character = await Character.findOneAndUpdate(
        { accountId: account._id },
        { $setOnInsert: { name: data.charName } }, // Solo setear nombre en creación
        { upsert: true, returnDocument: 'after' }
      );
      console.log(`[Seed] Character '${character.name}' listo.`);
      console.log(`       -> CharacterId: ${character._id}`);
      console.log('-----------------------------------');
    }

    console.log('[Seed] Población finalizada con éxito.');
  } catch (err) {
    console.error('[Seed] Error catastrófico:', err);
    process.exitCode = 1;
  } finally {
    await mongoose.connection.close();
    console.log('[Seed] Conexión cerrada.');
  }
}

runSeed();


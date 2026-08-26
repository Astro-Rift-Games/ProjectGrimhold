const mongoose = require('mongoose');
const Account = require('./src/models/Account');
const Character = require('./src/models/Character');
const config = require('./src/config/env');

async function run() {
  await mongoose.connect(config.mongodbUri);
  
  const accounts = await Account.find({});
  const characters = await Character.find({});
  
  const charAccountIds = new Set(characters.map(c => c.accountId.toString()));
  
  const accountsWithoutChar = accounts.filter(a => !charAccountIds.has(a._id.toString()));
  
  console.log('Accounts without characters:');
  for (const acc of accountsWithoutChar) {
    console.log('- ' + acc.username);
  }
  
  if (accountsWithoutChar.length === 0) {
    console.log('(None)');
  }
  
  process.exit(0);
}

run().catch(console.error);

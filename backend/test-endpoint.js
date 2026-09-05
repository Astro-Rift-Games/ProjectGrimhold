require('dotenv').config();
const jwt = require('jsonwebtoken');

async function test() {
  const token = jwt.sign({ accountId: 'test-account-id' }, process.env.JWT_SECRET || 'secret');
  const res = await fetch('http://localhost:3000//character/me/progression/commit', {
    method: 'POST',
    headers: {
      'Authorization': 'Bearer ' + token,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ characterAttributes: { vitality: 6 } })
  });
  
  const text = await res.text();
  console.log('Status:', res.status);
  console.log('Body:', text);
}

test();

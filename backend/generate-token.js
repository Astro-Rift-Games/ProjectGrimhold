const jwt = require('jsonwebtoken');

const token = jwt.sign({ accountId: 'test-account-id' }, process.env.JWT_SECRET || 'secret');
console.log('Token:', token);

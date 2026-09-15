// src/config/env.js
require('dotenv').config();

const REQUIRED = ['MONGODB_URI', 'JWT_SECRET', 'PORT'];

const missing = REQUIRED.filter((key) => !process.env[key]);
if (missing.length > 0) {
  console.error('[Config] Missing required environment variables: ' + missing.join(', '));
  process.exit(1);
}

module.exports = {
  port: parseInt(process.env.PORT, 10),
  mongodbUri: process.env.MONGODB_URI,
  jwtSecret: process.env.JWT_SECRET,
  jwtExpiresIn: process.env.JWT_EXPIRES_IN || '3600',
  nodeEnv: process.env.NODE_ENV || 'development',
  webhookSecret: process.env.WEBHOOK_SECRET || 'dev-webhook-secret-do-not-use-in-prod',
};

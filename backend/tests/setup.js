'use strict';

// Deterministic test configuration; never load local credentials or contact a remote DB.
Object.assign(process.env, {
  NODE_ENV: 'test',
  PORT: '3000',
  MONGODB_URI: 'mongodb://127.0.0.1:27017/grimhold_test',
  JWT_SECRET: 'synthetic-test-jwt-secret-not-for-deployment',
  WEBHOOK_SECRET: 'synthetic-test-webhook-secret-not-for-deployment',
  TRUST_PROXY: '',
  AUTH_RATE_LIMIT_WINDOW_MS: '900000',
  AUTH_RATE_LIMIT_IP_MAX: '30',
  AUTH_RATE_LIMIT_USER_MAX: '5'
});
require('dotenv').config = () => ({ parsed: {} });

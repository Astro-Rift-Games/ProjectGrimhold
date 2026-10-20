// src/config/env.js
require('dotenv').config();

const REQUIRED = ['MONGODB_URI', 'JWT_SECRET', 'PORT'];

const missing = REQUIRED.filter((key) => !process.env[key]);
if (missing.length > 0) {
  console.error('[Config] Missing required environment variables: ' + missing.join(', '));
  process.exit(1);
}

const ALLOWED_ENVS = ['development', 'test', 'staging', 'production'];
if (process.env.NODE_ENV && !ALLOWED_ENVS.includes(process.env.NODE_ENV)) {
  console.error(`[Config] Invalid NODE_ENV: '${process.env.NODE_ENV}'. Allowed values: ${ALLOWED_ENVS.join(', ')}`);
  process.exit(1);
}

const nodeEnv = process.env.NODE_ENV || 'development';
const webhookSecret = (process.env.WEBHOOK_SECRET || 'dev-webhook-secret-do-not-use-in-prod').trim();

const INSECURE_JWT_SECRETS = ['dev-secret', 'secret', 'changeme', 'jwt-secret', 'default', '123456'];
if (nodeEnv === 'production') {
  if (
    webhookSecret === 'dev-webhook-secret-do-not-use-in-prod' ||
    webhookSecret.length < 32 ||
    INSECURE_JWT_SECRETS.includes(webhookSecret.toLowerCase())
  ) {
    console.error('[Config] CRITICAL: WEBHOOK_SECRET must be set to a secure value of at least 32 characters in production.');
    process.exit(1);
  }

  const trimmedSecret = (process.env.JWT_SECRET || '').trim();
  if (trimmedSecret.length < 32 || INSECURE_JWT_SECRETS.includes(trimmedSecret.toLowerCase())) {
    console.error('[Config] CRITICAL: JWT_SECRET must be at least 32 characters of high entropy in production.');
    process.exit(1);
  }
}

module.exports = {
  port: parseInt(process.env.PORT, 10),
  mongodbUri: process.env.MONGODB_URI,
  jwtSecret: process.env.JWT_SECRET,
  jwtExpiresIn: process.env.JWT_EXPIRES_IN || '3600',
  nodeEnv: nodeEnv,
  webhookSecret: webhookSecret,
  get enableDebugRoutes() {
    return this.nodeEnv !== 'production' && process.env.ENABLE_DEV_DEBUG_ROUTES === 'true';
  },
  get enableLegacyExtraction() {
    return this.nodeEnv !== 'production' && process.env.ENABLE_LEGACY_EXTRACTION === 'true';
  },
  trustProxy: process.env.TRUST_PROXY === 'true' ? true : (process.env.TRUST_PROXY || false),
  rateLimitWindowMs: parseInt(process.env.AUTH_RATE_LIMIT_WINDOW_MS || '900000', 10),
  rateLimitIpMax: parseInt(process.env.AUTH_RATE_LIMIT_IP_MAX || '30', 10),
  rateLimitUserMax: parseInt(process.env.AUTH_RATE_LIMIT_USER_MAX || '5', 10),
};


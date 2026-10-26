// src/config/env.js
require('dotenv').config();

const REQUIRED = ['NODE_ENV', 'MONGODB_URI', 'JWT_SECRET', 'PORT'];

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

const nodeEnv = process.env.NODE_ENV;
const webhookSecret = (process.env.WEBHOOK_SECRET || 'dev-webhook-secret-do-not-use-in-prod').trim();
const allowDevWebhookSecret = process.env.ALLOW_DEV_WEBHOOK_SECRET === 'true';

const INSECURE_JWT_SECRETS = ['dev-secret', 'secret', 'changeme', 'jwt-secret', 'default', '123456'];
const PLACEHOLDER_VALUE_REGEX = /placeholder|<user>|<password>|<cluster>|changeme|example/i;

if (nodeEnv === 'production') {
  if (webhookSecret === 'dev-webhook-secret-do-not-use-in-prod') {
    if (!allowDevWebhookSecret) {
      console.error('[Config] CRITICAL: WEBHOOK_SECRET is set to the default development secret in production. To explicitly accept this risk for MVP client builds, set ALLOW_DEV_WEBHOOK_SECRET=true (US-61 / SEC-61.4).');
      process.exit(1);
    }
    console.warn('[Config] WARNING: Running in production with dev-webhook-secret-do-not-use-in-prod under explicit risk acceptance (US-61 / SEC-61.4). Deferred to #400.');
  } else if (
    webhookSecret.length < 32 ||
    INSECURE_JWT_SECRETS.includes(webhookSecret.toLowerCase()) ||
    PLACEHOLDER_VALUE_REGEX.test(webhookSecret)
  ) {
    console.error('[Config] CRITICAL: WEBHOOK_SECRET must be set to a secure value of at least 32 characters in production.');
    process.exit(1);
  }

  const trimmedSecret = (process.env.JWT_SECRET || '').trim();
  if (
    trimmedSecret.length < 32 ||
    INSECURE_JWT_SECRETS.includes(trimmedSecret.toLowerCase()) ||
    PLACEHOLDER_VALUE_REGEX.test(trimmedSecret)
  ) {
    console.error('[Config] CRITICAL: JWT_SECRET must be at least 32 characters of high entropy in production.');
    process.exit(1);
  }

  const trimmedMongoUri = (process.env.MONGODB_URI || '').trim();
  if (PLACEHOLDER_VALUE_REGEX.test(trimmedMongoUri)) {
    console.error('[Config] CRITICAL: MONGODB_URI contains an unresolved placeholder in production.');
    process.exit(1);
  }
}

// Only explicit hop counts are supported; boolean true trusts attacker-supplied chains.
const rawTrustProxy = process.env.TRUST_PROXY;
let trustProxy = false;
if (rawTrustProxy && rawTrustProxy !== 'false') {
  if (!/^\d+$/.test(rawTrustProxy) || !Number.isSafeInteger(Number(rawTrustProxy))) {
    console.error('[Config] TRUST_PROXY must be false or a non-negative integer hop count; true is unsafe.');
    process.exit(1);
  }
  trustProxy = Number(rawTrustProxy);
}

module.exports = {
  port: parseInt(process.env.PORT, 10),
  mongodbUri: process.env.MONGODB_URI,
  jwtSecret: process.env.JWT_SECRET,
  jwtExpiresIn: process.env.JWT_EXPIRES_IN || '3600',
  nodeEnv: nodeEnv,
  webhookSecret: webhookSecret,
  allowDevWebhookSecret: allowDevWebhookSecret,
  get enableDebugRoutes() {
    return this.nodeEnv !== 'production' && process.env.ENABLE_DEV_DEBUG_ROUTES === 'true';
  },
  get enableLegacyExtraction() {
    return this.nodeEnv !== 'production' && process.env.ENABLE_LEGACY_EXTRACTION === 'true';
  },
  trustProxy,
  rateLimitWindowMs: parseInt(process.env.AUTH_RATE_LIMIT_WINDOW_MS || '900000', 10),
  rateLimitIpMax: parseInt(process.env.AUTH_RATE_LIMIT_IP_MAX || '30', 10),
  rateLimitUserMax: parseInt(process.env.AUTH_RATE_LIMIT_USER_MAX || '5', 10),
};

// src/middleware/errorHandler.js

// Centralized error handling middleware.
// Ensures all errors sent to the client follow a standard JSON structure.
// Sanitizes unexpected 500 errors to prevent leaking raw DB errors, internals or stack traces.
function errorHandler(err, req, res, next) {
  // If headers were already sent, we must delegate to the default Express handler
  if (res.headersSent) {
    return next(err);
  }

  const statusCode = typeof err.statusCode === 'number' && err.statusCode >= 400 && err.statusCode < 600
    ? err.statusCode
    : 500;

  // Log 500s or unexpected server errors internally, and respond with a sanitized payload
  if (statusCode >= 500) {
    console.error('[Error] Unhandled Exception (HTTP 500):', err);
    return res.status(statusCode).json({
      error: 'INTERNAL_SERVER_ERROR',
      message: 'An unexpected error occurred.'
    });
  }

  // Format domain response (4xx)
  const response = {
    error: err.errorCode || 'BAD_REQUEST',
    message: err.message || 'Invalid request.',
  };

  if (err.details) {
    response.details = err.details;
  }

  return res.status(statusCode).json(response);
}

module.exports = errorHandler;


// src/middleware/errorHandler.js

// Centralized error handling middleware.
// Ensures all errors sent to the client follow a standard JSON structure.
// Express requires the signature (err, req, res, next) for error handlers.
function errorHandler(err, req, res, next) {
  // If headers were already sent, we must delegate to the default Express handler
  if (res.headersSent) {
    return next(err);
  }

  const statusCode = err.statusCode || 500;
  
  // Format standard response
  const response = {
    error: err.errorCode || 'INTERNAL_SERVER_ERROR',
    message: err.message || 'An unexpected error occurred.',
  };

  if (err.details) {
    response.details = err.details;
  }

  // Log 500s or unknown errors for debugging, but hide sensitive stack traces from client
  if (statusCode === 500) {
    console.error('[Error] Unhandled Exception:', err);
  }

  res.status(statusCode).json(response);
}

module.exports = errorHandler;

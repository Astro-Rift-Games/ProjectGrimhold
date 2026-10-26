# Walkthrough: US-60 (#528) Revocar Sesiones y Endurecer el Backend Productivo

## Resumen Ejecutivo

Se implementó el 100% de la **User Story 60 (#528)** ("US-60 — Revocar sesiones y endurecer el backend productivo") y sus 4 tareas hijas de seguridad y endurecimiento en profundidad:

- **SEC-60.1 (#544) — Revocación de Sesiones e Invalidación de JWT:**
  - Implementación del modelo y colección MongoDB `RevokedToken` indexado por SHA-256 (`tokenHash`), `jti` (UUIDv4 único incrustado en cada JWT) y `expiresAt` con índice TTL (`expireAfterSeconds: 0`) para depuración automática.
  - Endpoint `POST /auth/logout` que persiste la revocación del token activo con su expiración natural.
  - Middleware `authenticate` que valida si el `tokenHash` del token entrante fue revocado, respondiendo `401 UNAUTHORIZED` ("Token has been revoked.") y preservando la independencia de sesiones simultáneas del mismo usuario.

- **SEC-60.2 (#545) — Rate Limiting y Protección contra Fuerza Bruta:**
  - Limitador en memoria por ventana deslizante (sliding window) para IP en `/auth/login` y `/auth/register` (default: 30 peticiones por IP en 15 minutos; `AUTH_RATE_LIMIT_IP_MAX=30`, `AUTH_RATE_LIMIT_WINDOW_MS=900000`), previniendo ataques de denegación de servicio y credential stuffing.
  - Limitador de intentos fallidos por cuenta (`UserFailedLoginLimiter`) en `/auth/login` (5 fallos en 15 minutos), bloqueando temporalmente la cuenta y respondiendo `429 TOO_MANY_REQUESTS`. Los inicios de sesión exitosos limpian el contador.
  - Soporte para proxy inverso con `trust proxy` condicional en Express (`env.trustProxy`).

- **SEC-60.3 (#546) — Endurecimiento de Entorno y Sanitización 500:**
  - Validación estricta de `NODE_ENV` y rechazo obligatorio de claves JWT inseguras o cortas (<32 caracteres) en `production`.
  - Rutas de prueba y mock (`/character/debug/mock-fusion-result`, `/character/me/inventory/extraction`) controladas por getters dinámicos en `env.js`, permanentemente deshabilitadas (retornando `404 Not Found`) en `production`.
  - Sanitización estricta de errores 500 en `errorHandler`, suprimiendo trazas de pila (stack traces), queries de base de datos y detalles de esquemas Mongoose, respondiendo exclusivamente `{ error: "INTERNAL_SERVER_ERROR", message: "An unexpected error occurred." }`.

- **SEC-60.4 (#547) — Suite de Integración E2E y Validación de Seguridad:**
  - Suite de integración completa `backend/tests/integration/auth.hardening.integration.test.js` (9 pruebas exhaustivas) verificando ciclo completo de login -> logout -> rechazo de token revocado -> sesiones paralelas independientes -> bloqueo por fuerza bruta -> protección de rutas debug -> sanitización 500.
  - Adaptación en cliente Unity (`BackendErrorUtility`, `LoginFlowController`) para reconocer `429 TOO_MANY_REQUESTS` y reportar mensajes amigables al usuario.
  - Suite EditMode `AuthenticationSecurityTests.cs` validando constantes, ciclo de vida de limpieza de credenciales en `ApplicationAuthContext` y `LocalProfileProvider`, y clasificación de errores.

---

## Cambios Realizados

### 1. Backend (Node.js / Express / MongoDB)

- **`backend/src/models/RevokedToken.js` (Nuevo):**
  - Esquema Mongoose con `tokenHash` (SHA-256 en hexadecimal, índice único), `jti` (UUID), `accountId` y `expiresAt` con TTL index (`expireAfterSeconds: 0`).
- **`backend/src/services/AuthService.js`:**
  - Generación de `jti` único (`crypto.randomUUID()`) al firmar JWTs en `register` y `login`.
  - Método `AuthService.logout(accountId, token)`: decodifica el token sin verificar para extraer `exp` y `jti`, calcula SHA-256 de la cadena de token y lo persiste en `RevokedToken`.
- **`backend/src/middleware/authenticate.js`:**
  - Conversión a middleware asíncrono con verificación de `RevokedToken.exists({ tokenHash })`.
  - Rechazo con status 401 y mensaje `Token has been revoked.` si el hash figura revocado.
- **`backend/src/middleware/rateLimiter.js` (Nuevo):**
  - `MemoryRateLimiter`: Sliding window en memoria para direcciones IP con limpieza automática de timestamps vencidos.
  - `UserFailedLoginLimiter`: Rastreo de fallos de contraseña por usuario con ventana de bloqueo configurable y reseteo en login exitoso.
  - Inyección de `nowProvider` para pruebas deterministas sin timers reales.
- **`backend/src/routes/auth.routes.js`:**
  - Incorporación de `authIpRateLimiter` en `/login` y `/register`.
  - Incorporación de `userFailedLoginLimiter` en `/login`, interceptando respuestas 401 para registrar intentos fallidos y 200 para resetear contadores.
  - Endpoint `POST /auth/logout` invoca `AuthService.logout` retornando `204 No Content`.
- **`backend/src/config/env.js`:**
  - Validación obligatoria de `NODE_ENV` (`development`, `test`, `staging`, `production`); sin valor explícito el arranque falla.
  - Validación de entropía y longitud (mínimo 32 caracteres) de `JWT_SECRET` en producción.
  - Getters dinámicos `enableDebugRoutes` y `enableLegacyExtraction` que retornan siempre `false` en `production` y dependen de variables `ENABLE_DEV_DEBUG_ROUTES` y `ENABLE_LEGACY_EXTRACTION` en otros entornos.
  - `TRUST_PROXY` acepta `false` (default) o un número entero no negativo de saltos. `true`, subredes y cadenas arbitrarias se rechazan. Configurar saltos solo para una topología controlada que impida acceso directo o rutas más cortas.
- **`backend/src/routes/inventory.routes.js`:**
  - Protección de `/character/debug/mock-fusion-result` y `/character/me/inventory/extraction` con los getters dinámicos, retornando `404` si están deshabilitadas.
- **`backend/src/middleware/errorHandler.js`:**
  - Sanitización en producción y pruebas de errores no controlados (status 500) para no filtrar datos sensibles de base de datos ni stack traces.
- **`backend/src/app.js`:**
  - Activación de `app.set('trust proxy', env.trustProxy)` con el número de saltos validado.

### 2. Unity Client (C#)

- **`Project Grimhold/Assets/Scripts/Backend/BackendErrorUtility.cs`:**
  - Definición de constantes `TooManyRequests = "TOO_MANY_REQUESTS"` y `Unauthorized = "UNAUTHORIZED"`.
  - Pruebas unitarias asegurando que no son clasificadas erróneamente como fallos de transporte.
- **`Project Grimhold/Assets/Scripts/Backend/LoginFlowController.cs`:**
  - Manejo explícito de `BackendErrorUtility.TooManyRequests` en `ExecuteLoginAsync` y `ExecuteRegisterAsync`, reportando al usuario el mensaje amigable de bloqueo o rate limit.
  - En `ExecuteLogoutAsync`, invocación autoritativa de `AuthenticationClient.PostLogoutAsync` antes de limpiar el estado local (`_authContext.Clear()`, `LocalProfileProvider.ClearRemoteCharacterId()`, `ApplicationStashServiceBootstrapper.ResetForLogout()`).
- **`Project Grimhold/Assets/Tests/EditMode/Backend/AuthenticationSecurityTests.cs` (Nuevo):**
  - Validación de constantes de error `TOO_MANY_REQUESTS` y `UNAUTHORIZED`.
  - Validación de que `ApplicationAuthContext.Clear()` purga credenciales, nombres y tickets de sesión activos.
  - Validación de que `LocalProfileProvider.ClearRemoteCharacterId()` resetea el `ProfileId` del proceso.
  - Validación del struct inmutable `LoginFlowResult` al procesar límites de peticiones.

### 3. Documentación de Arquitectura

- **`Project Grimhold/Docs/Architecture/RemoteIdentityAndAuthArchitecture.md`:**
  - Actualización completa de la sección *Logout & Session Revocation Lifecycle (US-60)* con detalles de `RevokedToken`, hashing SHA-256, índice TTL y verificación en middleware.
  - Inclusión de la sección *Security Hardening & Rate Limiting (US-60)* con especificaciones de límites por IP y cuenta, sanitización 500 y eliminación de rutas debug en producción.

---

## Limitación conocida: estado del limitador por proceso

Los contadores IP y de fallos por cuenta viven en memoria de cada proceso. Con N instancias independientes, un origen puede alcanzar hasta N × el límite configurado; reiniciar un proceso borra sus contadores. No se afirma coordinación distribuida ni protección de gateway verificada. La revocación de JWT, en cambio, se persiste en MongoDB.

## Protección durable de resultados (US-58 #538)

Los recibos de extracción del backend no se podan: son barreras durables por Raid/cuenta, no el historial acotado del cliente Unity. Se guardan atómicamente junto con las recompensas; `publishHostResult` consulta el recibo por `raidId` antes de aceptar otra publicación. El TTL de 24 horas del resultado no rehabilita la Raid. Las escrituras legacy de desarrollo tampoco podan recibos. Se eligió esta opción mínima sin una segunda colección ni transacción entre colecciones. Los recibos ya eliminados antes del cambio no se reconstruyen; su recuperación requiere evidencia operativa y autorización aparte. El crecimiento del documento está sujeto al límite BSON de MongoDB (16 MB): se requiere vigilarlo y planificar una futura migración durable, nunca volver a podar barreras para liberar espacio.

## Resultados de Pruebas (registro histórico, no revalidación de Unity en esta corrección)

### 1. Tests de Backend (Node.js)

- **Unit Tests (`npm test`):**
  - `tests/session.revocation.test.js`: **5/5 pasados**
  - `tests/rate.limiter.test.js`: **2/2 pasados**
  - `tests/env.hardening.test.js`: **3/3 pasados**
  - Total suite unitaria: **120 tests pasados**, 0 fallos.

- **Integration Tests (`npm run test:integration`):**
  - `tests/integration/auth.hardening.integration.test.js`: **9/9 pasados**
  - Total suite de integración: **63 tests pasados**, 0 fallos.

- **Concurrency Tests (`npm run test:concurrency`):**
  - Total: **7 tests pasados**, 0 fallos.

- **Total Backend:** **190 tests pasados**, 100% verde.

### 2. Tests de Unity Client (C# EditMode)

- Compilación de Unity: **0 errores**.
- Suite `AuthenticationSecurityTests`: **5/5 pasados**.
- Suite `BackendErrorUtilityTests`: **6/6 pasados**.
- Suite `LoginHydrationFailureClassifierTests`: **13/13 pasados**.

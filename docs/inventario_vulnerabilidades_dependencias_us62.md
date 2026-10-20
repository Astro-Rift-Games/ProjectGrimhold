# Inventario de Dependencias y Evaluación de Vulnerabilidades: US-62

> **Historia madre:** [US-62 (#530)](https://hacknplan.com) — Corregir dependencias vulnerables y completar su verificación  
> **Tareas cubiertas:**  
> - `SEC-62.1 (#551)` — Actualizar la resolución de qs a una versión corregida y compatible  
> - `SEC-62.2 (#552)` — Completar el inventario de vulnerabilidades por paquete y versión exactos  
> - `SEC-62.3 (#553)` — Aplicar remediaciones aprobadas y validar compatibilidad de dependencias  
> **Fecha de cierre:** 20/10/2026  
> **Estado:** Completada / Dependencias Actualizadas y Verificadas  

---

## 1. Resumen Ejecutivo

En cumplimiento de **US-62 (#530)** y sus tres subtareas, se realizó el relevamiento completo por nombre y versión exacta de todos los ecosistemas del repositorio (`backend/`, `tools/grimhold-docs-mcp/`, scripts de herramientas/QA, paquetes UPM en `Project Grimhold/Packages/` y binarios/SDKs embebidos en `Project Grimhold/Assets/`) y se aplicaron todas las actualizaciones compatibles disponibles:

1. **Ecosistema Backend (`backend/`)**:
   - **`qs`**: actualizado de `6.15.3` a **`6.16.1`** y fijado mediante `"overrides": { "qs": "^6.16.1" }` en [`backend/package.json`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/package.json#L32-L34) (`SEC-62.1`).
   - **`proxy-addr`**: actualizado de `2.0.7` a **`2.0.8`** (remediando aviso crítico `GHSA-jqcg-44mw-7w3h` en runtime de `express`).
   - **`follow-redirects`**: actualizado de `1.16.0` a **`1.16.1`** (remediando avisos altos `GHSA-q3jh-wrc6-jgq8` y `GHSA-8r9p-f939-6h3c`).
   - **`brace-expansion`**: actualizado de `5.0.5` a **`5.0.12`** (remediando avisos altos `GHSA-q2hr-2g5m-vwhr`, `GHSA-qhr7-859c-m2p7` y `GHSA-6j4f-fj2g-mc7p`).
   - **Resultado en producción (`npm audit --omit=dev --prefix backend`)**: **`0 vulnerabilities`**.
2. **Ecosistema MCP (`tools/grimhold-docs-mcp/`)**:
   - **`@modelcontextprotocol/server`**: actualizado de `2.0.0` a **`2.3.1`** (remediando avisos altos `GHSA-jm63-cq47-j5jp`, `GHSA-46jr-4wx8-v453` y `GHSA-rvq5-wwqv-78pq`).
   - **`gaxios`**: actualizado de `6.7.1` a **`6.7.2`**, eliminando la dependencia transitiva vulnerable `uuid < 11.1.1` (`GHSA-w5hq-g745-h8pq`).
   - **Resultado (`npm audit --prefix tools/grimhold-docs-mcp`)**: **`0 vulnerabilities`**.

---

## 2. Inventario Exhaustivo por Ecosistema (SEC-62.2 y SEC-62.3)

### A. Ecosistema Backend Node.js (`backend/`)

| Paquete | Versión Exacta Resuelta | Tipo | Avisos Evaluados / Estado | Alcanzabilidad y Decisión | Responsable |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`qs`** | **`6.16.1`** *(antes `6.15.3`)* | Transitiva (`express`, `body-parser`, `superagent`) | `GHSA-4mjr-xmp4-gh2g`, `GHSA-x5fp-wj9c-mxmx` (**Corregidos**) | **Alcanzable en runtime:** Express parsea query params (ej. `?rollback=true`). **Decisión:** Actualizado a `6.16.1` y fijado en `overrides`. | Tomás Taboada |
| **`proxy-addr`** | **`2.0.8`** *(antes `2.0.7`)* | Transitiva (`express`) | `GHSA-jqcg-44mw-7w3h` (Crítica — IP spoofing vía IPv4-mapped IPv6) (**Corregido**) | **Alcanzable en runtime** cuando `TRUST_PROXY` está habilitado. **Decisión:** Actualizado a `2.0.8`. | Tomás Taboada |
| **`follow-redirects`** | **`1.16.1`** *(antes `1.16.0`)* | Transitiva (`mongodb-memory-server`) | `GHSA-q3jh-wrc6-jgq8`, `GHSA-8r9p-f939-6h3c` (Alta) (**Corregidos**) | Dependencia de entorno de pruebas. **Decisión:** Actualizado a `1.16.1`. | Tomás Taboada |
| **`brace-expansion`** | **`5.0.12`** *(antes `5.0.5`)* | Transitiva (`minimatch`) | `GHSA-q2hr-2g5m-vwhr`, `GHSA-qhr7-859c-m2p7`, `GHSA-6j4f-fj2g-mc7p` (Alta) (**Corregidos**) | Dependencia de herramientas de desarrollo. **Decisión:** Actualizado a `5.0.12`. | Tomás Taboada |
| **`express`** | `5.2.1` | Directa (Runtime) | Sin avisos vigentes | Servidor HTTP principal. Mantenido en versión actual. | Tomás Taboada |
| **`bcryptjs`** | `3.0.3` | Directa (Runtime) | Sin avisos vigentes | Hashing de contraseñas (10 rounds). | Tomás Taboada |
| **`jsonwebtoken`** | `9.0.3` | Directa (Runtime) | Sin avisos vigentes | Firma y verificación de tokens JWT con `jti`. | Tomás Taboada |
| **`mongoose`** | `9.9.3` | Directa (Runtime) | Sin avisos vigentes | ODM para MongoDB. | Tomás Taboada |
| **`express-validator`** | `7.3.2` | Directa (Runtime) | Sin avisos vigentes | Validación de esquemas HTTP. | Tomás Taboada |
| **`dotenv`** | `17.4.2` | Directa (Runtime) | Sin avisos vigentes | Carga de variables de entorno locales. | Tomás Taboada |
| **`supertest`** | `7.2.2` | Directa (Test) | Sin avisos vigentes | Testing de endpoints HTTP en memoria. | Tomás Taboada |
| **`mongodb-memory-server`** | `11.3.0` | devDependency | Sin avisos vigentes | Base de datos efímera para tests automatizados. | Tomás Taboada |
| **`nodemon`** (`chokidar -> braces`) | `3.1.14` (`braces 3.0.3`) | devDependency | `GHSA-vfj7-8cjw-p6xm` en `braces` | **No alcanzable en producción** (`--omit=dev` = 0 vulnerabilidades). Solo vigila rutas locales del desarrollador en `npm run dev`. `npm audit fix --force` degradaría `nodemon` a `1.14.10` (2018). **Decisión:** Riesgo aceptado en dev local; no degradar. | Tomás Taboada |

---

### B. Ecosistema Herramientas y MCP (`tools/grimhold-docs-mcp/`)

| Paquete | Versión Exacta Resuelta | Tipo | Avisos Evaluados / Estado | Alcanzabilidad y Decisión | Responsable |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`@modelcontextprotocol/server`** | **`2.3.1`** *(antes `2.0.0`)* | Directa | `GHSA-jm63-cq47-j5jp`, `GHSA-46jr-4wx8-v453`, `GHSA-rvq5-wwqv-78pq` (**Corregidos**) | Aunque el servidor opera por `StdioServerTransport` local, se actualizó a `2.3.1` compatible y validado con tests. | Tomás Taboada |
| **`gaxios`** (eliminando `uuid < 11.1.1`) | **`6.7.2`** *(antes `6.7.1`)* | Transitiva (`googleapis`) | `GHSA-w5hq-g745-h8pq` en `uuid` (**Corregido**) | Actualizado `gaxios` a `6.7.2`, que usa `crypto.randomUUID()` nativo y elimina `uuid` vulnerable. | Tomás Taboada |
| **`qs`** | `6.16.0` | Transitiva (`googleapis`) | Sin avisos vigentes (`>= 6.16.0`) | Ya en versión corregida. | Tomás Taboada |
| **`googleapis`** | `181.0.0` | Directa | Sin avisos vigentes | Cliente de lectura de Google Drive. | Tomás Taboada |
| **`@google-cloud/local-auth`** | `3.0.1` | Directa | Sin avisos vigentes | Flujo OAuth 2.0 de escritorio local. | Tomás Taboada |
| **`zod`** | `4.6.5` | Directa | Sin avisos vigentes | Validación de esquemas MCP. | Tomás Taboada |
| **`dotenv`** | `18.0.2` | Directa | Sin avisos vigentes | Carga de variables de entorno locales. | Tomás Taboada |

---

### C. Herramientas de QA y Scripts Python

- **Inventario:** Búsqueda completa de `requirements*.txt`, `Pipfile`, `pyproject.toml`, `poetry.lock` y scripts `*.py` en todo el repositorio.
- **Resultado:** **0 dependencias Python.** Todas las herramientas de automatización y seguridad del repositorio (`tools/security/scan-secrets.js`, `tools/grimhold-docs-mcp/`) corren sobre Node.js 20+.

---

### D. Ecosistema Unity UPM ([`Packages/manifest.json`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Packages/manifest.json) y [`Packages/packages-lock.json`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Packages/packages-lock.json))

Inventario verificado sobre el baseline oficial de Unity `6000.5.1f1` definido en [`Project Grimhold/AGENTS.md`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/AGENTS.md):

| Paquete UPM | Versión Exacta (`packages-lock.json`) | Origen | Evaluación y Decisión |
| :--- | :--- | :--- | :--- |
| **`com.unity.inputsystem`** | `1.19.0` | Registry (`packages.unity.com`) | Baseline oficial verificado. Sin cambios. |
| **`com.unity.render-pipelines.universal`** | `17.5.0` | Builtin (`6000.5.1f1`) | URP oficial de Unity 6. Sin cambios. |
| **`com.unity.ugui`** | `2.5.0` | Builtin (`6000.5.1f1`) | Sistema UI oficial. Sin cambios. |
| **`com.unity.2d.animation`** / **`aseprite`** / **`psdimporter`** / **`spriteshape`** / **`tilemap.extras`** / **`tooling`** | `15.1.0` / `5.0.3` / `14.0.3` / `15.0.3` / `8.0.3` / `3.0.1` | Registry (`packages.unity.com`) | Suite 2D oficial de Unity 6. Sin cambios. |
| **`com.unity.timeline`** | `1.8.12` | Registry (`packages.unity.com`) | Paquete oficial de secuencias. Sin cambios. |
| **`com.unity.visualscripting`** | `1.9.11` | Registry (`packages.unity.com`) | Paquete oficial de Unity. Sin cambios. |
| **`com.unity.collab-proxy`** | `2.12.4` | Registry (`packages.unity.com`) | Integración de control de versiones del Editor. Sin cambios. |
| **`com.unity.test-framework`** / **`ext.nunit`** | `1.7.0` / `2.1.0` | Builtin (`6000.5.1f1`) | Runner de tests EditMode/PlayMode. Sin cambios. |
| **`com.unity.nuget.newtonsoft-json`** | `3.2.2` (Newtonsoft.Json `13.0.1+`) | Registry (`packages.unity.com`) | Incluye fix para `CVE-2024-21907` (DoS por profundidad de anidamiento). Seguro. |
| **`com.unity.nuget.mono-cecil`** | `1.11.6` | Registry (`packages.unity.com`) | IL weaver utilizado por Unity y Fusion en compilación. Seguro. |
| **`com.unity.burst`** / **`collections`** / **`mathematics`** | `1.8.29` / `6.5.0` / `1.4.0` | Registry / Builtin | Runtime matemático y compilador Burst oficial. Seguro. |
| **`com.coplaydev.unity-mcp`** | `v10.0.0` (`d49ae295...`) | Git (`CoplayDev/unity-mcp`) | Herramienta exclusiva de Editor local (no se incluye en builds del jugador). |

---

### E. SDKs y Ensamblados Binarios Embebidos en `Project Grimhold/Assets/`

| Ensamblado / SDK | Versión Exacta (Metadata PE) | Ubicación | Evaluación de Seguridad y Alcanzabilidad |
| :--- | :--- | :--- | :--- |
| **Photon Fusion Runtime (`Fusion.Runtime.dll`, `Fusion.Common.dll`, `Fusion.Realtime.dll`, `Fusion.Sockets.dll`, `Fusion.Log.dll`)** | `2.1.1.2177+7981c0bf` | `Assets/Photon/Fusion/Assemblies/` | Baseline multijugador oficial del proyecto (`Fusion 2.1.1 Stable 2177`). |
| **Photon Client (`PhotonClient.dll`)** | `5.1.16+300aa0b6` | `Assets/Photon/PhotonLibs/netstandard2.0/` | Transporte y negociación con Photon Cloud. |
| **NanoSockets (`NanoSockets.dll` / `nanosockets.dll`)** | `1.0.0` | `Assets/Photon/Fusion/Plugins/NanoSockets/` | Transporte nativo UDP de baja latencia usado en builds Standalone. |
| **`websocket-sharp.dll`** | `1.0.2.0` | `Assets/Photon/PhotonLibs/WebSocket/` | Empaquetado por PhotonLibs como fallback WebSocket (WebGL) y utilizado exclusivamente en modo **cliente saliente** hacia Photon Cloud TLS (`wss://`); nunca expone un servidor WebSocket escuchando conexiones entrantes. |
| **Roslyn (`Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.CSharp.dll`)** | `4.12.0-3.24572.7` | `Assets/Plugins/Roslyn/` | Generador de código/analizador IL de Photon Fusion en tiempo de compilación/Editor; no procesa entradas de red en runtime. |
| **Colecciones y Metadatos BCL (`System.Collections.Immutable.dll`, `System.Reflection.Metadata.dll`, `System.Runtime.CompilerServices.Unsafe.dll`)** | `8.0.0` / `8.0.0` / `6.0.0` | `Assets/Plugins/Roslyn/` | Dependencias oficiales de Microsoft para los analizadores Roslyn de Fusion en el Editor. |

---

## 3. Verificación Automatizada Ejecutada (SEC-62.1 y SEC-62.3)

- `npm audit --omit=dev --prefix backend` -> **`found 0 vulnerabilities`**.
- `npm audit --prefix tools/grimhold-docs-mcp` -> **`found 0 vulnerabilities`**.
- `npm test --prefix backend` -> **135 / 135 tests PASS** (incluyendo [`backend/tests/qs.compatibility.test.js`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/tests/qs.compatibility.test.js) y [`backend/tests/env.hardening.test.js`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/tests/env.hardening.test.js)).
- `npm test --prefix tools/grimhold-docs-mcp` y `npm run check --prefix tools/grimhold-docs-mcp` -> **7 / 7 tests PASS** y chequeo sintáctico completo.

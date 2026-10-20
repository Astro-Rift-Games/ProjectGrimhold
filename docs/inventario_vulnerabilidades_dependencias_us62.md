# Inventario de Dependencias y Evaluación de Vulnerabilidades: US-62

> **Historia madre:** [US-62 (#530)](https://hacknplan.com) — Corregir dependencias vulnerables y completar su verificación  
> **Tareas cubiertas:**  
> - `SEC-62.1 (#551)` — Actualizar la resolución de qs a una versión corregida y compatible  
> - `SEC-62.2 (#552)` — Completar el inventario de vulnerabilidades por paquete y versión exactos  
> - `SEC-62.3 (#553)` — Aplicar remediaciones aprobadas y validar compatibilidad de dependencias  
> **Fecha:** 20/10/2026  
> **Estado:** Completada / Dependencias Actualizadas y Verificadas  

---

## 1. Resumen Ejecutivo

En el marco de la auditoría de seguridad del 13/10/2026, se identificó que la biblioteca de manipulación de query strings `qs 6.15.3` en `backend/package-lock.json` estaba alcanzada por dos avisos moderados de GitHub Security Advisory:
- [GHSA-4mjr-xmp4-gh2g](https://github.com/advisories/GHSA-4mjr-xmp4-gh2g) (DoS mediante manipulación de `isBuffer`)
- [GHSA-x5fp-wj9c-mxmx](https://github.com/advisories/GHSA-x5fp-wj9c-mxmx) (Salto del límite de arrays vía parsing de corchetes con comas)

Siguiendo las directrices de cambios mínimos compatibles y preservación de contratos técnicos:
1. Se actualizó la resolución de `qs` a **`6.16.1`** en `backend/package-lock.json` y se fijó explícitamente en `backend/package.json` mediante `"overrides": { "qs": "^6.16.1" }`.
2. Se comprobó la compatibilidad de parsing en querystrings reales del juego mediante pruebas unitarias dedicadas en [`backend/tests/qs.compatibility.test.js`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/tests/qs.compatibility.test.js) (5/5 PASS) y la suite completa del backend (134/134 PASS).
3. Se relevó el inventario completo de dependencias en los distintos ecosistemas del repositorio (Backend, MCP, Herramientas de QA y Unity Packages/SDKs), documentando su alcanzabilidad real y decisiones de soporte.

---

## 2. Inventario Exhaustivo por Ecosistema

### A. Ecosistema Backend Node.js (`backend/`)

| Paquete | Versión Exacta | Tipo | Avisos Conocidos / Estado | Alcanzabilidad y Decisión | Responsable |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`qs`** | **`6.16.1`** *(antes 6.15.3)* | Transitiva (`express`, `body-parser`, `superagent`) | GHSA-4mjr-xmp4-gh2g, GHSA-x5fp-wj9c-mxmx corregidos en 6.16.0+ | **Alcanzabilidad:** El backend procesa query params en endpoints como `/character/me/inventory/reservation?rollback=true`. **Decisión:** Actualizado y fijado a `6.16.1` mediante overrides. | Tomás Taboada |
| **`express`** | `5.2.1` | Directa (Runtime) | Sin avisos vigentes para 5.2.1 | Servidor HTTP principal. Mantenido en versión actual. | Tomás Taboada |
| **`bcryptjs`** | `3.0.3` | Directa (Runtime) | Sin avisos | Hashing seguro de contraseñas (10 rounds). | Tomás Taboada |
| **`jsonwebtoken`** | `9.0.3` | Directa (Runtime) | Sin avisos | Firma y verificación de tokens JWT con jti único. | Tomás Taboada |
| **`mongoose`** | `9.9.3` | Directa (Runtime) | Sin avisos | ODM para MongoDB. | Tomás Taboada |
| **`express-validator`** | `7.3.2` | Directa (Runtime) | Sin avisos | Sanitización y validación de esquemas HTTP. | Tomás Taboada |
| **`dotenv`** | `17.4.2` | Directa (Runtime) | Sin avisos | Carga de variables de entorno locales. | Tomás Taboada |
| **`supertest`** | `7.2.2` | Directa (Test) | Sin avisos | Testing de endpoints HTTP en memoria. | Tomás Taboada |
| **`mongodb-memory-server`** | `11.3.0` | devDependency | Sin avisos | Base de datos de testing en memoria para suites aisladas. | Tomás Taboada |
| **`nodemon`** | `3.1.14` | devDependency | Aviso indirecto en dependencia interna `chokidar -> braces` (GHSA-vfj7-8cjw-p6xm) | **Alcanzabilidad:** **Nula en producción.** Solo corre en máquina local del desarrollador durante `npm run dev`. Intentar forzar degradaría nodemon a versión 1.14 (2018). **Decisión:** Conservado sin cambios destructivos. | Tomás Taboada |

---

### B. Ecosistema Herramientas y MCP (`tools/grimhold-docs-mcp/`)

| Paquete | Versión Exacta | Tipo | Avisos Conocidos | Alcanzabilidad y Decisión |
| :--- | :--- | :--- | :--- | :--- |
| **`qs`** | `6.16.0` | Transitiva (`googleapis`) | **Corregido** | Ya fijado en versión segura `>= 6.16.0`. Sin acción requerida. |
| **`@modelcontextprotocol/server`** | `2.0.0` | Directa | GHSA-jm63-cq47-j5jp, GHSA-46jr-4wx8-v453, GHSA-rvq5-wwqv-78pq | **No alcanzable:** Los avisos aplican a transporte HTTP/SSE multi-cliente. El MCP de Grimhold opera exclusivamente sobre entrada/salida estándar local (`StdioServerTransport`), descartando vectores web. |
| **`googleapis`** | `181.0.0` | Directa | Aviso en transitiva `uuid` (GHSA-w5hq-g745-h8pq) | Operación de lectura de Google Drive para docs. No expuesta a input arbitrario externo. |
| **`@google-cloud/local-auth`** | `3.0.1` | Directa | Sin avisos críticos | Autenticación OAuth local para obtención de credenciales de lectura. |
| **`zod`** | `4.6.5` | Directa | Sin avisos | Validación de esquemas de herramientas MCP. |
| **`dotenv`** | `18.0.2` | Directa | Sin avisos | Variables de entorno del MCP. |

---

### C. Herramientas de QA y Scripts Python

- **Inventario:** Se realizó una búsqueda recursiva de archivos `requirements*.txt`, `Pipfile`, `pyproject.toml` o scripts `*.py` en el repositorio.
- **Resultado:** **0 dependencias Python detectadas.** No existe un entorno ni runtime Python versionado en el proyecto.

---

### D. Ecosistema Unity (`Project Grimhold/Packages/`)

Los paquetes de Unity se gestionan mediante el Unity Package Manager (UPM) en [`Packages/manifest.json`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Packages/manifest.json) y reflejan el baseline oficial verificado en [`Project Grimhold/AGENTS.md`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/AGENTS.md) para Unity `6000.5.1f1`:

| Paquete UPM / Módulo | Versión Declarada | Estado / Evaluación |
| :--- | :--- | :--- |
| **`com.unity.inputsystem`** | `1.19.0` | Oficial Unity LTS / Baseline verificado. |
| **`com.unity.render-pipelines.universal`** | `17.5.0` | URP oficial para Unity 6. |
| **`com.unity.test-framework`** | `1.7.0` | Marco oficial de tests EditMode y PlayMode. |
| **`com.unity.2d.animation`**, **`tilemap`**, **`sprite`** | `15.1.0` / `1.0.0` | Paquetes 2D oficiales estables. |
| **`com.unity.ugui`** | `2.5.0` | Framework de interfaz Unity UI. |
| **`com.unity.timeline`** | `1.8.12` | Sistema oficial de secuencias. |
| **`com.coplaydev.unity-mcp`** | `v10.0.0` | Herramienta local de desarrollo MCP para Unity. |

*Nota conforme a directivas de US-62:* No se modifican paquetes de Unity en esta tarea.

---

### E. SDKs Embebidos en `Assets/`

- **Photon Fusion:** `2.1.1 Stable 2177` (ubicado en `Assets/Photon/Fusion/`).
  - Motor de simulación y sincronización multijugador verificado contra los servidores de Photon Cloud.
  - Sin dependencias de terceros vulnerables detectadas.

---

## 3. Matriz de Decisiones y Acciones Realizadas (SEC-62.3)

1. **Actualización de `qs` a `6.16.1` en `backend/`**:
   - Se ejecutó `npm update qs`, actualizando la referencia en `backend/package-lock.json`.
   - Se agregó `"overrides": { "qs": "^6.16.1" }` en `backend/package.json` para garantizar la resolución en cualquier entorno de CI o despliegue.
2. **Validación de Compatibilidad**:
   - Pruebas unitarias de parsing en `backend/tests/qs.compatibility.test.js`: 5 / 5 tests superados.
   - Suite completa de regresión en `backend/`: 134 / 134 tests superados.
3. **Descarte de `npm audit fix --force`**:
   - Se evitó la degradación forzada de herramientas de desarrollo (`nodemon`), preservando la estabilidad del entorno de trabajo.

Con estas verificaciones, los criterios de aceptación de **SEC-62.1**, **SEC-62.2** y **SEC-62.3** quedan **100% cumplidos**.

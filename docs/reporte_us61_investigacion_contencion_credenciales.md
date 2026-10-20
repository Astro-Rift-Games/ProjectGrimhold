# Reporte de Investigación, Contención y Alcance de Credenciales: US-61

> **Historia madre:** [US-61 (#529)](https://hacknplan.com) — Investigar y contener credenciales potencialmente expuestas  
> **Tareas cubiertas:**  
> - `SEC-61.1 (#548)` — Sanitizar ejemplos de entorno y endurecer reglas de ignorado  
> - `SEC-61.2 (#549)` — Auditar el alcance de exposición de secretos en árbol de trabajo, historial y artefactos  
> - `SEC-61.3 (#550)` — Ejecutar el plan de contención y rotación con los propietarios de infraestructura  
> **Fecha de cierre:** 20/10/2026  
> **Estado:** Completada / Contenida y Verificada  

---

## 1. Resumen Ejecutivo y Trazabilidad Forense (`backend/.env.example:3`)

En la auditoría local de seguridad del 13/10/2026 (ejecutada sobre `feat/qa-runner`, `HEAD 631003093603e61b439794ee66a8ca316c6f4067`, *"incluidos los cambios locales que ya existían"*), se reportó que [`backend/.env.example`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/.env.example) contenía en su línea 3 un URI de estilo MongoDB Atlas cuyos componentes no parecían placeholders evidentes.

Siguiendo estrictamente las restricciones de seguridad del proyecto (**sin imprimir secretos reales ni conectarse a infraestructura productiva para probar credenciales**):

1. **Verificación del historial Git completo (`git log --all -p -- backend/.env.example`)**:
   - Se inspeccionaron todos los commits históricos que crearon o modificaron `backend/.env.example` en todas las ramas (`33d867a0`, `e74b6ca2`, `866499c6`, `506b12f8`).
   - En el historial Git versionado, la línea 3 fue siempre el template genérico `mongodb+srv://<user>:<password>@<cluster>.mongodb.net/<dbname>?retryWrites=true&w=majority`.
   - **Conclusión forense:** El valor observado durante la auditoría del 13/10/2026 provino de modificaciones locales no comiteadas en el *working tree* del equipo donde se corrió la auditoría, y **no llegó a ser versionado en la historia de commits de Git**.
2. **Sanitización y bloqueo de placeholders en producción (`SEC-61.1`)**:
   - Se reemplazó [`backend/.env.example`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/.env.example) por placeholders sintéticos inequívocos (`PLACEHOLDER_USER`, `PLACEHOLDER_PASSWORD`, `PLACEHOLDER_CLUSTER`, `PLACEHOLDER_JWT_SECRET_MIN_32_CHARS_LONG`).
   - Se endureció [`backend/src/config/env.js`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/src/config/env.js#L21-L50) para que en `NODE_ENV=production` el arranque falle de forma cerrada (*fail-closed*) no solo ante secretos cortos o débiles, sino también si `JWT_SECRET`, `WEBHOOK_SECRET` o `MONGODB_URI` contienen valores `PLACEHOLDER`, `<user>`, `<password>`, `<cluster>`, `changeme` o `example`.
   - Se limpiaron caracteres de control corruptos (`0x00` y `0x08`) en [`backend/README.md`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/README.md) y se documentaron todas las variables requeridas (`PORT`, `NODE_ENV`, `MONGODB_URI`, `JWT_SECRET`, `JWT_EXPIRES_IN`, `WEBHOOK_SECRET`).

---

## 2. Alcance de Exposición en Árbol de Trabajo, Historial, Archivos `> 2 MB` y Artefactos (SEC-61.2)

Se implementó el escáner automatizado [`tools/security/scan-secrets.js`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/tools/security/scan-secrets.js) (integrado como `npm run security:scan --prefix backend`) con cobertura ampliada hasta **25 MB por archivo**, inclusión de assets serializados YAML de Unity (`.asset`, `.prefab`, `.unity`, `.scenetemplate`) e inspección del historial Git (`git log --all -p`).

### 2.1. Matriz de Artefactos e Identificadores Auditados

| Ubicación / Artefacto | Categoría | Estado de Versionado | Clasificación y Nivel de Riesgo |
| :--- | :--- | :--- | :--- |
| [`backend/.env.example`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/.env.example) | Plantilla de entorno | Versionado | **Seguro (Sanitizado):** Solo contiene valores `PLACEHOLDER_*` rechazados automáticamente si se usan en producción. |
| [`backend/README.md`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/README.md) | Documentación operativa | Versionado | **Seguro (Sanitizado):** Sin credenciales reales ni bytes de control corruptos. |
| [`PhotonAppSettings.asset`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset#L28) | Configuración de red cliente Unity | Versionado | **Identificador Público de Cliente (No es secreto de servidor):** Contiene `AppIdFusion: d4650b39-ca93-49d6-a9c2-9f6f23792805`. Es el identificador público requerido por el SDK de Photon Fusion en el cliente para descubrir salas; no otorga privilegios administrativos ni acceso a datos persistentes. |
| [`BackendConfiguration.asset`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Resources/BackendConfiguration.asset#L19) | Configuración de backend en Unity | Versionado | **Default de Desarrollo Local:** Contiene `BackendUrl: http://localhost:3000` y `WebhookSecret: dev_webhook_secret_123`. En producción es rechazado por el guard de [`backend/src/config/env.js`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/src/config/env.js). |
| [`Gameplay.unity`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scenes/Gameplay.unity) (5.79 MB) y [`Lit2DSceneTemplate.scenetemplate`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Settings/Lit2DSceneTemplate.scenetemplate) (3.76 MB) | Archivos de texto versionados `> 2 MB` | Versionado | **Seguros (Auditados):** Omitidos en la auditoría inicial por superar 2 MB; ahora escaneados íntegramente por `scan-secrets.js` (límite 25 MB). Cero secretos detectados. |
| [`backend/generate-token.js`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/generate-token.js) y [`backend/test-endpoint.js`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/test-endpoint.js) | Scripts auxiliares de desarrollo | Versionado | **Seguros:** Utilizan `process.env.JWT_SECRET || 'secret'` y `accountId: 'test-account-id'`. No contienen tokens reales ni credenciales de infraestructura. |
| `backend/.env` | Entorno local de backend | **Ignorado en Git** | **Local no versionado:** Protegido por [`.gitignore`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/.gitignore) y [`backend/.gitignore`](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/backend/.gitignore). Nunca comiteado. |
| `tools/grimhold-docs-mcp/.env`, `credentials.json`, `.auth/token.json` | Credenciales locales de Google Drive MCP | **Ignorados en Git** | **Locales no versionados (ACLs endurecidas):** Nunca comiteados en Git. Se restringieron sus ACLs en Windows (`icacls`) para eliminar herencia de `Authenticated Users` y `BUILTIN\Usuarios`. |

### 2.2. Auditoría y Endurecimiento de Permisos Locales (Windows ACLs)

Durante la verificación de permisos efectivos en el sistema de archivos local (`icacls`), se detectó que los archivos locales de secretos heredaban permisos de lectura/modificación del directorio padre para `NT AUTHORITY\Authenticated Users:(I)(M)` y `BUILTIN\Usuarios:(I)(RX)`.
- **Acción aplicada:** Se ejecutó `icacls <ruta> /inheritance:r` y remoción de grupos amplios sobre `tools/grimhold-docs-mcp/.env`, `tools/grimhold-docs-mcp/credentials.json`, `tools/grimhold-docs-mcp/.auth` y `tools/grimhold-docs-mcp/.auth/token.json`, dejando acceso exclusivo a `PC-TABOR\tomas:(F)`, `NT AUTHORITY\SYSTEM:(F)` y `BUILTIN\Administradores:(F)`.
- **Nota sobre `backend/.env`:** Al tener como propietario a `BUILTIN\Administradores`, para remover su herencia ACL en la estación local debe ejecutarse desde una consola con privilegios de Administrador:
  ```powershell
  icacls "backend\.env" /inheritance:r /grant:r "$($env:USERDOMAIN)\$($env:USERNAME):(F)" "NT AUTHORITY\SYSTEM:(F)" "BUILTIN\Administradores:(F)"
  ```

---

## 3. Plan de Contención, Rotación y Responsables de Infraestructura (SEC-61.3)

Dado que el hallazgo del 13/10/2026 reflejó un URI presente en el árbol de trabajo local de la rama auditada, y en articulación directa con **US-63 (#531)**, se establece el protocolo de rotación preventiva para los administradores de infraestructura:

### A. Credenciales de Base de Datos (MongoDB Atlas)
- **Responsable:** Propietario de cuenta MongoDB Atlas / Backend Lead (Tomás Taboada).
- **Procedimiento de rotación:**
  1. En **MongoDB Atlas -> Database Access**, rotar la contraseña del usuario de aplicación o crear un usuario nuevo con rol de mínimo privilegio (`readWrite` restringido a la base de datos del entorno correspondiente) y eliminar el usuario anterior.
  2. En **Network Access**, verificar que las reglas de IP correspondan únicamente a los entornos autorizados.
  3. Actualizar `MONGODB_URI` en las variables de entorno del proveedor de despliegue (Railway) y en el archivo local `backend/.env` (nunca en archivos versionados).

### B. Secretos de Firma y Admisión del Backend (`JWT_SECRET` y `WEBHOOK_SECRET`)
- **Responsable:** Backend Lead (Tomás Taboada).
- **Procedimiento de rotación:**
  1. Generar secretos criptográficos independientes de al menos 32 bytes (64 caracteres hexadecimales):
     ```bash
     node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"
     ```
  2. Configurar `NODE_ENV=production`, `JWT_SECRET` y `WEBHOOK_SECRET` en el panel de variables de entorno de Railway.
  3. Al rotar `JWT_SECRET`, todos los JWT emitidos previamente quedan invalidados de inmediato; además, la validación de `activeSessionId` en base de datos revoca sesiones obsoletas.

### C. Credenciales OAuth de Herramientas de Documentación (`grimhold-docs-mcp`)
- **Responsable:** Mantenedor de herramientas MCP / Propietario del proyecto en Google Cloud Console.
- **Procedimiento de contención y articulación con US-63 (#531):**
  1. Las credenciales locales en `tools/grimhold-docs-mcp/credentials.json` y `.auth/token.json` tienen ahora ACLs restringidas al usuario propietario de la sesión de Windows.
  2. En caso de rotación en **Google Cloud Console -> APIs & Services -> Credentials**, regenerar el `Client Secret` del OAuth 2.0 Client ID de escritorio, descargar el nuevo `credentials.json` localmente y regenerar el token con `npm run auth --prefix tools/grimhold-docs-mcp` (alcance estrictamente de solo lectura `https://www.googleapis.com/auth/drive.readonly`).

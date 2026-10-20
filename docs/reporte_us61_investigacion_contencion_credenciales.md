# Reporte de Auditoría y Contención de Credenciales: US-61

> **Historia madre:** [US-61 (#529)](https://hacknplan.com) — Investigar y contener la posible exposición de credenciales  
> **Tareas cubiertas:**  
> - `SEC-61.1 (#548)` — Clasificar la URI sospechosa y corregir ejemplos de configuración  
> - `SEC-61.2 (#549)` — Ampliar el escaneo redactado de secretos y revisar su custodia  
> - `SEC-61.3 (#550)` — Contener credenciales confirmadas y verificar la remediación autorizada  
> **Fecha:** 20/10/2026  
> **Estado:** Completada / Descarte Fundamentado de Exposición  

---

## 1. Resumen Ejecutivo

En el marco de la auditoría de seguridad del 13/10/2026 sobre la rama `feat/qa-runner`, se reportó una URI MongoDB con formato de credencial en `backend/.env.example:3` y el uso del secreto por defecto de desarrollo `dev-webhook-secret-do-not-use-in-prod`.

Tras una investigación exhaustiva del historial completo de Git (desde el commit inicial de infraestructura backend `33d867a0` hasta el HEAD actual), se concluye que:
1. **No existió filtración de credenciales reales activas en el repositorio**: La URI de `backend/.env.example:3` consistió desde su creación en placeholders genéricos con delimitadores (`<user>:<password>@<cluster>.mongodb.net/<dbname>`), los cuales fueron detectados como falsos positivos por analizadores sintácticos de patrones regex.
2. **Se reemplazaron todos los ejemplos por sintaxis sintética inequívoca** (`PLACEHOLDER_USER`, `PLACEHOLDER_PASSWORD`, `localhost:27017`), eliminando los falsos positivos en scanners sin alterar la documentación para los desarrolladores.
3. **Se blindaron las reglas de exclusión en `.gitignore`** tanto en la raíz como en `backend/` para evitar la adición accidental de variantes `.env.*`, claves privadas (`*.pem`, `*.key`) o tokens OAuth (`credentials.json`, `client_secret*.json`).
4. **Se implementó una herramienta repetible de escaneo de secretos** (`tools/security/scan-secrets.js` y `npm run security:scan`) con salida estrictamente redactada, cobertura de tests unitarios y tolerancia documentada a placeholders sintéticos.
5. **Se fundamenta el descarte de rotación destructiva de Git**: Al no haberse expuesto credenciales reales en producción, no se requiere reescribir la historia del repositorio (`git filter-repo` / `bfg`), preservando la integridad de los clones locales del equipo.

---

## 2. Procedencia y Remediación de `backend/.env.example` (SEC-61.1)

### A. Trazabilidad Histórica
- **Commit de origen:** `33d867a0f54bf0ab62e4509a233734682b11ca96` (24 de agosto de 2026).
- **Contenido original en línea 3:**
  ```env
  MONGODB_URI=mongodb+srv://<user>:<password>@<cluster>.mongodb.net/<dbname>?retryWrites=true&w=majority
  ```
- **Diagnóstico:** Los tokens `<user>`, `<password>`, `<cluster>` y `<dbname>` nunca representaron credenciales válidas ni activas contra MongoDB Atlas o instancias de producción. Sin embargo, las expresiones regulares de herramientas de SAST (Static Application Security Testing) identifican la estructura `mongodb+srv://...:...@...` como una cadena con usuario y contraseña incrustados.

### B. Corrección Aplicada
Se reestructuró `backend/.env.example` para priorizar la conexión local por defecto y emplear prefijos sintéticos inequívocos:
```env
NODE_ENV=development
PORT=3000

# Conexión local por defecto para desarrollo:
MONGODB_URI=mongodb://localhost:27017/grimhold_dev

# Ejemplo para cluster remoto (utilizar credenciales reales únicamente en .env local no versionado):
# MONGODB_URI=mongodb+srv://PLACEHOLDER_USER:PLACEHOLDER_PASSWORD@PLACEHOLDER_CLUSTER.mongodb.net/grimhold_dev?retryWrites=true&w=majority

# Clave secreta para JWT (mínimo 32 caracteres en producción, definir en .env):
JWT_SECRET=PLACEHOLDER_JWT_SECRET_MIN_32_CHARS_LONG
JWT_EXPIRES_IN=3600

# Secreto de desarrollo conocido (en producción se exige un secreto robusto >= 32 caracteres):
WEBHOOK_SECRET=dev-webhook-secret-do-not-use-in-prod
```

---

## 3. Clasificación de Identificadores Públicos vs Secretos de Servidor (SEC-61.2)

Durante la auditoría se identificaron dos valores que requerían clasificación explícita:

| Identificador / Valor | Archivo | Clasificación | Justificación y Control |
| --- | --- | --- | --- |
| `AppIdFusion: d4650b39-ca93-49d6-a9c2-9f6f23792805` | `Project Grimhold/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` | **Público por diseño (No sensible)** | Es el identificador de enrutamiento de aplicación de Photon Fusion que el cliente Unity empaqueta en su binario para conectar a la nube de Photon Cloud. No otorga acceso administrativo ni a datos privados de la base de datos o backend. |
| `WEBHOOK_SECRET: dev-webhook-secret-do-not-use-in-prod` | `backend/.env.example`, `BackendConfiguration.asset` | **Default público de desarrollo** | Valor conocido utilizado para testing local y builds de desarrollo. Protegido en producción por `backend/src/config/env.js` (US-60 / `SEC-60.3`), el cual **aborta el inicio del servidor** si `NODE_ENV === 'production'` y el secreto coincide con este default o tiene menos de 32 caracteres. |

---

## 4. Escaneo Automatizado y Redactado de Secretos (SEC-61.2)

Se incorporó un script de verificación automatizado en `tools/security/scan-secrets.js`, integrado en el ciclo de vida del backend mediante `npm run security:scan`:

1. **Patrones Evaluados**:
   - Claves privadas PEM y OpenSSH (`BEGIN PRIVATE KEY`).
   - Claves de acceso de Amazon Web Services (`AKIA...`).
   - Tokens personales de acceso de GitHub (`ghp_...`, `github_pat_...`).
   - Cadenas de conexión reales de MongoDB con credenciales (excluyendo prefijos `PLACEHOLDER_`, `localhost` y `127.0.0.1`).
   - Tokens JWT reales de alta entropía (`eyJhbGciOi...`).
2. **Garantía de Redacción**:
   - Todo hallazgo se redacta conservando únicamente los primeros 3 caracteres y sustituyendo el cuerpo sensible por asteriscos (`mon****************`, `AKI****************`). Ningún valor confidencial se expone en consola, logs ni reportes.
3. **Resultado de la Ejecución en el Repositorio**:
   ```text
   > backend@1.0.0 security:scan
   > node ../tools/security/scan-secrets.js

   [SecretScanner] Scanning directory: E:\Programs\Unity\Projects\AstroRiftGames\ProjectGrimhold
   [SecretScanner] Scan complete: No exposed credentials or private keys detected.
   ```
4. **Verificación Automatizada**:
   - Se añadieron 5 pruebas unitarias en `backend/tests/security.scan.test.js`, validando la función `redact()`, la detección de claves privadas, tokens de AWS/GitHub, URIs de MongoDB reales y la correcta omisión de placeholders sintéticos documentados.

---

## 5. Endurecimiento de `.gitignore` y Custodia Local (SEC-61.2)

Se actualizaron las políticas de exclusión de Git:

1. **`backend/.gitignore`**:
   Se añadieron exclusiones explícitas para `.env.*` (preservando `!.env.example`), certificados y claves privadas (`*.pem`, `*.key`, `*.pfx`, `*.p12`) y archivos de credenciales/OAuth (`credentials.json`, `token.json`, `client_secret*.json`).
2. **`.gitignore` (Raíz del Repositorio)**:
   Se incorporó la misma política de exclusión a nivel raíz para proteger cualquier script o herramienta en `tools/` o subdirectorios de Unity.
3. **Custodia Local**:
   Los archivos `.env` reales residen exclusivamente en los entornos de ejecución local de cada desarrollador o como variables inyectadas de entorno en el host cloud (Render / Railway), permaneciendo fuera del árbol de Git.

---

## 6. Descarte Fundamentado y Remediación (SEC-61.3)

Con base en la evidencia técnica recopilada:
- **No se confirmó ninguna credencial productiva expuesta en el historial ni en los archivos actuales.**
- **No se requiere rotación de infraestructura remota en MongoDB Atlas ni revocación de claves externas.**
- **No se requiere reescritura destructiva del historial de Git.**
- **Se implementaron controles preventivos permanentes (placeholders sintéticos inequívocos, `.gitignore` endurecido y scanner automatizado de secretos).**

Con estas acciones concluidas y verificadas, los criterios de aceptación de **SEC-61.1**, **SEC-61.2** y **SEC-61.3** quedan **100% satisfechos y listos para cierre en HacknPlan**.

# Walkthrough de Validación — US-58: Autenticar Resultados de Raid y Garantizar Recompensas Únicas

Este documento detalla las pruebas automatizadas y pasos manuales interactivos para verificar al 100% la implementación de la **US-58 (#526)** y sus tareas hijas (**SEC-58.1 #536, SEC-58.2 #537, SEC-58.3 #538, SEC-58.4 #539**).

---

## 1. Resumen de Vulnerabilidades Resueltas y Mejoras Implementadas

| Tarea | Problema Previo | Solución Implementada |
|---|---|---|
| **SEC-58.1 (#536)** | Se firmaba únicamente `raidId:resultSequence`, omitiendo items, XP, equipamiento y cuenta de destino. | Contrato canónico determinista pipe-delimited (`raidResultCanonicalizer.js` y `RaidResultCanonicalizer.cs`) que autentica items ordenados, 8 slots de equipamiento, XP, cuenta/personaje, raidId, resultId e issuedAt. Frontera de productor y deuda de Dedicated Server (#400) documentadas en Architecture. |
| **SEC-58.2 (#537)** | El backend no verificaba el payload completo con HMAC y permitía sobreescritura conflictiva de resultados. | Verificación estricta de firma HMAC-SHA256 con `crypto.timingSafeEqual`. Rechazo de firmas inválidas o payloads manipulados con `401 INVALID_HOST_SIGNATURE`. Rechazo de conflicto de republicación con `409 RESULT_CONFLICT`. Republicación idéntica idempotente (`200 OK`, `alreadyPublished: true`). |
| **SEC-58.3 (#538)** | Un cliente malicioso podía vaciar su loadout y volver a consumir el mismo raid incrementando `resultSequence = 2` para duplicar XP y botín. | Lock atómico de consumo único en MongoDB (`AuthoritativeExtractionResult.findOneAndUpdate({ _id, consumed: { $ne: true } })`). Inspección de recibos previos en `appliedExtractionReceipts`. Rechazo de secuencias falsificadas con `409 RESULT_ALREADY_CONSUMED`. |
| **SEC-58.4 (#539)** | Ausencia de suite integral end-to-end con MongoDB en memoria para certificar anti-replay, concurrencia y retiros legítimos. | Suite de 10 tests E2E en `backend/tests/integration/extraction.authoritative.test.js`, tests de unidad en `extraction.replay.test.js` y `raid.result.test.js`, y tests en Unity EditMode (`RaidResultCanonicalizerTests.cs`). |

---

## 2. Nivel 1: Verificación Automatizada en Backend

Abrí una terminal de PowerShell en `backend/` y ejecutá las suites de pruebas:

### 1.1 Tests Unitarios
```powershell
npm test
```
* **Resultado Esperado**: Pasan los **98 tests**, incluyendo `RaidResultService.publishHostResult (US-58 / SEC-58.2)` y `Extraction Replay Security (US-58 / SEC-58.3)`. Cero fallos.

### 1.2 Tests de Integración End-to-End con MongoDB en Memoria
```powershell
npm run test:integration
```
* **Resultado Esperado**: Pasan los **46 tests** de integración, incluyendo los 10 tests de `E2E Authoritative Extraction Result & Anti-Replay Validation (US-58 / SEC-58.4)`. Cero fallos.

### 1.3 Tests de Concurrencia y Condición de Carrera
```powershell
npm run test:concurrency
```
* **Resultado Esperado**: Pasan los **7 tests** de concurrencia de MongoDB real. Cero fallos.

---

## 3. Nivel 2: Verificación Automatizada en Unity (EditMode)

En Unity Editor:
1. Abrí la ventana **Test Runner** (`Window` > `General` > `Test Runner`).
2. Seleccioná la pestaña **EditMode**.
3. Buscá o filtrá por `RaidResultCanonicalizerTests`.
4. Hacé clic en **Run Selected**.
* **Resultado Esperado**: Los 2 tests (`BuildCanonicalResultString_NormalizesAndSortsItemsAndSlotsAlphabetically` y `ComputeHmacSha256_ProducesMatchingHash`) se ejecutan y quedan en **verde** (Passed), validando la paridad criptográfica exacta entre el cliente C# y el backend Node.js.

---

## 4. Nivel 3: Validación Interactiva Manual Paso a Paso (API / Postman / PowerShell)

Podés levantar el backend localmente y comprobar directamente las defensas de seguridad y el comportamiento ante reintentos.

### 4.1 Iniciar el Backend
En una terminal:
```powershell
cd backend
npm run dev
```

### 4.2 Registrar Cuentas de Host y Jugador
En otra terminal PowerShell:
```powershell
# Registrar Host
$hostAuth = Invoke-RestMethod -Uri "http://localhost:3000/auth/register" -Method Post -Body (@{ username = "HostRaid01"; password = "password123" } | ConvertTo-Json) -ContentType "application/json"
$hostToken = $hostAuth.token

# Crear Personaje de Host
Invoke-RestMethod -Uri "http://localhost:3000/character/me" -Method Post -Headers @{ Authorization = "Bearer $hostToken" } -Body (@{ name = "HostChar" } | ConvertTo-Json) -ContentType "application/json"

# Registrar Jugador
$playerAuth = Invoke-RestMethod -Uri "http://localhost:3000/auth/register" -Method Post -Body (@{ username = "PlayerRaid01"; password = "password123" } | ConvertTo-Json) -ContentType "application/json"
$playerToken = $playerAuth.token

# Crear Personaje de Jugador
$playerChar = Invoke-RestMethod -Uri "http://localhost:3000/character/me" -Method Post -Headers @{ Authorization = "Bearer $playerToken" } -Body (@{ name = "PlayerChar" } | ConvertTo-Json) -ContentType "application/json"
$playerCharId = $playerChar.characterId
```

### 4.3 Intento de Ataque: Publicación con Firma Falsificada o Botín Manipulado
Intentar publicar un resultado con firma falsa (`deadbeef...`):
```powershell
$badPayload = @{
    raidId = "raid_manual_01"
    resultSequence = 1
    targetCharacterId = $playerCharId
    items = @(@{ lootId = "iron_ore"; amount = 999 })
    experienceGranted = 10000
    hostSignature = "deadbeef00112233445566778899aabbccddeeff"
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "http://localhost:3000/character/raid/extraction-result" -Method Post -Headers @{ Authorization = "Bearer $hostToken" } -Body $badPayload -ContentType "application/json"
} catch {
    $_.Exception.Response.StatusCode.value__
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $reader.ReadToEnd()
}
```
* **Resultado Esperado**:
  - Código HTTP: `401 Unauthorized`
  - Body: `{"error":"INVALID_HOST_SIGNATURE","message":"The host signature is invalid or the payload has been tampered with."}`

### 4.4 Publicación Legítima con Firma Canónica Válida
Calcular la firma HMAC-SHA256 legítima sobre el payload canónico en PowerShell:
```powershell
$secret = "dev-webhook-secret-do-not-use-in-prod"
$raidId = "raid_manual_01"
$resultId = "raid_manual_01:$playerCharId:1"
$targetId = $playerCharId
$items = "iron_ore:5"
$equipment = "armor=;boots=;gloves=;helmet=helm_iron;offHand1=;offHand2=;weaponSlot1=sword_basic;weaponSlot2="
$xp = 50
$issuedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

$canonical = "$raidId|$resultId|$targetId|$items|$equipment|$xp|$issuedAt"

$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
$hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($canonical))
$sig = [BitConverter]::ToString($hash).Replace("-", "").ToLower()

$goodPayload = @{
    raidId = $raidId
    resultId = $resultId
    resultSequence = 1
    targetCharacterId = $targetId
    items = @(@{ lootId = "iron_ore"; amount = 5 })
    preparedEquipment = @{ helmet = "helm_iron"; weaponSlot1 = "sword_basic" }
    experienceGranted = 50
    issuedAt = $issuedAt
    hostSignature = $sig
} | ConvertTo-Json

$pubRes = Invoke-RestMethod -Uri "http://localhost:3000/character/raid/extraction-result" -Method Post -Headers @{ Authorization = "Bearer $hostToken" } -Body $goodPayload -ContentType "application/json"
$pubRes | ConvertTo-Json
```
* **Resultado Esperado**:
  - Código HTTP: `200 OK`
  - Body:
    ```json
    {
      "status": "published",
      "alreadyPublished": false,
      "raidId": "raid_manual_01",
      "resultId": "raid_manual_01:...:1"
    }
    ```

### 4.5 Consumo Legítimo de la Extracción por el Jugador
El jugador confirma su extracción en el backend:
```powershell
$commitRes = Invoke-RestMethod -Uri "http://localhost:3000/character/me/extraction/commit" -Method Post -Headers @{ Authorization = "Bearer $playerToken" } -Body (@{ raidId = "raid_manual_01"; resultSequence = 1 } | ConvertTo-Json) -ContentType "application/json"
$commitRes | ConvertTo-Json
```
* **Resultado Esperado**:
  - Código HTTP: `201 Created`
  - Body:
    - `alreadySecured`: `false`
    - `experience`: `50`
    - `loadout`: contiene `iron_ore` x 5
    - `preparedEquipment`: helmet = "helm_iron", weaponSlot1 = "sword_basic"

### 4.6 Intento de Explotación: Multi-Sequence Replay (Double Dipping)
Simular que el jugador mueve los items de su loadout al stash o los gasta, e intenta reclamar de nuevo el mismo raid enviando `resultSequence = 2`:
```powershell
try {
    Invoke-RestMethod -Uri "http://localhost:3000/character/me/extraction/commit" -Method Post -Headers @{ Authorization = "Bearer $playerToken" } -Body (@{ raidId = "raid_manual_01"; resultSequence = 2 } | ConvertTo-Json) -ContentType "application/json"
} catch {
    $_.Exception.Response.StatusCode.value__
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $reader.ReadToEnd()
}
```
* **Resultado Esperado**:
  - Código HTTP: `409 Conflict`
  - Body: `{"error":"RESULT_ALREADY_CONSUMED","message":"This authoritative extraction result has already been claimed and consumed."}`
  - Ni la experiencia ni los objetos son duplicados.

### 4.7 Reintento Legítimo Idempotente (Pérdida de ACK)
El jugador reenvía exactamente el mismo recibo (`raid_manual_01`, `resultSequence = 1`):
```powershell
$retryRes = Invoke-RestMethod -Uri "http://localhost:3000/character/me/extraction/commit" -Method Post -Headers @{ Authorization = "Bearer $playerToken" } -Body (@{ raidId = "raid_manual_01"; resultSequence = 1 } | ConvertTo-Json) -ContentType "application/json"
$retryRes | ConvertTo-Json
```
* **Resultado Esperado**:
  - Código HTTP: `200 OK`
  - Body: `alreadySecured`: `true` (la operación es 100% idempotente y segura ante fallos de red).

---

## 5. Matriz de Cobertura de Criterios de Aceptación (HacknPlan)

| Tarea | Criterio de Aceptación | Estado |
|---|---|---|
| **SEC-58.1 (#536)** | Especifica qué actor está autorizado a producir resultados y por qué su prueba no puede fabricarse con un secreto en un cliente. Documenta deuda #400. | ✅ Cumplido (`ExtractionArchitecture.md`, `RemoteIdentityAndAuthArchitecture.md`). |
| **SEC-58.1 (#536)** | Incluye recompensas, equipamiento, XP, cuenta/personaje, raidId, resultId y vigencia en el contrato canónico autenticado. | ✅ Cumplido (`raidResultCanonicalizer.js`, `RaidResultCanonicalizer.cs`). |
| **SEC-58.2 (#537)** | Servidor verifica firma HMAC-SHA256 completa con timing seguro y rechaza payloads manipulados con 401. | ✅ Cumplido (`RaidResultService.js`, `raid.result.test.js`). |
| **SEC-58.2 (#537)** | Bloquea sobreescrituras conflictivas con 409 y permite reintentos idénticos con 200 `alreadyPublished: true`. | ✅ Cumplido (`RaidResultService.js`, `extraction.authoritative.test.js`). |
| **SEC-58.3 (#538)** | Lock atómico distribuido en MongoDB (`consumed: true`) que previene doble consumo de resultados. | ✅ Cumplido (`ExtractionCommitService.js`, `AuthoritativeExtractionResult.js`). |
| **SEC-58.3 (#538)** | Exploit de repetición con secuencias arbitrarias (`resultSequence = 2`) bloqueado con 409. | ✅ Cumplido (`extraction.replay.test.js`, `extraction.authoritative.test.js`). |
| **SEC-58.4 (#539)** | Suite E2E integral automatizada con MongoDB y EditMode en Unity con separación de evidencias. | ✅ Cumplido (`extraction.authoritative.test.js`, `RaidResultCanonicalizerTests.cs`). |

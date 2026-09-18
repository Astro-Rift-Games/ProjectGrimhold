# Plan Correctivo — Persistencia de Misiones (post-vertical-slice)

**Repositorio:** Astro-Rift-Games/ProjectGrimhold — branch `Mission-System`
**Origen:** Auditoría (`auditoria_sistema_misiones.md`) + verificación directa contra el código actual de la branch, en respuesta a los errores `PersistenceFailed` / "Applied extraction receipts contain invalid or duplicate data" al aceptar y reclamar Misiones.
**Relación con el plan anterior:** Este plan no reabre las Etapas 1–9 ya implementadas. Es una corrección puntual de un bug de persistencia preexistente en `Stash/`, expuesto por el sistema de Misiones porque `TrySave` valida el snapshot completo en cada `Commit()`.

---

## 0. Instrucciones para Antigravity (obligatorias — se mantienen del plan original)

1. Esta corrección se implementa en **una sola etapa**, pero igual debe entregarse completa según el punto 5 de esta sección antes de darse por cerrada — no hay avance a "siguiente etapa" sin mi aprobación porque no hay siguiente etapa: al terminar, Antigravity se detiene y espera validación.
2. El proyecto debe compilar y los tests automatizados deben quedar en verde al finalizar.
3. Seguir `AGENTS.md`: no introducir un segundo mecanismo de idempotencia/watermark paralelo al que ya existe para Progresión y Extracción; reutilizar el patrón existente.
4. No expandir el alcance a otros bugs no descritos en este documento, aunque aparezcan durante la implementación — si aparece algo nuevo, reportarlo en el walkthrough en vez de corregirlo silenciosamente.
5. Entregar, al finalizar:
   - Código + tests.
   - Un walkthrough `docs/plan-mission-system/Etapa-Correctiva-1-persistencia.md` con: resumen de cambios, archivos tocados, mensaje de commit sugerido, pasos de validación manual en el Editor de Unity, y configuración de Inspector/escena si aplica.
   - Confirmación explícita de qué causa raíz quedó resuelta y cuál (si alguna) quedó fuera de alcance.
6. No reclamar validación manual o de Play Mode que no se haya ejecutado realmente.

---

## 1. Diagnóstico (resumen, ya validado contra el código)

### Causa raíz real de los errores visibles en el log
`ApplicationStashServiceBootstrapper.HydrateSnapshot` (`Assets/Scripts/Stash/ApplicationStashServiceBootstrapper.cs`, ~línea 245) agrega el último `ExtractionReceipt` confirmado por el backend a `snapshot.AppliedExtractionReceipts` **sin limpiar la lista antes**, a diferencia del bloque de Progresión unas líneas más abajo (~línea 274), que sí hace `Clear()` antes de `Add()`. Como `HydrateSnapshot` corre en cada login (`InitializeStore`) sobre un snapshot que ya fue cargado desde el JSON en disco, el mismo recibo se duplica a partir del segundo login posterior a cualquier extracción. `LocalProfileRepository.TrySave` (~línea 117) valida el snapshot completo mediante un round-trip `Encode` → `TryDecode` antes de escribir a disco; `LocalProfileSaveCodec.TryDecode` rechaza duplicados en `AppliedExtractionReceipts` con el error exacto visto en consola. Como esa validación cubre el snapshot entero, **cualquier** `Commit()` posterior falla — no solo los relacionados a Misiones — lo que explica que tanto `TryAcceptMission` como `TryClaimMission` fallen con el mismo error sin relación funcional entre ambas.

### Bug secundario real, distinto, no responsable del error visible
`LocalProfileStore.TryClaimMission`, rama `RewardType.Experience`, aplica `CharacterProgressionRules.TryApplyExperience` y sobreescribe `next.Level`/`next.CurrentExperience` sin actualizar `LastAppliedProgressionResultSequence`, `LastProgressionReceipt` ni `AppliedProgressionReceipts`. No dispara el error del log (los checks de Progresión no cruzan Level/XP contra el recibo), pero deja el nivel/XP persistido sin recibo trazable, con riesgo concreto de pérdida silenciosa de XP en la siguiente extracción si esa extracción calcula su nivel/XP base desde un snapshot de Progresión tomado antes del Claim.

### Hallazgo menor
`LocalProfileSaveCodec.cs` (~línea 611) tiene un `Debug.LogError("[AUDIT-CODEC]...")` agregado durante el debugging previo, persiguiendo la pista equivocada (el desincronismo de Progresión, no el de Extracción). No es dañino pero es ruido de diagnóstico que no debería quedar en el código final.

---

## 2. Alcance de la corrección

### 2.1 Fix principal — duplicación de `AppliedExtractionReceipts`
En `HydrateSnapshot`, limpiar `snapshot.AppliedExtractionReceipts` antes de agregar el recibo confirmado por el backend, replicando exactamente el patrón ya usado para `AppliedProgressionReceipts` en el mismo método. El backend es la fuente de verdad sobre el último recibo aplicado; el historial local se reemplaza, no se acumula.

### 2.2 Fix secundario — recibo de Progresión ausente en el Claim de Misiones con recompensa de XP
En `LocalProfileStore.TryClaimMission`, rama `RewardType.Experience`: además de aplicar `next.Level`/`next.CurrentExperience`, generar y encadenar un `ProgressionReceipt` para esa aplicación de XP, siguiendo el mismo mecanismo ya usado por `TryCommitExtraction` (incrementar `LastAppliedProgressionResultSequence`, construir el recibo, asignarlo a `LastProgressionReceipt`, agregarlo a `AppliedProgressionReceipts` respetando `MaxAppliedProgressionReceipts`). No crear un segundo mecanismo de watermark: Misiones reutiliza la misma secuencia/cadena de recibos que ya gobierna Progresión.

### 2.3 Limpieza
Retirar el `Debug.LogError("[AUDIT-CODEC]...")` de `LocalProfileSaveCodec.cs` una vez confirmado que el fix 2.1 resuelve los errores observados.

### 2.4 Tests a agregar/actualizar
- EditMode: un login simulado con un `AppliedExtractionReceipts` ya presente en el snapshot cargado de disco + un `lastAppliedExtractionReceipt` del backend con el mismo `raidId`/`resultSequence` no debe producir una lista con duplicados tras `HydrateSnapshot`.
- EditMode: dos logins simulados consecutivos con el mismo recibo de backend no deben dejar el snapshot en un estado que falle `TryDecode`.
- EditMode: `TryClaimMission` con una recompensa de XP debe dejar `AppliedProgressionReceipts.Last()` igual a `LastProgressionReceipt`, y `LastAppliedProgressionResultSequence` coherente con ese recibo, después del Claim.
- EditMode: un `TrySave` posterior a un Claim con recompensa de XP no debe fallar la validación de round-trip del Codec.

### 2.5 Fuera de alcance
No se toca el flujo de `TryCommitExtraction` en sí, ni el cálculo del origen del nivel/XP base al iniciar un Raid (si ese origen efectivamente lee del `LocalProfileStore` al momento de la extracción o de la admisión, es una pregunta abierta que puede requerir su propia investigación si tras este fix se observa pérdida de XP entre un Claim y una extracción — se reporta, no se corrige acá). No se toca la UI de Misiones ni el resto del sistema de Guild/Contratos.

---

## 3. Criterios de aceptación de esta etapa

- Aceptar una Misión y reclamar una Misión (con y sin recompensa de XP) funcionan sin error `PersistenceFailed` en una sesión que ya tuvo al menos una extracción y un reinicio de aplicación previos (el escenario exacto que hoy reproduce el bug).
- Reclamar dos misiones con recompensa de XP en sesiones distintas dentro de un mismo perfil no deja el historial de recibos de Progresión inconsistente.
- La suite EditMode completa (incluida la de Misiones ya existente) sigue en verde.
- El log `[AUDIT-CODEC]` ya no aparece en consola.

## 4. Validación manual esperada en Unity (a detallar en el walkthrough final)

1. Desde una sesión con backend conectado (o el mock/fixture que se use en desarrollo), completar una extracción para generar un `AppliedExtractionReceipts`.
2. Cerrar y volver a abrir la aplicación (nuevo login sobre el mismo perfil) — este es el paso que hoy dispara la duplicación.
3. Aceptar un Contrato Rango E y confirmar que no aparece `PersistenceFailed` en consola.
4. Completar el Contrato, reclamarlo con una recompensa de XP configurada, y confirmar en Inspector/HUD que el nivel/XP se actualiza y que el Commit no falla.
5. Repetir el login (cerrar/abrir de nuevo) una vez más y confirmar que el perfil sigue cargando sin error de "duplicate data".

---

¿Avanzás con esto tal cual, o preferís que ajuste algo del alcance antes de que Antigravity lo implemente?

# Walkthrough — US-53 · Etapa 1: Línea base y bloqueante heredado de US-52

## 1. Resumen de cambios
- **Resultado entregado:** Se estableció y registró la línea base de la suite EditMode (3823 tests) y de los ScriptableObjects de configuración de prueba. Se solucionó el defecto bloqueante H-1 en `NetworkSpawnManager.TryConfirmTeammateAbsence`: la confirmación de ausencia («Continuar sin compañero») ahora valida previamente la resolución del avatar vía `RaidTeammateAbsenceDecisionRules.Evaluate` (retornando `RejectedTargetAvatarUnavailable`) y garantiza que la transición a `Defeated` haya sido efectiva antes de aplicar cualquier efecto colateral terminal (`MarkTerminal`, desregistro de `_disconnectedRegistry`, etc.), preservando la invariante *«nunca terminal + fuera del registro + Raiding»*.
- **Archivos creados / modificados / eliminados:**
  - `Project Grimhold/Assets/Scripts/Networking/RaidTeammateAbsenceDecisionRules.cs` (modificado: añade `RejectedTargetAvatarUnavailable` al enum `TeammateAbsenceDecisionResult` y parámetro de evaluación `isTargetAvatarResolvable`).
  - `Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs` (modificado: evalúa `isAvatarResolvable` y condiciona mutaciones terminales a la confirmación efectiva de `Defeated`).
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidTeammateAbsenceExecutionTests.cs` (modificado: añade test unitario EditMode `Evaluate_RejectsWhenTargetAvatarUnresolvable`).
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidConnectivityContinuityTests.cs` (modificado: añade aserción 7 para `RejectedTargetAvatarUnavailable`).
  - `Project Grimhold/Docs/Walkthroughs/US-53/Etapa-1-linea-base-y-bloqueante-h1.md` (creado: registro documental del walkthrough de la Etapa 1).
- **Qué quedó FUERA de la etapa y por qué:** Etapa 2 a 7 (contratos de equipos, reserva de progreso compartida, santuario, countdown y HUD). Los follow-ups heredados de US-52 no bloqueantes de extracción (loot, 20% XP forzada, etc.) se mantienen fuera de alcance conforme a la sección 11 del plan.
- **Estado de criterios de aceptación (TASK-190 / TASK-191) afectados por esta etapa:**
  - T191: «Validación de Solo, Dúo, Abatido, desconexión y Continuar sin compañero»: se desrejó el camino de fallo que dejaba al compañero ausente en estado fantasma impidiendo la resolución de la extracción.
- **Decisiones técnicas tomadas y alternativas descartadas:**
  - *Tomada:* Incorporar `RejectedTargetAvatarUnavailable` en `RaidTeammateAbsenceDecisionRules` como regla pura parametrizada con valor default `true` para preservar compatibilidad estricta con todas las llamadas existentes y pruebas unitarias preexistentes.
  - *Descartada:* Forzar la transición a `Defeated` de forma sintética sin avatar; fue descartada porque viola el pipeline arquitectónico de cadáveres/loot documentado en `RaidConnectivityContinuityArchitecture.md`.
- **Hallazgos clasificados:**
  - *Current-task:* Ninguno adicional en esta etapa.
  - *Follow-up:* Reversión de mutaciones automáticas de TextMeshPro/animaciones provocadas por la compilación de Unity (se aplicó `git checkout` para asegurar un diff 100% limpio).
  - *Blocker:* Resuelto (bloqueante H-1 remediado).
- **Estado intermedio incoherente (si lo hay):** Ninguno. El sistema de continuidad de ausencia es ahora estrictamente *fail-closed* ante la ausencia de avatar.

## 2. Ejecución real
- **Compilación y consola:** Compilación limpia observada en Unity Editor mediante Unity MCP (`read_console`). 0 errores.
- **Tests ejecutados con Unity MCP (suite, cantidad, resultado):**
  - Suite completa EditMode: 3823 tests ejecutados como línea base. Se registraron los fallos preexistentes documentados en `output.txt` (Merchant, PlayerAnimatorView, etc., ajenos a networking/extracción).
  - `Tests.EditMode.Networking.RaidTeammateAbsenceExecutionTests`: 9 tests ejecutados, 9 pasados (100%).
  - `RaidConnectivityContinuityTests`: 15 tests ejecutados, 15 pasados (100%).
  - Total combinado de pruebas de ausencia/continuidad: 24 tests pasados.
- **Tests NO ejecutados y por qué:** Pruebas PlayMode de multiplayer real para confirmación de ausencia (el flujo de RPC requiere dos clientes Fusion activos; se cubrió mediante EditMode sobre las reglas puras).
- **Cambios serializados verificados:**
  - Valores de prueba confirmados e intactos:
    - `ReconnectBudgetConfig.asset`: `_durationSeconds: 30`
    - `DefaultDungeonPressureConfig.asset`: 300 / 240 / 60
    - `ExtractionConfig.asset`: `_progressQuota: 100`, `_countdownDurationSeconds: 5`, etc.
  - Cero cambios en prefabs, escenas o assets (.meta o .asset).

## 3. Instrucciones de implementación manual en Unity
No requiere.

## 4. Instrucciones de validación manual
1. Iniciar sesión Host y Client en modo Dúo en la escena `Gameplay`.
2. Forzar la desconexión del Client y esperar a que transcurra el presupuesto de reconexión (30 s según el asset de prueba).
3. Presionar «Continuar sin compañero» en la UI del Host.
4. **Resultado esperado:**
   - Si el avatar del cliente desconectado está presente, el Host ejecuta la derrota forzada, se genera el cadáver con loot y el participante pasa a `Defeated`.
   - Si el avatar por algún motivo hubiese sido destruido o no se pudiese resolver, la confirmación se rechaza de forma limpia con warning en consola (`RejectedTargetAvatarUnavailable`) y no se marca como terminal ni se desregistra al participante en estado `Raiding`.

## 5. Información para el commit
**Título:** `fix(raid): prevent premature absence finalization when target avatar is unresolvable`
**Descripción:**
Enforce strict fail-closed validation in `NetworkSpawnManager.TryConfirmTeammateAbsence` to uphold the invariant that a participant is never marked terminal or removed from the disconnected registry while remaining in `Raiding`.
Evaluates avatar resolvability via `RaidTeammateAbsenceDecisionRules.Evaluate` returning `RejectedTargetAvatarUnavailable`, and verifies that forced defeat actually transitions the target participant to `Defeated` before executing terminal mutations.

Tests:
- `Tests.EditMode.Networking.RaidTeammateAbsenceExecutionTests` (9 passed, including `Evaluate_RejectsWhenTargetAvatarUnresolvable`)
- `RaidConnectivityContinuityTests` (15 passed, covering all 7 rejection and confirmation states)

**Archivos incluidos:**
- `Project Grimhold/Assets/Scripts/Networking/RaidTeammateAbsenceDecisionRules.cs`
- `Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidTeammateAbsenceExecutionTests.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidConnectivityContinuityTests.cs`

**Archivos excluidos:**
- `Plan-US-53-Extraccion-Compartida-Equipo.md`
- `Project Grimhold/Docs/Walkthroughs/US-53/Etapa-1-linea-base-y-bloqueante-h1.md`

## 6. Siguiente paso
Esperar confirmación explícita del usuario para continuar con la **Etapa 2 — Contrato técnico y modelo puro de equipos**.

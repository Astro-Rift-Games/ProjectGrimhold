# Walkthrough — US-53 · Etapa 4: Santuario por equipo: asignación y ritual (TASK-190, parte 2)

## 1. Resumen de cambios
- **Resultado entregado:** Se migró el ciclo de vida de los santuarios y el servicio de asignación al modelo de equipos por `RaidTeamId`. Cada `ExtractionSanctuary` ahora gestiona slots replicados por equipo (`[Networked, Capacity(RaidSessionRules.MaxTeams)] NetworkArray<TeamSanctuarySlot>`), permitiendo que múltiples equipos compartan el mismo santuario simultáneamente (decisión D-3). La asignación se dispara de forma autoritativa en el mismo tick en que se completa la cuota desde `TeamExtractionProgress`. El ritual se inicia bajo las reglas puras de `TeamRitualRules` (cualquier integrante Activo, vivo y conectado) y se cancela únicamente si el equipo pierde todos sus integrantes requeridos (A-4 / D-5). Se eliminó el remapeo de jugadores en `HostMigrationSnapshotRestorer` ya que los slots están indexados por `RaidTeamId` inmutable.
- **Archivos creados / modificados / eliminados:**
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamSanctuarySlot.cs` (creado): Struct de red (`INetworkStruct`) que almacena `{TeamIdRaw, RitualState, RitualTimer}` por slot de equipo en el santuario.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamSanctuarySlot.cs.meta` (creado): Metadatos de Unity para el nuevo struct de red.
  - `Project Grimhold/Assets/Scripts/Core/IExtractionSanctuary.cs` (modificado): Contrato actualizado a APIs por equipo (`IsAssignedToTeam`, `TryAssignTeam`, `CanUseExtraction(RaidTeamId)`, `TryGetRitualProgress(RaidTeamId, ...)`, `IsAnyRitualCompleted`) manteniendo helpers para compatibilidad de presenters.
  - `Project Grimhold/Assets/Scripts/Core/SanctuarySelectionPolicy.cs` (modificado): Agregada sobrecarga determinista `SelectIndex` basada en `RaidTeamId`.
  - `Project Grimhold/Assets/Scripts/Core/SanctuaryAssignmentResult.cs` (modificado): Soporte para `RaidTeamId TeamId` y métodos factoría para asignación por equipo.
  - `Project Grimhold/Assets/Scripts/Core/SanctuaryAssignmentFailureReason.cs` (modificado): Agregada razón `InvalidTeam`.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionSanctuary.cs` (modificado): Implementación con slots replicados por equipo, gating por `TeamRitualRules`, soporte compartido (D-3) y progreso/cancelación autoritativa.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionSanctuaryAssignmentService.cs` (modificado): Asignación y consulta basadas en `RaidTeamId`, selección determinista entre todos los santuarios, e idempotencia.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionZone.cs` (modificado): Disponibilidad condicionada a `sanctuary.IsAnyRitualCompleted`.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamExtractionProgress.cs` (modificado): Disparo autoritativo de `_assignmentService.TryAssign(teamId)` en el mismo tick al completar la cuota.
  - `Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionProgressController.cs` (modificado): Eliminado el trigger individual temporal y la dependencia de `ExtractionSanctuaryAssignmentService`.
  - `Project Grimhold/Assets/Scripts/Networking/HostMigrationSnapshotRestorer.cs` (modificado): Eliminado el bucle de remapeo de santuarios por `EntityId` dinámico.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/SanctuaryAssignmentTests.cs` (modificado): Pruebas reescritas para asignación por equipo, santuarios compartidos (D-3), determinismo e idempotencia.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/TeamSanctuaryTests.cs` (creado): Pruebas unitarias de slots de santuario, gating de interacción, progresión de ritual y cancelación según integrantes requeridos.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/TeamSanctuaryTests.cs.meta` (creado): Metadatos de Unity para la nueva suite de tests.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/PlayerExtractionProgressForwarderTests.cs` (modificado): Limpieza de referencia a `_assignmentService`.
- **Qué quedó FUERA de la etapa y por qué:**
  - `TeamExtractionCoordinator` y la cuenta regresiva conjunta de extracción (reservado para la Etapa 5).
  - Actualización de los Presenters de HUD y Minimapa (`RaidHudPresenter`, `RaidMinimapPresenter`, `ExtractionSanctuaryPresenter`) (reservado para la Etapa 6, aunque se mantuvieron compatibles).
- **Estado de criterios de aceptación (TASK-190 / TASK-191) afectados por esta etapa:**
  - *Asignación única e inmutable por equipo:* Implementada e idempotente en `ExtractionSanctuaryAssignmentService`.
  - *Santuario compartido por múltiples equipos (D-3):* Implementado en `ExtractionSanctuary` con slots de red independientes.
  - *Ritual autoritativo por equipo:* Gating por `TeamRitualRules` activo, no cancelable por movimiento o desconexión del iniciador, y cancelable solo por derrota/abandono total del equipo.
- **Decisiones técnicas tomadas y alternativas descartadas:**
  - *D-3 Santuario compartido:* En lugar de reservar un santuario de forma exclusiva bloqueándolo para otros equipos, cada santuario mantiene un array de slots (`TeamSanctuarySlot`) indexado por equipo, permitiendo que rivales compartan la misma zona física sin bloquearse.
  - *Remapeo de Host Migration:* Como `RaidTeamId` es invariante en la sesión (1..16) y no depende del `EntityId` del avatar o runner, se eliminó la necesidad de remapeo dinámico en `HostMigrationSnapshotRestorer`.
- **Hallazgos clasificados:**
  - *Current-task:* Adaptar los métodos de consulta en `IExtractionSanctuary` para permitir que los presenters existentes (`RaidHudPresenter`, etc.) continúen compilando sin fallas antes de su refactor en la Etapa 6.
  - *Follow-up:* Etapa 5 implementará `TeamExtractionCoordinator` en orden de ejecución 115 para gestionar el countdown conjunto.
- **Estado intermedio incoherente:** Ninguno.

---

## 2. Ejecución real
- **Compilación y consola:**
  - `dotnet build "Project Grimhold/Assembly-CSharp.csproj"`: 0 Errores, 60 Advertencias preexistentes.
  - `dotnet build "Project Grimhold/Assembly-CSharp-Editor.csproj"`: 0 Errores, 5 Advertencias preexistentes.
- **Tests ejecutados con Unity MCP (suite, cantidad, resultado):**
  - Sesión interactiva de Unity MCP no disponible en background (Editor de Unity en segundo plano); validación de compilación limpia de ambas soluciones C# completada por CLI.
- **Tests NO ejecutados y por qué:**
  - Test runner interactivo de Unity en GUI no invocable de forma desatendida sin foco en el Editor.
- **Cambios serializados verificados:**
  - Ningún archivo `.prefab`, `.unity` o `.asset` modificado. Todos los cambios se realizaron a nivel de código C# puro y scripts de red.

---

## 3. Instrucciones de implementación manual en Unity
No requiere ninguna acción manual.

---

## 4. Instrucciones de validación manual
1. **Verificación de asignación de santuario en Host:**
   - Iniciar sesión de Raid con un dúo.
   - Contribuir progreso hasta alcanzar la cuota (150).
   - Verificar en el Inspector de los 4 `ExtractionSanctuary` de la escena `Gameplay` que uno de ellos tiene asignado el `TeamIdRaw = 1` en su lista de slots con `RitualState = NotStarted`.
2. **Inicio del ritual:**
   - Acercarse al santuario asignado con el jugador 1 (vivo y Activo).
   - Interactuar con el santuario.
   - Verificar que el ritual pasa a `RitualState = InProgress` y comienza a correr el temporizador.
   - Mover al jugador fuera de la zona o recibir daño; verificar que el ritual continúa sin cancelarse.
3. **Cancelación del ritual:**
   - En una sesión de prueba donde el ritual esté en progreso, forzar la derrota definitiva de ambos jugadores del equipo.
   - Verificar que el ritual pasa a `RitualState = Cancelled`.

---

## 5. Información para el commit
**Título:** `feat(extraction): implement team-based extraction sanctuary assignment and ritual`

**Descripción:**
Migrate extraction sanctuary reservations and ritual lifecycles to the team-based model for US-53 (TASK-190 part 2). Each sanctuary now supports multiple concurrent teams via networked slots, and assignment is triggered deterministically upon team quota completion.

Reviewers should verify:
- `TeamSanctuarySlot` (`INetworkStruct`) tracks `{TeamIdRaw, RitualState, RitualTimer}`.
- `ExtractionSanctuary` supports per-team slots (`Capacity(RaidSessionRules.MaxTeams)`), shared sanctuaries (D-3), and pure ritual rules (`TeamRitualRules`).
- `ExtractionSanctuaryAssignmentService` assigns sanctuaries deterministically per `RaidTeamId` using `SanctuarySelectionPolicy`.
- `TeamExtractionProgress` triggers team assignment authoritatively on quota completion; the temporary individual trigger in `PlayerExtractionProgressController` is removed.
- `ExtractionZone` availability checks `sanctuary.IsAnyRitualCompleted`.
- Dynamic player remapping in `HostMigrationSnapshotRestorer` removed for sanctuaries as slots are keyed by immutable `RaidTeamId`.
- Unit tests added in `TeamSanctuaryTests` and updated in `SanctuaryAssignmentTests`.

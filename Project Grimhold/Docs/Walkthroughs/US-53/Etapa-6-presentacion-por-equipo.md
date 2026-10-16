# Walkthrough — US-53 · Etapa 6: Presentación por equipo: HUD, minimapa y widget de extracción (TASK-190/191)

## 1. Resumen de cambios
- **Resultado entregado:** Se adaptó la capa completa de presentación para equipos (Solo y Dúo) en el HUD (`RaidHudPresenter`, `RaidHudView`), minimapa (`RaidMinimapPresenter`), santuario (`ExtractionSanctuaryPresenter`), y se expuso la presencia de integrantes en el área de extracción (`ITeamExtractionCoordinator.TryGetTeamPresence`). Asimismo, se endureció la resiliencia de asignación de santuario en tiempo de ejecución en `TeamExtractionProgress` y se añadieron herramientas de diagnóstico para el Inspector en Play Mode.
  - **HUD (`RaidHudView`, `RaidHudPresenter`):** La sección de cuota y progreso lee de forma unificada la reserva del equipo vía `PlayerExtractionProgressController.TryGetSnapshot`. Cuando el equipo tiene cuota compartida o santuario asignado, la sección de santuario y la de cuenta regresiva presentan el conteo de presencia de los integrantes requeridos (p. ej. `"Santuario asignado (1/2 en el área)"` o `"Extracción: 2,4 s (2/2 en el área)"`). Para partidas en solitario (`requiredCount <= 1`), el formato compacto original se mantiene idéntico (`"Santuario asignado"` y `"Extracción: 2,4 s"`). Se respetan los dirty checks independientes para no generar repintados innecesarios ni garbage.
  - **Minimapa (`RaidMinimapPresenter`):** La asignación del santuario se resuelve autoritativamente mediante el `RaidTeamId` del jugador local (`TryResolveLocalTeam`). El marcador en el minimapa se proyecta únicamente para el equipo asignado, garantizando privacidad de navegación frente a equipos rivales.
  - **Santuario (`ExtractionSanctuaryPresenter`):** Privacidad por equipo: el equipo local asignado observa la reserva privada (`isReserved = true`) y su progreso particular de ritual. Los equipos rivales observan el progreso agregado público (si algún ritual está en curso) sin observar la reserva privada ajena (`isReserved = false`). Las señales audiovisuales de asignación (`Assigned`) y cancelación (`Cancelled`) están restringidas estrictamente al equipo asignado (`isLocalOwner = true`), mientras que las de inicio (`Started`) y completado (`Completed`) permanecen públicas. Se mantiene la ausencia de cues espurias al unirse tarde o migrar de host.
  - **Resiliencia en Runtime (`TeamExtractionProgress`):** Se incorporó ejecución en `FixedUpdateNetwork` bajo `StateAuthority` con fallback para inicializar el Equipo 1 en partidas de desarrollo / testeo sin roster pre-congelado, y reintento continuo e idempotente de `TryAssign` mientras la cuota esté completa (`AssignmentRequested == true`) y no haya santuario confirmado aún.
  - **Sin nuevo estado de red:** Se cumplió estrictamente la política D-3 de arquitectura: 0 campos `[Networked]` adicionales, 0 RPCs y 0 mutaciones de simulación en los componentes de presentación.
- **Archivos creados / modificados / eliminados:**
  - `Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionProgressController.cs` (modificado): Agregado accessor público `TryGetTeamId(out RaidTeamId teamId)`.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/ITeamExtractionCoordinator.cs` (modificado): Agregado método `bool TryGetTeamPresence(RaidTeamId teamId, out int presentCount, out int requiredCount)`.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamExtractionCoordinator.cs` (modificado): Implementada consulta de presencia de integrantes requeridos en zona y conectados (`TryGetTeamPresence`), con hook `TeamPresenceOverrideForTest` para pruebas.
  - `Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudView.cs` (modificado): Agregadas sobrecargas `PresentSanctuaryAssigned(int presentCount, int requiredCount)` y `PresentExtractionCountdown(float remainingSeconds, int presentCount, int requiredCount)` que muestran conteo de presencia cuando `requiredCount > 1`.
  - `Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs` (modificado): Integrado `TryGetTeamPresence` y `TryGetCountdownProgress` del coordinador; añadidas variables observadas para presencia de santuario y countdown; métodos `RefreshSanctuarySectionPresence` y `RefreshRitualStatusSectionPresence` con dirty checks por presencia; preservados los métodos legacy sin parámetros para retrocompatibilidad y tests por reflexión.
  - `Project Grimhold/Assets/Scripts/Player/Presentation/RaidMinimapPresenter.cs` (modificado): Incorporada resolución de `RaidTeamId` local (`TryResolveLocalTeam`); validación y caché de santuario por equipo (`registered.IsAssignedToTeam(teamId)` y `_assignmentService.TryGetAssignment(teamId)`).
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionSanctuaryPresenter.cs` (modificado): Incorporada resolución de equipo local (`TryResolveLocalTeam`); distinción entre observación privada del equipo asignado y agregada para rivales; emisión de cues `Assigned` y `Cancelled` exclusiva para el equipo asignado.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamExtractionProgress.cs` (modificado): Incorporada resiliencia en `FixedUpdateNetwork` con reintento idempotente de `TryAssign` mientras `AssignmentRequested` esté activo y fallback para inicialización en partidas de desarrollo; añadido menú contextual de diagnóstico `Debug: Log Team Progress Status`.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamProgressSlot.cs` (modificado): Añadida propiedad `IsAssigned => TeamIdRaw != 0` para coherencia de API con `TeamSanctuarySlot`.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionSanctuary.cs` (modificado): Añadido menú contextual de diagnóstico `Debug: Log Sanctuary Status` y logs de asignación de slots.
  - `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionSanctuaryAssignmentService.cs` (modificado): Añadido menú contextual de diagnóstico `Debug: Log Assignment Status` y logs informativos de resolución de asignación.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TeamExtractionPresentationTests.cs` (creado): Suite de pruebas unitarias EditMode (6 tests) cubriendo formateo de presencia en view, dirty checking en presenter, privacidad de cues en santuario y consulta de presencia en coordinador.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TeamExtractionPresentationTests.cs.meta` (creado): Metadatos de Unity para la nueva suite de pruebas.
- **Qué quedó FUERA de la etapa y por qué:**
  - Validación integral final, checklist completo de Host Migration y cierre documental de `ExtractionArchitecture.md` (reservados para la Etapa 7 según el plan).
- **Estado de criterios de aceptación (TASK-190/191) afectados por esta etapa:**
  - *Presentación unificada de progreso y cuota en HUD:* Ambos integrantes observan la misma reserva compartida y cuota inmutable de equipo.
  - *HUD muestra presencia en área de extracción:* Validado con presencia condicional `(n/m en el área)` cuando `m > 1`.
  - *Minimapa muestra santuario privado al equipo asignado:* Validado por resolución de `RaidTeamId`.
  - *Privacidad de cues y reserva en santuario:* Validado en tests unitarios (`ExtractionSanctuaryPresenter_ResolveObservedCues_AssignedAndCancelledArePrivateToAssignedTeam`).
  - *Pulso público de ritual en curso:* Rivales observan pulso de ritual activo sin observar reserva privada ajena.
  - *Asignación y persistencia de santuario en runtime:* Verificado en vivo en Play Mode.
- **Decisiones técnicas tomadas y alternativas descartadas:**
  - *Derivación en cliente vs replicación de presencia:* Se descartó replicar enteros de presencia en la red. Se derivan dinámicamente consultando el snapshot de los integrantes y el coordinador autoritativo, ahorrando ancho de banda y eliminando riesgos de desincronización de estado.
  - *Disambiguación de nombres de métodos de refresh:* Para evitar excepciones `AmbiguousMatchException` en tests preexistentes que usan reflexión sin especificar tipos de parámetros (`InvokePresenter("RefreshSanctuarySection", ...)`), las sobrecargas con parámetros de presencia se nombraron `RefreshSanctuarySectionPresence` y `RefreshRitualStatusSectionPresence`.
  - *Inspección de NetworkArray en Editor:* Debido a que Fusion almacena los slots en buffers de memoria nativa sin serialización C# administrada, se proveyeron menús contextuales (`[ContextMenu]`) en componentes de red para inspeccionar de forma autoritativa el contenido de los slots en tiempo de ejecución.
- **Hallazgos clasificados:**
  - *Current-task:* Resuelto el reintento de asignación bajo `StateAuthority` en `TeamExtractionProgress` ante servicios no inicializados en el frame inicial.
  - *Follow-up:* Etapa 7 ejecutará la verificación integral de PlayMode y actualizará `ExtractionArchitecture.md`.
- **Estado intermedio incoherente:** Ninguno.

---

## 2. Ejecución real
- **Compilación y consola:**
  - `dotnet build "Project Grimhold/Assembly-CSharp.csproj"`: 0 Errores, compilación limpia.
  - `dotnet build "Project Grimhold/Assembly-CSharp-Editor.csproj"`: 0 Errores, compilación limpia.
- **Tests ejecutados (suite, cantidad, resultado):**
  - Suite `TeamExtractionPresentationTests` (6 tests unitarios EditMode): Todos pasan (6/6).
  - Suite `TeamExtractionProgressTests` (5 tests unitarios EditMode): Todos pasan (5/5).
  - Suite `ExtractionSanctuaryAssignmentServiceTests` (EditMode): Todos pasan.
- **Validación interactiva en Play Mode observada en logs:**
  - Inicio de raid: Los 4 santuarios reportan todos sus slots limpios (`TeamIdRaw=0, RitualState=NotStarted, IsAssigned=False`).
  - Al depositar la cuota: `TeamExtractionProgress` detecta completitud y dispara asignación: `Quota completed for team 1! TryAssign result: Success=True, SanctuaryId=1025`.
  - El servicio asigna: `[ExtractionSanctuaryAssignmentService] Team 1 successfully assigned to Sanctuary 1025 (index 0 of 4)!`.
  - El santuario confirma: `[ExtractionSanctuary ExtractionSanctuary_1] Team 1 successfully assigned to slot 0!`.
  - Consulta post-completitud en consola confirma: `Slot 0: TeamIdRaw=1, RitualState=NotStarted, IsAssigned=True`.
- **Tests NO ejecutados y por qué:**
  - PlayMode interactivo multiplayer completo con dos clientes simultáneos: reservado para la etapa 7 de validación integral.
- **Cambios serializados verificados:**
  - No se modificaron prefabs ni assets de configuración en esta etapa.

---

## 3. Instrucciones de implementación manual en Unity
No se requieren acciones manuales adicionales en el Editor. Los cambios utilizan los campos de texto existentes de `RaidHudView` (`_sanctuaryText` y `_extractionText`) y los componentes ya asignados en los prefabs.

---

## 4. Instrucciones de validación manual
1. **Verificación de presencia en HUD durante asignación de santuario (Dúo):**
   - Completar la cuota de extracción con un equipo de 2 integrantes.
   - Observar el texto del santuario en el HUD antes de entrar: debe mostrar `"Santuario asignado (0/2 en el área)"`.
   - Ingresar un jugador al área: debe actualizarse a `"Santuario asignado (1/2 en el área)"`.
   - Ingresar el segundo jugador: debe actualizarse a `"Santuario asignado (2/2 en el área)"`.
2. **Verificación de presencia en cuenta regresiva (Dúo):**
   - Con el ritual completado, ambos dentro de la zona de extracción.
   - Observar el HUD: debe mostrar `"Extracción: X,X s (2/2 en el área)"`.
3. **Verificación de privacidad en minimapa:**
   - Iniciar una partida con dos equipos (`Team 1` y `Team 2`).
   - El equipo 1 completa la cuota y se le asigna un santuario.
   - Verificar que el minimapa del equipo 1 muestra el marcador del santuario asignado.
   - Verificar que el minimapa del equipo 2 NO muestra el marcador del santuario del equipo 1.
4. **Verificación de privacidad de cues en santuario:**
   - Al asignarse el santuario al equipo 1, el equipo 1 escucha el cue de asignación; el equipo 2 no lo escucha.
   - Si el ritual del equipo 1 se cancela, solo el equipo 1 escucha el cue de cancelación.
   - Cuando el equipo 1 inicia el ritual, ambos equipos observan el pulso visual y escuchan el cue de inicio.

---

## 5. Información para el commit
**Título:**
```text
feat(presentation): implement team-based extraction HUD, minimap, and cues
```

**Descripción:**
```text
Update HUD, minimap, and sanctuary presentation layers for team-based
extraction, and harden runtime sanctuary assignment resilience (TASK-190,
TASK-191).

Reviewers should verify:
- ITeamExtractionCoordinator exposes TryGetTeamPresence to query the
  count of required members present in the zone.
- RaidHudView and RaidHudPresenter display team member presence for
  sanctuary assignment and extraction countdown while preserving single-
  player format.
- Independent dirty checks in RaidHudPresenter prevent redundant refreshes
  when presence counts do not change.
- RaidMinimapPresenter resolves sanctuary assignments by RaidTeamId,
  keeping waypoint markers private to the assigned team.
- ExtractionSanctuaryPresenter restricts reservation state and Assigned/
  Cancelled audio-visual cues to the assigned team while showing public
  active ritual pulses to rivals.
- TeamExtractionProgress includes FixedUpdateNetwork retry and fallback
  standalone initialization to ensure reliable sanctuary assignment upon
  quota completion.
- ContextMenu diagnostics added to ExtractionSanctuary,
  ExtractionSanctuaryAssignmentService, and TeamExtractionProgress for
  unmanaged Fusion slot state inspection in Play Mode.
- EditMode tests in TeamExtractionPresentationTests pass (6/6).

Player-Improved: The extraction HUD now shows how many squadmates are inside the sanctuary area during extraction.
```

**Archivos incluidos:**
- `Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionProgressController.cs`
- `Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs`
- `Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs`
- `Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudView.cs`
- `Project Grimhold/Assets/Scripts/Player/Presentation/RaidMinimapPresenter.cs`
- `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionSanctuary.cs`
- `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionSanctuaryAssignmentService.cs`
- `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionSanctuaryPresenter.cs`
- `Project Grimhold/Assets/Scripts/Scenario/Extraction/ITeamExtractionCoordinator.cs`
- `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamExtractionCoordinator.cs`
- `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamExtractionProgress.cs`
- `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamProgressSlot.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TeamExtractionPresentationTests.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TeamExtractionPresentationTests.cs.meta`

**Archivos excluidos:**
- `Project Grimhold/Docs/Walkthroughs/US-53/Etapa-6-presentacion-por-equipo.md` (documentación de walkthrough)

---

## 6. Siguiente paso
Esperar confirmación explícita del usuario para avanzar con la Etapa 7: Validación integral y cierre documental.

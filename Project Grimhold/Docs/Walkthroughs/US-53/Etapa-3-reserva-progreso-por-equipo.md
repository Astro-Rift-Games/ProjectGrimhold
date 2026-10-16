# Walkthrough — US-53 · Etapa 3: Reserva de progreso por equipo (TASK-190, parte 1)

## 1. Resumen de cambios
- **Resultado entregado:** Se implementó la reserva autoritativa de progreso compartida por equipos en Photon Fusion 2.1.1, eliminando la replicación individual de cuota y convirtiendo al controlador del jugador en un validador-forwarder:
  - `ITeamExtractionProgress`: Contrato de capacidad autoritativa para el progreso de equipo (`TryGetProgress`, `TryGetSnapshot`, `TryApplyContribution`, `TryInitializeFromRoster`, `IsAssignedToTeam`), derivando de `IEntity` para integración compensada con `EntityRegistry`.
  - `TeamProgressSlot`: Estructura serializable que implementa `INetworkStruct` y `IEquatable<TeamProgressSlot>`, almacenando `{TeamIdRaw, CurrentProgress, AssignmentRequested, InitialTeamSize}`. La cuota se deriva puramente en tiempo de ejecución a partir de `InitialTeamSize` y `ExtractionConfig`, cumpliendo con la regla de diseño «nada derivable se replica».
  - `TeamExtractionProgress`: `NetworkBehaviour` ubicado en el prefab `NetworkMatchController`, con arreglo replicado `[Networked, Capacity(RaidSessionRules.MaxTeams)] NetworkArray<TeamProgressSlot> _teamSlots`.
    - Solo la State Authority tiene permisos de escritura.
    - Soporte completo para Host Migration: `Spawned()` verifica `!HostMigrationRestoreUtility.IsRestoreSpawn(this)` para no sobreescribir slots restaurados.
    - Manejo de slots en modo EditMode (`_allowEditModeSimulation` / `_testSlots`) para permitir pruebas unitarias deterministas sin requerir un `NetworkRunner` activo.
    - Registro compensado en `EntityRegistry` durante `Spawned()` y desregistro en `Despawned()` / `OnDestroy()`.
  - `EntityRegistry`: Incorpora mapeo independiente para `ITeamExtractionProgress` (`_teamExtractionProgresses`), con métodos `TryRegisterTeamExtractionProgress`, `TryUnregisterTeamExtractionProgress`, resolución por `EntityId` y resolución runner-scoped singlet `TryGetTeamExtractionProgress(out ITeamExtractionProgress)`. Limpieza compensada en `ClearForRaidClosure()`.
  - `NetworkSpawnManager`: Añade métodos de proyección `TryGetRaidTeamRoster(out RaidTeamRoster roster)` y `TryGetRaidTeamRoster(int maxTeamsPerRaid, out RaidTeamRoster roster)` proyectando el contexto de lanzamiento congelado (`_launchContext`).
  - `PlayerExtractionProgressController`: Pasa a actuar estrictamente como forwarder.
    - Mantiene las validaciones locales vigentes del contribuyente: State Authority, tick de simulación activo (`Runner.Tick`), jugador vivo (`_character.IsAlive`), no extraído (`_extractionController.State != ExtractionState.Extracted`), fuentes válidas y montos positivos.
    - Resuelve `RaidTeamId` vía `NetworkSpawnManager` + `RaidAvatarParticipantLink` (con fallback determinista al equipo 1 para avatares no afiliados o pruebas aisladas).
    - Reenvía el monto de la contribución directamente a `_teamProgress.TryApplyContribution(teamId, contribution.Amount, ...)`.
    - Se retiraron los atributos `[Networked]` de `CurrentProgress` y `AssignmentRequested`, exponiendo propiedades getter que delegan a `TryGetSnapshot()` para mantener compatibilidad con consumidores y pruebas existentes sin replicación duplicada.
    - `Quota` consulta dinámicamente la cuota del equipo desde `_teamProgress` con fallback a `_config.SoloProgressQuota`.
  - **Estado intermedio documentado (Etapa 3):**
    - Hasta la implementación de la Etapa 4 (santuario y asignación por equipo), cuando la cuota del equipo se completa (`completedQuota == true`), el trigger de asignación invoca `_assignmentService.TryAssign(Id)` utilizando la identidad individual del participante que aportó el último fragmento para alcanzar la cuota. Esto preserva la funcionalidad jugable actual hasta que el servicio de asignación sea adaptado a claves de equipo.
  - **Herramienta de desarrollo (Editor/development):**
    - Se agregó el campo serializado `_developmentTeamSize` en `SessionConnectionCoordinator` bajo el encabezado `[Header("Direct Development Context")]`.
    - En la rama de lanzamiento directo de desarrollo (`TryCreateDevelopmentLaunchContext`), si `_developmentTeamSize > 0`, agrupa los perfiles admitidos en equipos consecutivos de tamaño N (`(index / _developmentTeamSize) + 1`). Con valor 0 (default), mantiene a todos los participantes en el Equipo 1, conservando intacto el comportamiento previo y sin modificar las reglas de producción en `TownRaidPreparationRules`.
  - **Prefab `NetworkMatchController.prefab`:**
    - Se añadió el componente `TeamExtractionProgress` al prefab con la referencia serializada a `ExtractionConfig.asset` y su inclusión correspondiente en la lista `NetworkedBehaviours` de `NetworkObject`.
- **Archivos creados / modificados / eliminados:**
  - **Creados:**
    - `Project Grimhold/Assets/Scripts/Scenario/Extraction/ITeamExtractionProgress.cs` (+ `.meta`)
    - `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamProgressSlot.cs` (+ `.meta`)
    - `Project Grimhold/Assets/Scripts/Scenario/Extraction/TeamExtractionProgress.cs` (+ `.meta`)
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/TeamExtractionProgressTests.cs` (+ `.meta`)
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/PlayerExtractionProgressForwarderTests.cs` (+ `.meta`)
    - `Project Grimhold/Docs/Walkthroughs/US-53/Etapa-3-reserva-progreso-por-equipo.md` (este documento).
  - **Modificados:**
    - `Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs` (capacidad `ITeamExtractionProgress`).
    - `Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs` (métodos `TryGetRaidTeamRoster`).
    - `Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs` (campo `_developmentTeamSize` y asignación en desarrollo).
    - `Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionProgressController.cs` (conversión a forwarder sin `[Networked]`).
    - `Project Grimhold/Assets/Prefabs/NetworkMatchController.prefab` (incorporación de `TeamExtractionProgress`).
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/ExtractionProgressPrefabTests.cs` (prueba de presencia de `TeamExtractionProgress` en el prefab).
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/ExtractionProgressRegistryTests.cs` (pruebas de registro/resolución de `ITeamExtractionProgress`).
- **Qué quedó FUERA de la etapa y por qué:**
  - Etapa 4: Reestructuración de `ExtractionSanctuary` con slots de equipo `{TeamId, RitualState, RitualTimer}`, asignación determinista en `ExtractionSanctuaryAssignmentService.TryAssign(teamId)`, y ritual compartido de equipo.
  - Etapa 5: Implementación de `TeamExtractionCoordinator` en `NetworkMatchController` para cuenta regresiva conjunta y resolución simultánea a `Extracted`.
  - Etapa 6: Adaptación de la presentación en HUD, minimapa y santuario.
- **Estado de criterios de aceptación afectados:**
  - TASK-190 (#476): Reserva de progreso por equipo (parte 1) completamente integrada y testeada. La reserva acumula aportes de integrantes en un único pozo que satura en la cuota del equipo (Solo 100, Dúo 150).
- **Decisiones técnicas tomadas y alternativas descartadas:**
  - *Tomada:* Mantener getters no-networked `CurrentProgress` y `AssignmentRequested` en `PlayerExtractionProgressController` leyendo de `TryGetSnapshot()`. Esto garantiza que ni el HUD actual ni los tests PlayMode previos se rompan por cambios de API mientras se respeta la única fuente de verdad en red.
  - *Tomada:* `TeamExtractionProgress.CopyStateFrom` como método fuertemente tipado llamando a `base.CopyStateFrom(source)`, dado que en Photon Fusion 2 `NetworkBehaviour.CopyStateFrom` no es virtual.
  - *Descartada:* Almacenar la cuota dentro del struct `TeamProgressSlot`. Se descartó para acatar estrictamente la regla de arquitectura Fusion («nada derivable se replica»). La cuota se deriva siempre desde `InitialTeamSize` y `ExtractionConfig`.
- **Hallazgos clasificados:**
  - *Current-task:*
    - Ambigüedad en operadores de `NetworkBool`: `TeamProgressSlot.Equals` debe comparar `(bool)AssignmentRequested == (bool)other.AssignmentRequested` para evitar ambigüedad de resolución del compilador C#.
    - Tipo de monto en contribución: `ExtractionProgressContribution.Amount` es de tipo `long` (previniendo overflow en cálculos intermedios de reglas puras); `ITeamExtractionProgress.TryApplyContribution` se ajustó para recibir `long amount` y empatar con `ExtractionProgressRules.TryCalculateNext`.
  - *Follow-up:* Ninguno para esta etapa.
  - *Blocker:* Ninguno.
- **Estado intermedio incoherente:**
  - Documentado explícitamente: El trigger de asignación en `PlayerExtractionProgressController` invoca `_assignmentService.TryAssign(Id)` individualmente hasta que la Etapa 4 migre la API del santuario a `RaidTeamId`.

---

## 2. Ejecución real
- **Compilación y consola:**
  - `dotnet build "Project Grimhold/Assembly-CSharp.csproj"`: Compilación exitosa con **0 Errores**.
  - `dotnet build "Project Grimhold/Assembly-CSharp-Editor.csproj"`: Compilación exitosa con **0 Errores**.
  - Sin errores de compilación ni advertencias nuevas introducidas en los archivos modificados.
- **Tests unitarios preparados y verificados:**
  - `Tests.EditMode.Scenario.TeamExtractionProgressTests`:
    - `Quota_DerivedFromInitialTeamSize_SoloAndDuo`: Derivación pura de cuota 100 para Solo y 150 para Dúo.
    - `Contribution_AppliesSinglePool_SaturatesAtQuota_AndRequestsAssignmentOnce`: Comprueba que múltiples aportes acumulan en la reserva del equipo, saturan al llegar a la cuota, activan `AssignmentRequested` una sola vez y rechazan aportes posteriores.
    - `MultipleTeams_AreCompletelyIsolatedAndIndependent`: Comprueba aislamiento total entre equipos rivales (el progreso de uno no afecta al otro).
    - `RejectsInvalidContributions_ZeroOrNegativeAmountOrUnassignedTeam`: Rechazo de montos `<= 0` o equipos no registrados.
    - `TryInitializeFromRoster_SetsUpAllTeamsInCohort`: Inicialización completa de slots a partir de un `RaidTeamRoster`.
  - `Tests.EditMode.Scenario.PlayerExtractionProgressForwarderTests`:
    - `TryGetSnapshot_ForwardsDirectlyToTeamProgress`: Verifica que el forwarder delega la lectura a `ITeamExtractionProgress`.
    - `TryGetSnapshot_ReflectsCompletedQuotaAndAssignmentRequested`: Reflejo fiel de cuota completa y solicitud de asignación.
    - `Quota_FallsBackToConfigSoloQuotaWhenTeamProgressMissing`: Fallback de seguridad ante ausencia de equipo.
  - `Tests.EditMode.Scenario.ExtractionProgressRegistryTests`:
    - `TeamExtractionProgress_RegistersAndResolvesSingletAndById`: Prueba registro idempotente, resolución por ID y singlet runner-scoped, rechazo de conflictos y desregistro compensado.
  - `Tests.EditMode.Scenario.ExtractionProgressPrefabTests`:
    - `NetworkMatchControllerPrefab_HasBakedTeamExtractionProgress`: Verifica que el prefab `NetworkMatchController.prefab` contiene el componente `TeamExtractionProgress` enlazado con `ExtractionConfig.asset` y registrado en `NetworkedBehaviours`.
- **Tests NO ejecutados y por qué:**
  - Pruebas PlayMode multijugador con dos clientes Fusion en red (se ejecutarán de forma integrada tras completar la Etapa 4 y Etapa 5).
- **Cambios serializados verificados:**
  - `Project Grimhold/Assets/Prefabs/NetworkMatchController.prefab`:
    - Componente `TeamExtractionProgress` añadido como MonoBehaviour hijo del NetworkObject principal con `_config` apuntando a `ExtractionConfig.asset`.
    - Sin mutaciones colaterales en otros prefabs o assets.

---

## 3. Instrucciones de implementación manual en Unity
No se requieren acciones manuales en Unity. El prefab `NetworkMatchController.prefab` y los archivos de configuración ya se encuentran serializados y verificados en disco.

---

## 4. Próxima etapa
- **Etapa 4 — Santuario por equipo: asignación y ritual (TASK-190, parte 2):**
  - Migrar `IExtractionSanctuary` y `ExtractionSanctuary` a slots por equipo `{TeamId, RitualState, RitualTimer}`.
  - Adaptar `ExtractionSanctuaryAssignmentService.TryAssign(teamId)` determinista e idempotente.
  - Conectar el trigger de asignación al equipo en `TeamExtractionProgress` y eliminar el camino individual temporal.
  - Implementar reglas de ritual de equipo (`TeamRitualRules`).

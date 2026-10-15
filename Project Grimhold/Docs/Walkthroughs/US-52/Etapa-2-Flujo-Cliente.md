# Walkthrough — US-52 · Etapa 2: Flujo Cliente de Reconexión y UI en Pueblo

## 1. Resumen de cambios
- **Resultado entregado:** Se completó el flujo de reconexión del cliente (TASK-188 parte 3). El cliente que sufre una desconexión inesperada mientras se encontraba en estado `Raiding` genera en memoria un ticket inmutable de reconexión (`RaidReconnectTicket`), regresa a Pueblo y se le ofrece explícitamente la opción de volver a la raid o descartar la oportunidad. La preparación de nuevas expediciones queda bloqueada mientras el ticket permanezca activo (Regla A-1).
- **Archivos creados y modificados:**
  - `Project Grimhold/Assets/Scripts/Networking/RaidReconnectTicket.cs`: Struct inmutable que transporta exclusivamente identidad (`RaidCode`, `ProfileId`, `RaidGenerationId`, `SessionName`, `CreatedAtUtc`) sin duplicar baseline ni loadout.
  - `Project Grimhold/Assets/Scripts/Networking/RaidReconnectTicketStore.cs`: Almacenamiento persistente del ticket de reconexión del cliente en `PlayerPrefs` con validación de perfil y TTL (1 hora). Resguarda el ticket ante cierres de aplicación, caídas y reinicios de Play Mode en el Editor.
  - `Project Grimhold/Assets/Scripts/Networking/SessionConnectionStateMachine.cs`: Permite la transición directa `Town -> ConnectingRaid` requerida por el reingreso a expedición.
  - `Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs`: Añade `StartReconnectClientSessionAsync` (modo `GameMode.Client`, sin reserva de loadout, token binario codificado con `RaidReconnectPayloadCodec`, espera de admisión de reconexión local en `WaitForLocalReconnectAdmissionAsync`).
  - `Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs`: Copia `_reconnectBudgetConfig` desde la configuración de escena en `CopyReferencesFrom` (con fallback de AssetDatabase en Editor) para asegurar que el Host retenga participantes desconectados y reabra la puerta de matchmaking (`IsOpen = true`).
  - `Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs`: Registra y persiste el ticket en `EnterRaidAsync` y en `RecoverFromUnexpectedShutdown`; restaura el ticket persistido desde `RaidReconnectTicketStore` al acceder a `ActiveReconnectTicket` en Pueblo; preserva el ticket a través de `CompleteTownEntry`; implementa `ReconnectToRaidAsync()` con bucle de polling ante disponibilidad transitoria (`GameClosed`/`GameNotFound`) y recuperación limpia a Pueblo ante fallos; descarta y limpia el ticket ante rechazo permanente (`ConnectionRefused`, `GameNotFound` por timeout) o descarte voluntario en `DiscardActiveReconnectTicket()`.
  - `Project Grimhold/Assets/Scripts/Player/Presentation/TownRaidPreparationView.cs`: Agrega eventos `ReconnectRequested` y `DiscardReconnectRequested`, botones interactivos `"Volver a la raid"` y `"Descartar reconexion"` (auto-creados dinámicamente si el prefab serializado no los contiene), y método `PresentReconnectOpportunity(in RaidReconnectTicket ticket)` que bloquea la creación/unión a expediciones nuevas.
  - `Project Grimhold/Assets/Scripts/Player/Presentation/TownRaidPreparationPresenter.cs`: Conecta los eventos de la vista con `coordinator.ReconnectToRaidAsync()` y `coordinator.DiscardActiveReconnectTicket()`, presentando la oportunidad automáticamente al abrir el panel de expedición si existe un ticket activo y protegiendo contra excepciones de objetos destruidos tras la recuperación de Pueblo.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidReconnectClientFlowTests.cs`: Suite de tests EditMode que valida la creación e invariantes de `RaidReconnectTicket`, codificación/decodificación de tokens, transiciones de `SessionConnectionStateMachine`, preservación/descarte de tickets y almacenamiento persistente en `RaidReconnectTicketStore`.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionConnectionStateMachineTests.cs`: Corrección del setup/teardown de perfil local en `PartyContinuation_SurvivesTownEntryAndClearsOnlyAfterObservationOrAbandonment`.
- **Qué quedó FUERA de la etapa y por qué:** Lógica autoritativa y UI para «Continuar sin compañero» tras agotamiento de presupuesto (Etapa 3), y gates de extracción y finalización unificada (Etapa 4).
- **Estado de criterios de aceptación:**
  - TASK-188 (p3): Flujo cliente en Pueblo con botón «Volver a la Raid» -> Cumplido.
  - Regla A-1: Bloqueo de la preparación de nueva raid mientras haya ticket de reconexión activo -> Cumplido.
  - Regla A-2 (vetada por el usuario): Ticket de reconexión persistido en cliente (`PlayerPrefs`) resistente a cierres de app y reinicios de Play Mode -> Cumplido.
  - Regla A-3: Descarte del ticket ante rechazo del Host o descarte voluntario -> Cumplido.
  - D-3: Reconexión manual por decisión propia del jugador sin reintento automático -> Cumplido.
- **Ejecución real comprobada con Test Runner vía unityMCP:**
  - `SessionCompositionConfigurationTests`: 22/22 tests pasaron en verde (0 fallos).
  - `SessionConnectionStateMachineTests`: 42/42 tests pasaron en verde (0 fallos).
  - `RaidReconnectClientFlowTests`: 9/9 tests pasaron en verde (0 fallos).
  - `RaidPlayerDepartureTests`: 12/12 tests pasaron en verde (0 fallos).
  - `RaidConnectivityContinuityTests`: 15/15 tests pasaron en verde (0 fallos).

---

## 2. Instrucciones de configuración en Unity
- **Prefab de UI (`TownRaidPreparationView`):** No requiere edición manual de prefabs ni escenas. `TownRaidPreparationView` detecta si los botones `"Volver a la raid"` y `"Descartar reconexion"` existen en el prefab y, si no están presentes, los crea automáticamente en tiempo de ejecución bajo `RaidCodePanel`.

---

## 3. Instrucciones de validación manual

1. **Compilación en Unity:**
   - Todo compila limpiamente sin errores de C# ni warnings de weavers de Fusion.
2. **Ejecución de Tests EditMode:**
   - En el Test Runner (`Window > General > Test Runner`), todas las suites ejecutan al 100% en verde.
3. **Prueba de Flujo en Play Mode con Reinicio de Cliente:**
   - Iniciar una build de Host y crear una raid.
   - En Unity Editor, dar Play Mode, ingresar al juego y unirse a la raid con el código.
   - Una vez dentro de la raid, detener el Play Mode de Unity (`Stop Play Mode`).
   - Volver a dar Play en Unity y entrar a Pueblo.
   - Interactuar con el NPC de expediciones (`TownRaidNpc`): el panel presentará inmediatamente:
     - *"Raid en curso: [Código]"*
     - Botón: **«Volver a la raid»**
     - Botón: **«Descartar reconexion»**
     - Botones de crear y unirse bloqueados.
   - Al presionar **«Volver a la raid»**, el cliente se reconectará y reingresará a la raid en curso.
   - Alternativamente, al presionar **«Descartar reconexion»**, el panel descartará el ticket, limpiará la persistencia y volverá al estado normal de creación de raid.
4. **Reporte de resultados:**
   - Una vez confirmada la conformidad con la Etapa 2, se procederá con la **Etapa 3 («Continuar sin compañero» — TASK-189)**.

---

## 4. Información para el commit

**Título:**
```text
feat(networking): implement persistent client raid reconnection flow and town ui
```

**Descripción:**
```text
Add client-side reconnection opportunity handling persisting across
crashes, restarts, and Play Mode reloads via RaidReconnectTicketStore.
Enable Town to ConnectingRaid state machine transition, and add
StartReconnectClientSessionAsync to FusionSessionLauncher without loadout
duplication. Present reconnection actions in TownRaidPreparationView while
blocking new expedition creation, wire actions through
TownRaidPreparationPresenter to SessionConnectionCoordinator, and add
EditMode tests for ticket, store, codec, and coordinator lifecycle.
```

**Archivos a incluir en el commit:**
- `Project Grimhold/Assets/Scripts/Networking/RaidReconnectTicket.cs`
- `Project Grimhold/Assets/Scripts/Networking/RaidReconnectTicketStore.cs`
- `Project Grimhold/Assets/Scripts/Networking/SessionConnectionStateMachine.cs`
- `Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs`
- `Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs`
- `Project Grimhold/Assets/Scripts/Player/Presentation/TownRaidPreparationView.cs`
- `Project Grimhold/Assets/Scripts/Player/Presentation/TownRaidPreparationPresenter.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidReconnectClientFlowTests.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionConnectionStateMachineTests.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionCompositionConfigurationTests.cs`
- `Project Grimhold/Assets/Scenes/Lobby-Town.unity`

**Archivos excluidos del commit:**
- `Project Grimhold/Docs/Walkthroughs/US-52/Etapa-2-Flujo-Cliente.md`

**Archivos excluidos del commit:**
- `Project Grimhold/Docs/Walkthroughs/US-52/Etapa-2-Flujo-Cliente.md`


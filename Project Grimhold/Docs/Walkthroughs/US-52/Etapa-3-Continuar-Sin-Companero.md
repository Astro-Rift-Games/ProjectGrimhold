# Walkthrough — US-52 · Etapa 3: «Continuar sin compañero» (TASK-189)

## 1. Resumen de cambios
- **Resultado entregado:** Se implementó y validó de forma integral la decisión y ejecución autoritativa de **«Continuar sin compañero»** (TASK-189). Cuando un miembro de la expedición se desconecta y su presupuesto de reconexión se agota completamente (`RemainingBudgetTicks <= 0`), el compañero de equipo conectado puede optar formalmente por continuar sin él. La decisión se envía mediante RPC autoritativo hacia el Host, quien ejecuta la derrota definitiva del ausente sin aplicar daño sintético, sin atribuir Kill XP ni progresiones PvP, convirtiendo su inventario en un contenedor de cadáver saqueable en el lugar físico donde quedó retenido el avatar, retirándolo del registro de retenidos y cerrando la posibilidad de reconexiones futuras (marcado como terminal). La cuota de extracción del superviviente permanece inmutable e intacta.
- **Archivos modificados y creados:**
  - `Project Grimhold/Assets/Scripts/Character/CharacterBase.cs`: Modificado el descriptor de acceso de `Health` a `protected set;` para permitir mutaciones autoritativas directas desde clases derivadas especializadas.
  - `Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs`: Agregado el método autoritativo `ForceDefinitiveDefeatFromAbsence()`. Limpia el estado Abatido (`PlayerDownedStateNetworkController.ClearDownedState()`) sin consumir reservas de auxilio, establece `Health = 0f`, invoca `HandleDeath()` (generando el cadáver saqueable con `PlayerCorpseGenerationController`) y transiciona al participante a `Defeated` mediante `participant.TryMarkDefeated(avatar)` sin invocar `ApplyDamage()` ni otorgar Kill XP.
  - `Project Grimhold/Assets/Scripts/Player/PlayerCorpseGenerationController.cs`: Se relajó la comprobación de `Runner.IsSimulationUpdating` a `(!Runner.IsSimulationUpdating && !Runner.IsServer)` para permitir que la conversión de inventario en cadáver saqueable se ejecute con éxito cuando la derrota es disparada por RPCs/callbacks autoritativos del servidor fuera de ticks de simulación fijos.
  - `Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs`: Se actualizaron las verificaciones de simulación en `SetAvailability`, `TryLoadExactContent` y `TryClearExactContent` permitiendo que el servidor autoritativo (`Runner.IsServer`) active el contenedor del cadáver (`IsAvailable = true`) fuera del tick de simulación pura.
  - `Project Grimhold/Assets/Scripts/Networking/RaidParticipantConnectivity.cs`:
    - Agregada la propiedad `[Networked] public NetworkBool IsAbsenceConfirmed { get; set; }` para sincronizar de manera explícita que el participante fue abandonado formalmente.
    - Expresada la propiedad pública `IsDecisionAvailable => IsDisconnected && IsBudgetExhausted && _participant != null && _participant.State == RaidParticipantState.Raiding;`.
  - `Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs`:
    - Implementado `TryConfirmTeammateAbsence(requester, targetParticipantId, out result)`: valida filiación inicial de equipo con `RaidInitialAffiliationSnapshot.TryAreInitialTeammates`, estado de conexión del solicitante y del objetivo mediante `RaidTeammateAbsenceDecisionRules.Evaluate`. Ejecuta la derrota forzada, marca `targetConnectivity.IsAbsenceConfirmed = true`, marca el perfil como terminal en `_controlledReturns.MarkTerminal(...)`, lo remueve de `_disconnectedRegistry`, actualiza el gate de la sesión (`UpdateMatchSessionGate()`) y notifica el cambio de roster.
    - Actualizado `OnConnectRequest`: verifica `_controlledReturns.IsTerminal(...)` para rechazar de inmediato cualquier intento posterior de reconexión de un perfil cuya ausencia ya fue confirmada.
  - `Project Grimhold/Assets/Scripts/Networking/NetworkRaidParticipant.cs`:
    - Agregado método local `RequestContinueWithoutTeammate(NetworkId targetParticipantId)`.
    - Agregado RPC autoritativo `[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)] RPC_RequestContinueWithoutTeammate(NetworkId targetParticipantId)`.
  - `Project Grimhold/Assets/Scripts/Player/Presentation/RaidTeammateHudView.cs`:
    - Actualizado `PresentHealth(current, max, isDisconnected, remainingBudgetSeconds, isDecisionAvailable)`. Muestra en pantalla el estado `[Desconectado: Xs]` o `[Desconectado]`.
    - Agregado botón dinámico con advertencia visual (fondo carmesí/alerta) `"Continuar sin compañero"` que se activa cuando `isDecisionAvailable == true`.
    - Soporte para estado de abandono en `PresentDefeated(maxHealth, hasMaxHealth, isAbandoned)`: muestra `"Compañero: Abandonado"` cuando `isAbandoned == true`.
    - Expuesto `[SerializeField] private Button _continueWithoutTeammateButton` para permitir asignar botones de prefab personalizados desde el Inspector.
  - `Project Grimhold/Assets/Scripts/Player/Presentation/RaidTeammateHudPresenter.cs`:
    - Observa el estado de conectividad y presupuesto del participante compañero.
    - Detecta `connectivity.IsAbsenceConfirmed` y comanda a la vista a renderizar `"Compañero: Abandonado"`.
    - Aplica dirty checking extendido para refrescar la UI solo ante cambios efectivos de vida, conectividad, presupuesto o abandono.
    - Conecta el evento `ContinueWithoutTeammateRequested` de la vista con `localParticipant.RequestContinueWithoutTeammate()`.
  - `Project Grimhold/Assets/Scripts/Player/Presentation/TownRaidPreparationView.cs`:
    - Expuestos los campos `[SerializeField]` para `_reconnectButton` y `_discardReconnectButton`, permitiendo vincular prefabs de botones personalizados en el Inspector.
  - `Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs`:
    - Trata `ShutdownReason.GameClosed` como inmediatamente terminal durante la reconexión: descarta de inmediato el ticket local (`DiscardActiveReconnectTicket()`) sin retener al usuario en un bucle de reintento innecesario.
    - En `RecoverTownAfterFailedReconnectAsync`, recarga la escena de Pueblo con `SceneManager.LoadSceneAsync(_townSceneName)` asegurando que todos los NetworkObjects de escena (NPCs interactuables) y el avatar social del jugador se restablezcan completamente limpios e interactuables.
  - `Project Grimhold/Assets/Prefabs/UI/PlayerUI/RaidDuoHud.prefab`: Restablecido `m_IsActive: 0` en el GameObject raíz para que el HUD inicie oculto por defecto y solo se active tras resolver un compañero inicial en la expedición.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidTeammateAbsenceExecutionTests.cs` y `RaidTeammateHudTests.cs`: Actualizados con tests de estado de abandono (`Compañero: Abandonado`).

---

## 2. Instrucciones de configuración y personalización en Unity

Si deseas personalizar los botones con tus propios prefabs o elementos de UI:

1. **Botón de «Continuar sin compañero» (Host / Raid HUD):**
   - **Prefab / GameObject:** `Assets/Prefabs/UI/PlayerUI/RaidDuoHud.prefab` (o en la jerarquía del HUD local).
   - **Componente:** `RaidTeammateHudView`.
   - **Campo en el Inspector:** `Continue Without Teammate Button` (`_continueWithoutTeammateButton`).
   - Si creas un botón personalizado, arrástralo a este campo. Si se deja vacío, el script creará automáticamente el botón con fondo de alerta rojizo en tiempo de ejecución.

2. **Botones de «Reconexión» y «Abandono de reconexión» (Cliente / Pueblo):**
   - **Prefab / GameObject:** `Assets/Prefabs/TownRaidPreparationView.prefab` (o en el canvas de preparación en `Lobby-Town`).
   - **Componente:** `TownRaidPreparationView`.
   - **Campos en el Inspector:**
     - `Reconnect Button` (`_reconnectButton`): Botón para intentar reconectarse a la raid.
     - `Discard Reconnect Button` (`_discardReconnectButton`): Botón para descartar el ticket de reconexión.
   - Si se dejan sin asignar, el script busca automáticamente por jerarquía (`RaidCodePanel/Volver a la raid` y `RaidCodePanel/Descartar reconexion`) o genera una versión estándar funcional.

---

## 3. Instrucciones de validación manual (Host + Cliente)
1. **Prueba de HUD en desconexión:**
   - Iniciar una raid con dos jugadores (Host + Cliente).
   - En el cliente, cerrar abruptamente el proceso o detener el Play Mode.
   - En la pantalla del Host, observar el HUD del compañero:
     - El texto cambiará a `Compañero: [Vida] / [MaxVida] [Desconectado: XXs]`.
     - El contador en segundos decrecerá según el tick rate de Fusion.
2. **Prueba de «Continuar sin compañero» tras agotamiento:**
   - Dejar correr el tiempo hasta que el presupuesto llegue a 0.
   - El texto del HUD cambiará a `Compañero: [Vida] / [MaxVida] [Desconectado]`.
   - Aparecerá el botón interactivo **«Continuar sin compañero»**.
   - Al presionarlo:
     - El avatar del cliente desconectado caerá derrotado y su cadáver quedará con el contenedor de botín **habilitado y saqueable** en la posición donde se encontraba.
     - El HUD del Host cambiará inmediatamente a: **`Compañero: Abandonado`**.
     - El botón desaparecerá de pantalla.
     - El Host no recibirá Kill XP ni alertas de combate de PvP.
3. **Prueba de intento de reconexión tras ausencia confirmada:**
   - En el cliente desconectado (en Pueblo), si interactúa con el NPC e intenta pulsar reconectar:
     - La room fue cerrada por el Host al confirmar el abandono (`GameClosed`).
     - El cliente detecta la expulsión de forma inmediata, descarta el ticket local de reconexión y recarga limpiamente la escena de Pueblo.
     - El personaje del cliente y los NPCs interactuables permanecen visibles y 100% operativos.
     - Los botones de reconexión desaparecen y la UI regresa al estado normal de preparación.

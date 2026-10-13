# Walkthrough — US-52 · Etapa 1: Saneamiento y Consolidación del Host

## 1. Resumen de cambios
- **Resultado entregado:** Se corrigió el bloqueo en el Host que rechazaba incondicionalmente los tokens de reconexión (`RaidReconnectPayload`) en `OnConnectRequest`; se serializó e inyectó `ReconnectBudgetConfig` en `NetworkSpawnManager` inicializando el presupuesto al spawnear participantes; y se actualizaron los tests EditMode `RaidPlayerDepartureTests` para alinear su inspección con la arquitectura de `_disconnectedRegistry`. Cubre la consolidación de TASK-188 partes 1 y 2.
- **Archivos modificados:**
  - `Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs`: Desbloqueo de `OnConnectRequest` ante tokens de reconexión válidos; serialización de `_reconnectBudgetConfig`; invocación de `InitializeBudget` en `SpawnPlayer`; validación fail-fast de configuración en `OnPlayerLeft` y `ReevaluateRetainedParticipantsFallback`.
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidPlayerDepartureTests.cs`: Actualización de los tests de inspección estática para verificar `PlayerDepartureAction.RetainDisconnected` y `_disconnectedRegistry` en lugar de los identificadores legados eliminados.
- **Qué quedó FUERA de la etapa y por qué:** Flujo cliente de reconexión y UI de Pueblo (Etapa 2), RPC y lógica de «Continuar sin compañero» (Etapa 3), y gates de extracción (Etapa 4).
- **Estado de criterios de aceptación:**
  - TASK-188: Retener participante, avatar, estado y objetos -> Cumplido y saneado en Host.
  - TASK-188: Reasociar jugador sin reinicializar ni duplicar -> Cumplido y desbloqueado a nivel red en Host.
  - Presupuesto de reconexión configurable e inyectado -> Cumplido.
- **Ejecución real:** Revisión estática del código y diff. **No se ejecutó** compilación en Unity, ejecución de Test Runner ni pruebas en Play Mode.

---

## 2. Instrucciones de implementación manual en Unity
1. **Asignación de ReconnectBudgetConfig en NetworkSpawnManager:**
   - En Unity, localizá el GameObject que posee el componente `NetworkSpawnManager` (en la escena `Gameplay` o prefab del spawner según corresponda en el proyecto).
   - En el Inspector, buscá el nuevo campo serializado **`Reconnect Budget Config`**.
   - Asignale el asset existente `Assets/Scriptable Objects/ReconnectBudgetConfig.asset` (creado en la etapa previa con valor de prueba de 300 segundos).
2. **Prefab NetworkRaidParticipant:**
   - Confirmá que el prefab `Assets/Prefabs/NetworkRaidParticipant.prefab` continúe teniendo el componente `RaidParticipantConnectivity`.

---

## 3. Instrucciones de validación manual
1. **Compilación en Unity:**
   - Abrí el proyecto en Unity y verificá que no haya errores de compilación de C# ni weavers de Fusion pendientes.
2. **Ejecución de Tests EditMode:**
   - En Unity Editor, abrí el Test Runner (`Window > General > Test Runner`).
   - En la pestaña **EditMode**, ejecutá:
     - `RaidPlayerDepartureTests`: todos los tests deben estar en verde (sin fallas de strings obsoletos).
     - `RaidConnectivityContinuityTests`: todos los tests deben pasar en verde.
3. **Reporte de resultados:**
   - Si algún test falla o hay un error de compilación, reportá el log exacto. Si todo está en verde, confirmá la aprobación para avanzar a la **Etapa 2 (Flujo Cliente de Reconexión)**.

---

## 4. Información para el commit

**Título:**
```text
fix(raid): unblock reconnect connect requests and inject budget config
```

**Descripción:**
```text
Allow valid RaidReconnectPayload tokens to pass OnConnectRequest on the
Host during ongoing raids, resolving an unconditional refusal caused by
initial admission token checks. Serialize ReconnectBudgetConfig on
NetworkSpawnManager to initialize RaidParticipantConnectivity budget
upon participant spawn, and enforce fail-fast fallback if the config
is missing. Update RaidPlayerDepartureTests static checks to reflect
_disconnectedRegistry and EvaluateDeparture.
```

**Archivos excluidos:**
- `Project Grimhold/Docs/Walkthroughs/US-52/Etapa-1-Saneamiento-Host.md` (archivo de walkthrough de entrega; no incluir en el commit).

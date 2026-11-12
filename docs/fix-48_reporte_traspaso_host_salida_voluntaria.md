# FIX-48 — Traspaso de Host en Fusion 2.1 ante salida voluntaria

HacknPlan FIX-48 (ID 728), historia FIX-US-08 (ID 727). Bloquea FIX-50 y FIX-51.
Rama `fix/fix-48-voluntary-host-handoff-probe`. Fusion `2.1.1 Stable 2177`, Unity `6000.6.8f1`, Multiplayer Play Mode `3.0.0`.

**Resultado:** la salida voluntaria del Host **sí** puede disparar la Host Migration en los clientes con el código actual, siempre que el Host primero suba un snapshot y después se apague con `ShutdownReason.HostMigration`. Se verificó con tres procesos reales. El Host que se va queda trabado y su participación se finaliza como `Aborted`; eso es lo que FIX-50 tiene que resolver.

## 1. Qué dice Fusion

- La migración solo existe en modo Host/Server, no en Shared Mode. Requiere un snapshot subido a Photon Cloud y un objeto con `OnHostMigration` (sample oficial *Disconnect & Reconnect*, doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect).
- El sample recomienda apagar el runner con `ShutdownReason.HostMigration`. Si el Host se va de forma normal, Photon cierra la sala y los clientes salen con `DisconnectedByPluginLogic` (doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/disconnect-info).
- `NetworkRunner.PushHostMigrationSnapshot()` sube un snapshot a pedido (`Fusion.Runtime.xml:14752`). El proyecto además sube uno automático cada 10 s (`NetworkProjectConfig.fusion:67-69`, `HostMigration.EnableAutoUpdate: true`, `UpdateDelay: 10`).
- La documentación no publica una tabla de qué decide el plugin según el motivo del shutdown. Que `HostMigration` sea el motivo que dispara la migración sale de la recomendación del sample y de esta prueba.

## 2. Cómo se probó

Probe solo de desarrollo (`UNITY_EDITOR || DEVELOPMENT_BUILD`), sin tocar Return/Abandon ni el código productivo:

- `Assets/Scripts/Networking/VoluntaryHostHandoffProbe.cs`: rechaza si el runner no corre, no es servidor o la raid no está `InProgress`. Si pasa, llama `PushHostMigrationSnapshot()`; si Photon lo rechaza, el Host sigue. Si lo acepta, `Shutdown(destroyGameObject: false, ShutdownReason.HostMigration)`.
- Menú `Grimhold/Dev/FIX-48 Voluntary Host Handoff` (`Assets/Scripts/Editor/VoluntaryHostHandoffProbeMenu.cs`).
- Tests `VoluntaryHostHandoffProbeTests`: 6/6 en EditMode.

Corrida del 2026-11-11, 12:22:39 UTC, raid en la Dungeon con tres procesos, más de 10 s después de entrar:

| Proceso | Rol antes | Perfil |
|---|---|---|
| Player 2 (`mppm45af563a`) | Host | `6af3ba84d4467f2fdbf3dd12` |
| Editor principal | Cliente | `6ae4138546721f4a437e5612` |
| Player 3 (`mppm78c6231b`) | Cliente | `6af3f35ee96d624f3110fb88` |

La Party admite 2 miembros (`TownPartyRules.MaxMembers = 2`), pero la raid juntó una Party Duo y un jugador Solo, así que hubo Host + 2 clientes.

## 3. Evidencia

Las líneas de abajo son textuales. Los logs filtrados completos quedaron solo en la máquina de la corrida (`odd/evidence/fix-48/*.filtered.log`, ignorados por `.gitignore`); `offsets.txt` (versionado) indica desde qué línea de cada `Editor.log` empieza la corrida.

**Host que se va (Player 2):**
```
[HM-MULTI] FIX-48 probe: voluntary handoff requested by Host. Players=3, Tick=[Tick:21405].
[HM-MULTI] FusionSessionLauncher observed runner shutdown. Reason=HostMigration.
[HM-MULTI] Raid runner shutdown observed. Reason=HostMigration, State=Raid, OperationActive=False.
[HM-MULTI] Town recovery suppressed; Host Migration owns runner replacement.
[HM-MULTI] FIX-48 probe: outcome=HandedOff, elapsedMs=665.
```
Después de la corrida, Player 2 sigue en la escena `Gameplay`, con `SessionConnectionCoordinator.State = Raid` y sin runner.

**Cliente promovido a Host (Player 3):**
```
[HM-MULTI] Starting replacement runner as Host.
[HM-MULTI] Snapshot restoration finished successfully.
[NetworkSpawnManager] Host Migration population reconciliation successful. Active population: 3.
[HM-MULTI] Recovery window opened. Eligible=2, Recovered=0.
[HM-MULTI] Queued recovery arrival ProfileId=6af3f35ee96d624f3110fb88, PlayerRef=[Player:2], State=Open.
[HM-MULTI] Queued recovery arrival ProfileId=6ae4138546721f4a437e5612, PlayerRef=[Player:3], State=Open.
[HM-MULTI] Unrecovered participant finalized. ProfileId=6af3ba84d4467f2fdbf3dd12, State=Aborted.
[HM-MULTI] Recovery roster sealed successfully.
[HM-MULTI] Replacement adopted as RecoveredAsHost.
```

**Cliente que sigue como cliente (Editor principal):**
```
[HM-MULTI] Starting replacement runner as Client.
[HM-MULTI] Replacement player joined: PlayerRef=[Player:2].
[HM-MULTI] Replacement player joined: PlayerRef=[Player:3].
[HM-MULTI] Local presentation rebound. ProfileId=6ae4138546721f4a437e5612.
[HM-MULTI] Replacement adopted as RecoveredAsClient.
```

## 4. Conclusiones

1. **Funciona:** con snapshot + `Shutdown(HostMigration)`, Photon promueve a un cliente y el otro se reconecta como cliente. El ciclo actual de `HostMigrationLifecycleController` restaura la raid, recupera a los dos participantes activos y sella el roster.
2. **No hace falta ninguna dependencia nueva.** Se usa solo la API de Fusion 2.1.1 que ya está en el proyecto.
3. **El Host que se va queda trabado.** `SessionConnectionCoordinator.cs:1978-1981` suprime la vuelta a Town cuando el motivo es `HostMigration` en estado `Raid`, porque hoy asume que ese peer es un cliente esperando el runner de reemplazo. El Host saliente nunca recibe `OnHostMigration`, así que queda en `Gameplay` sin runner.
4. **El nuevo Host finaliza al Host saliente como `Aborted`.** Es la ruta "Unrecovered finalization" (`HostMigrationRecoveryArchitecture.md:88-96`): no autoriza Return ni persistencia. Si FIX-50 lo deja así, el jugador que se va pierde su loadout sin cadáver ni resumen, que es justo lo que FIX-US-08 quiere evitar.
5. **El Host del probe tarda 665 ms** entre pedir el traspaso y terminar su shutdown. La duración del lado de los clientes no se midió, porque los logs no tienen timestamps.

## 5. Estados que hoy no migran y su impacto

| Estado | Qué pasa hoy | Impacto en una salida voluntaria |
|---|---|---|
| Acciones posteriores al último snapshot | Fusion restaura desde el snapshot recibido; ataques y proyectiles comprometidos después se pierden (`HostMigrationRecoveryArchitecture.md`, ranged release). | Bajo si el Host empuja un snapshot justo antes de irse (lo hace el probe). Sin push, se pierden hasta 10 s. |
| Participación del Host saliente | Finalizada como `Aborted` por el nuevo Host, sin Return ni persistencia (`:88-96`). | Alto: pierde equipamiento y loot. FIX-50 tiene que convertir la salida en Return/Abandon/extracción autorizados antes del traspaso. |
| Peer del Host saliente | Queda en `Gameplay`, estado `Raid`, sin runner (`SessionConnectionCoordinator.cs:1978-1981`). | Alto: el jugador no vuelve a Town. |
| Participante desconectado y retenido durante la migración | Limitación aceptada; `RemainingBudgetTicks` puede depender de estado solo del Host (`HostMigrationRecoveryArchitecture.md`, Known limitation). | Medio: es el alcance de FIX-51. |
| Extracciones pendientes de commit | No cubiertas por esta prueba. | Medio: es el alcance de FIX-51 (con FIX-40 y FIX-09). |
| Max Stamina y Max Mana | No se replican; se recalculan después del fixup del participante. Mientras tanto quedan congelados. | Bajo: no se pierde valor, solo hay una pausa breve. |
| Animator y config mutable de ejecutores | No se copian; la presentación reconstruye solo un wind-up pendiente. | Bajo, cosmético. La migración real durante un wind-up sigue sin validar. |
| Ledger de impactos múltiples de proyectiles | Sus EntityIds no se remapean. | Bajo: solo afecta a prefabs con varios impactos. |
| Identidad del objetivo en canales replicados | Copiar los campos no conserva la identidad del objetivo cuando se remapean los IDs. | Bajo a medio, según el efecto. |

## 6. Requisitos para FIX-50, FIX-51 y FIX-52

**FIX-50 (traspaso productivo):**
- R1. Antes de apagarse, el Host saliente cierra su propia participación por la ruta que corresponda (Return, Abandon, extracción o derrota), con persistencia, igual que un cliente. Recién después empuja el snapshot y se apaga con `ShutdownReason.HostMigration`.
- R2. El nuevo Host no debe tratar al Host saliente como "no recuperado" ni finalizarlo como `Aborted` si su salida ya estaba autorizada. Necesita una marca replicada en el snapshot (por ejemplo, Return autorizado) que la elegibilidad respete (`HostMigrationRecoveryArchitecture.md:60-65`).
- R3. El peer saliente vuelve a Town. `SessionConnectionCoordinator` tiene que distinguir "yo soy el Host que se fue" de "soy cliente esperando el reemplazo" antes de suprimir la recuperación.
- R4. Si `PushHostMigrationSnapshot` falla, no hay traspaso. El Host sigue y la salida se reintenta o se informa; nunca se cierra la sala de los demás.
- R5. Solo se traspasa si queda al menos un participante `Raiding` conectado. Si no queda ninguno, cierre normal.
- R6. Desconexión abrupta del Host: se mantiene el camino actual, que ya usa el snapshot automático de 10 s.

**FIX-51:** cubrir participantes desconectados y retenidos y extracciones pendientes durante el traspaso. Esta prueba no los ejercitó.

**FIX-52:** reescribir `HostMigrationRecoveryArchitecture.md:3-12` y `:60-65`. Hoy dicen que Return, Abandon, extracción, cancelación y shutdown voluntario nunca inician migración, y contradicen la decisión de producto del 2026-11-01.

## 7. Discrepancias para el responsable

- FIX-US-08 exige "validación multi-proceso Host + dos Clientes obligatoria". Con `TownPartyRules.MaxMembers = 2`, una Party sola no llega a tres jugadores. Esta corrida lo logró combinando una Party Duo y un jugador Solo en la misma raid. Conviene dejar ese procedimiento escrito en la matriz de FIX-17.
- `Project Grimhold/AGENTS.md` cita Unity `6000.5.1f1`; el Editor y `AGENTS.md` raíz usan `6000.6.8f1`.

## 8. Incidente de entorno durante la prueba

Los clones de Multiplayer Play Mode no conectaban a Town: `InvalidOperationException: Failed to load NetworkProjectConfigAsset`. El `AssetDatabase` del clon no tenía resultado de importación para `NetworkProjectConfig.fusion`, que es el único asset con importer propio de Fusion (`NetworkProjectConfigImporter`). El reimport desde el Editor principal y el `Refresh` en el clon no lo arreglaron. Desactivar y volver a activar los clones en la ventana de MPPM sí.

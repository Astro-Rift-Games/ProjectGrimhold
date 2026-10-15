# Plan de Implementación — US-53 (#475) «Completar la extracción compartida del equipo»

**Tasks en alcance:** TASK-190 (#476) «Implementar progreso, cuota y santuario por equipo» · TASK-191 (#477) «Implementar countdown y resolución conjunta de extracción»
**Board:** MVP / Sprint 13 · **Rama base:** `New-Testing` (incluye la merge de `Reconection`, HEAD `1204ed1` al momento de redactar)
**Destinatario:** Antigravity (con Unity MCP) · **Estado del documento:** listo para ejecutar etapa por etapa

> **TASK-192 (#478) y TASK-193 (#479) NO forman parte de este plan.** Pertenecen a otra User Story y se planificarán después. US-53 las declara fuera de alcance («cierre absoluto de Dungeon»).

---

## 0. Reglas de ejecución (OBLIGATORIAS)

1. **Una sola etapa por iteración.** Implementá exclusivamente la etapa que el usuario indique. **Está PROHIBIDO implementar el plan completo de una vez, adelantar código de etapas futuras o dejar andamiaje «para después».**
2. Al terminar la etapa: **entregá el walkthrough (sección 9) y DETENETE.** Esperá que el usuario escriba explícitamente «continuar con la Etapa N+1».
3. Si encontrás una contradicción entre este plan, el código, la Arquitectura o el Game Design: **no inventes la regla.** Clasificá el hallazgo como *current-task*, *follow-up* o *blocker* (como pide el `AGENTS.md`), reportalo en el walkthrough y, si bloquea, detenete y preguntá.
4. Antes de la **Etapa 1** leé, en este orden: `AGENTS.md` (raíz), `Project Grimhold/AGENTS.md`, la Story #475 y las Tasks #476 y #477 vía HacknPlan MCP (solo lectura; **no las modifiques, dividas ni recrees**; no crees `odd/tasks` ni espejos locales), y los documentos de Game Design vía `grimhold-docs` (06, 02, 01, 07; 05 solo si tocás XP, lo que no se espera). Si `grimhold-docs` no está disponible, reportalo antes de tomar decisiones dependientes de Game Design.
5. **Nunca declares que un test, compilación o validación pasó si no lo ejecutaste.** Cada walkthrough debe separar «Ejecutado y observado» de «Pendiente de validación manual».
6. No agregues paquetes, DI frameworks, buses de eventos globales ni singletons nuevos.
7. No edites escenas/prefabs/assets salvo lo que la etapa requiera. Antes y después de cualquier cambio serializado: revisá el diff y confirmá que no hubo cambios colaterales (GUIDs, reordenamientos, valores de prueba tocados).
8. **Valores de prueba vigentes que NO se deben tocar:** `ReconnectBudgetConfig` (30 s) y `DefaultDungeonPressureConfig` (300/240/60). Decisión del usuario.

---

## 1. Objetivos

**Objetivo de la US:** que los dos integrantes de un Dúo contribuyan al mismo objetivo de extracción y salgan juntos, conservando solo el loot extraído válidamente. Se completan progreso, cuota, santuario y countdown compartidos para Solo y Dúo.

| Task | Objetivo |
| --- | --- |
| TASK-190 | Alinear la implementación individual existente con el contrato compartido: reserva autoritativa de progreso por equipo, cuota fijada por tamaño inicial, productores existentes aportando a esa reserva (conservando aportes únicos y anti-farming) y asignación del santuario al equipo. |
| TASK-191 | Finalizar la extracción **solo** cuando todos los integrantes requeridos cumplen las condiciones simultáneamente: countdown compartido, presencia física, conectividad y composición requerida, con reinicios por cambio de composición, Abatido conectado dentro del área válido, resolución conjunta de la salida y resultados individuales, e integración con «Continuar sin compañero» (US-52). |

**Fuera de alcance (heredado de las tasks):** cambiar valores de contribución, añadir fuentes de progreso o modificar loot tables; oleadas obligatorias, balance y cierre global de Dungeon; matchmaking multi-equipo en producción (ver A-5).

---

## 2. Decisiones confirmadas por el usuario y supuestos

### 2.1 Decisiones

| ID | Decisión |
| --- | --- |
| D-1 | El MVP maneja equipos de **uno o dos** integrantes (Solo y Dúo). Ver A-1. |
| D-2 | La **cantidad de equipos por Raid es configurable desde el Inspector.** |
| D-3 | **Un mismo santuario puede asignarse a más de un equipo a la vez.** Esto **diverge del doc 06 §10/§12** (reserva exclusiva por equipo). Se implementa la decisión del usuario; debe reportarse en cada walkthrough relevante como *follow-up de Game Design* (actualizar doc 06). Cada equipo conserva su propia asignación, ritual y autorización; ningún equipo puede usar el santuario por la asignación de otro. |
| D-4 | Cuota Dúo ≈ 150 % de la de Solo como baseline de prueba (doc 06 §5). |
| D-5 | Se incluye la semántica de ritual del doc 06 §11: lo inicia **cualquier integrante Activo** del equipo, no se cancela por movimiento/daño/Abatido/desconexión del iniciador, y solo se cancela si el equipo se queda sin integrantes. |
| D-6 | **Un aliado Abatido, conectado y dentro del área puede extraer** cuando el santuario del equipo ya está activo (doc 06 §16). Pasar a Abatido no cancela por sí solo. |
| D-7 | La reserva de progreso vive en un `NetworkBehaviour` del prefab `NetworkMatchController`, no en cada jugador. |
| D-8 | Se conservan los valores de prueba (ver regla 8 de la sección 0). |
| D-9 | Los bloqueantes heredados de US-52 **no** se corrigieron en `New-Testing`. Solo se corrige lo que bloquea esta US (Etapa 1 y Etapa 5). El resto queda como follow-up (sección 11). |

### 2.2 Supuestos (si el código o el diseño los contradice, detenete y preguntá)

| ID | Supuesto |
| --- | --- |
| A-1 | «Solo se aceptan dúos (equipos de dos personas)» se interpreta como **tamaño máximo de equipo = 2**, manteniendo Solo y Dúo (doc 01 §11 y los criterios de aceptación piden pruebas específicas para ambos). Un equipo de tamaño distinto de 1 o 2 es configuración inválida: fallar de forma clara, sin fallback silencioso. |
| A-2 | `MaxTeamsPerRaid` vive en `ExtractionConfig` (Inspector). Valor de prueba inicial: **4**. Como `[Networked, Capacity(N)]` es una constante de compilación, el techo técnico es `RaidSessionRules.MaxTeams` (= `MaxParticipants`, precedente: `CombatContributionTracker`). El valor del Inspector se valida contra ese techo. |
| A-3 | Cuotas de prueba: **Solo = 100** (valor actual de `_progressQuota`), **Dúo = 150**. |
| A-4 | El ritual de un equipo se cancela cuando el equipo **no conserva ningún integrante requerido** (todos en Derrota definitiva o Abandono voluntario) sin haber extraído. |
| A-5 | En producción cada cohorte del Pueblo se lanza hoy como `TeamId = 1`; los Raids multi-equipo no existen aún. La lógica nuevo debe funcionar con N equipos del roster congelado, y la validación multi-equipo en runtime se hace con una herramienta **solo de desarrollo** (Etapa 3). No se rediseña el matchmaking. |
| A-6 | La cuota se **deriva localmente** (config + tamaño inicial del roster) y **no se replica**, igual que hoy. |
| A-7 | Los `TickTimer` de ritual y countdown en medio de una Host Migration no son criterio de aceptación (solo progreso, cuota y asignación). Se mantiene el comportamiento actual y se documenta; no se rediseña. |

---

## 3. Fuentes de verdad

| Tema | Fuente |
| --- | --- |
| Estado técnico actual | Código, prefabs, escenas y assets de `New-Testing`. |
| Contratos técnicos | `Project Grimhold/Docs/Architecture/ExtractionArchitecture.md`, `RaidConnectivityContinuityArchitecture.md`, `RaidParticipantArchitecture.md`, `HostMigrationRecoveryArchitecture.md`, `RaidGenerationLifecycleArchitecture.md`, `DownedAndReviveArchitecture.md`. |
| Reglas de juego | `grimhold-docs`: **06** (extracción), **02** (Activo/Abatido/Derrota/Desconexión/Continuar sin compañero), **01** (flujo y sesión), **07** (loot). |
| Reglas de trabajo | `AGENTS.md` raíz y de Unity. |

**Documentos citados que no se entregaron al planificar** (no bloquean esta US): 00, 09, 13, 14, 16. El **doc 10** es necesario para TASK-192/193 (plan posterior).

---

## 4. Arquitectura actual (verificada leyendo `New-Testing`)

| Responsabilidad | Componente actual | Observación relevante |
| --- | --- | --- |
| Progreso | `PlayerExtractionProgressController` (por jugador) | `[Networked] CurrentProgress`, `AssignmentRequested`. Cuota única: `ExtractionConfig.ProgressQuota` (100). Implementa `IExtractionProgressReceiver` y `IExtractionProgressReader`. |
| Productores de progreso | `DamageResolver` (derrota), `NetworkLootContainerInteractable` (primera apertura), `NetworkLootPickup` y `PlayerLootTransferNetworkController` (primera adquisición) | Todos resuelven el receptor con `EntityRegistry.TryGetExtractionProgressReceiver(playerId)`. La unicidad es responsabilidad de cada productor (`FirstOpenResolved`, `DamageResult` fatal, `FirstAcquisitionEligibleAmount`). **No deben cambiar.** |
| Asignación de santuario | `ExtractionSanctuaryAssignmentService` (runner-local) | Selecciona entre santuarios **libres** con `SanctuarySelectionPolicy` (semilla + tick + `EntityId` del jugador) y reserva por `EntityId`. Hay 4 santuarios de escena en `Gameplay`. |
| Santuario | `ExtractionSanctuary` (+ `ExtractionZone` en la misma raíz) | Un único `OwnerIdValue` (int), un `RitualState` y un `RitualTimer`. Interacción solo del dueño; el ritual se cancela si muere el dueño. `Cancelled` y `Completed` son terminales. |
| Countdown | `PlayerExtractionController` (por jugador) | `State/ActiveZoneIdValue/ExtractionTimer` propios. `ExtractionZone` (orden 100) pide `TryBeginExtraction` a cada ocupante exacto. Gate de conectividad de US-52 (`IsParticipantConnected`, con fallo abierto). |
| Resultado de extracción | `PlayerExtractionController.CompleteExtractionInternal` → `RaidAvatarParticipantLink.NotifyExtractionCompleted` → `NetworkRaidParticipant.TryMarkExtracted`; luego `PlayerExtractionLootSaver` | Ya es **individual por participante**; se conserva. |
| Identidad de equipo | `RaidInitialAffiliationSnapshot` (roster congelado: `ProfileId`, `RaidParticipantId`, `RaidTeamId`) | Hoy todo el roster se lanza como `TeamId = 1`. `RaidSessionRules.MaxParticipants = 16`. |
| Conectividad/ausencia (US-52) | `RaidParticipantConnectivity`, `NetworkSpawnManager.TryConfirmTeammateAbsence`, `PlayerCharacter.ForceDefinitiveDefeatFromAbsence` | «Continuar sin compañero» deja al ausente en `Defeated` y fuera de la composición. |
| Estado Abatido | `PlayerCharacter.IsAlive => base.IsAlive \|\| IsDowned` | Abatido **ya cuenta como vivo** para extracción; no hace falta cambiar esa semántica. |
| Host Migration | `HostMigrationSnapshotRestorer` (remapea el dueño del santuario por `EntityId` dinámico, ~línea 350), `HostMigrationRestoreUtility.IsRestoreSpawn`, `CopyStateFrom` | Todo `Spawned()` que escriba estado debe ser *restore-safe*. |
| Presentación | `RaidHudPresenter`, `RaidMinimapPresenter`, `ExtractionSanctuaryPresenter`, `LocalPlayerHudBinder` | Consumen progreso, asignación, ritual y countdown por snapshots. |

**Brecha con el diseño:** `ExtractionArchitecture.md` declara hoy «MVP individual, sin progreso/cuota/asignación de equipo». Ese contrato se **reemplaza** en la Etapa 2.

---

## 5. Arquitectura objetivo

### 5.1 Contratos nuevos o modificados

| Contrato | Responsabilidad | Autoridad |
| --- | --- | --- |
| `RaidTeamRoster` (C# puro) | Proyección inmutable del roster congelado: equipos ordenados, tamaño inicial, participantes por equipo, validación (1–2 por equipo, equipos ≤ `MaxTeamsPerRaid`). | n/a (puro) |
| `ExtractionConfig` | Agrega `_duoProgressQuota`, `_maxTeamsPerRaid`; `ProgressQuota` pasa a `SoloProgressQuota` (campo serializado `_progressQuota` **sin renombrar**); `QuotaForTeamSize(int)`. | Estática |
| `ITeamExtractionProgress` + `TeamExtractionProgress` (`NetworkBehaviour`) | Reserva por equipo: `[Networked, Capacity(RaidSessionRules.MaxTeams)]` de slots `{TeamId, CurrentProgress, AssignmentRequested}`. Cuota derivada, no replicada. Único escritor: State Authority. Registrada como capability independiente en `EntityRegistry`. Vive en el prefab `NetworkMatchController` (precedente: `DungeonPressureController`). | State Authority |
| `PlayerExtractionProgressController` | Pasa a **forwarder**: mantiene `IExtractionProgressReceiver/Reader` por jugador (los productores no cambian), valida al contribuyente y delega en la reserva de su equipo. Se eliminan sus propiedades `[Networked]` (una sola fuente de verdad). | State Authority |
| `IExtractionSanctuary` + `ExtractionSanctuary` | API por **equipo** (no por jugador): slots `{TeamId, RitualState, RitualTimer}`; `IsAssignedToTeam`, `TryAssignTeam`, `CanUseExtraction(teamId)`, `TryGetRitualProgress(teamId, …)`. Varios equipos pueden compartir un santuario. | State Authority |
| `ExtractionSanctuaryAssignmentService` | `TryAssign(teamId)` / `TryGetAssignment(teamId)`; elige entre **todos** los santuarios (no solo libres); idempotente y determinista en resimulación; sin reintento automático (regla vigente). | Host |
| `TeamExtractionCoordinator` (`NetworkBehaviour`) | Countdown conjunto por equipo: slots `{CountdownTimer, ActiveZoneId, RequiredSignature}`. Evalúa composición requerida, presencia, conectividad y reinicios; completa la salida de todos los requeridos en el mismo tick. | State Authority |
| `PlayerExtractionController` | Conserva el estado terminal individual (`Extracted`, restricciones, hooks de resultado/loot). Pierde el inicio individual del countdown; expone API `internal` dirigida por el coordinador. | State Authority |
| Reglas puras: `ExtractionTeamQuotaRules`, `TeamExtractionRequirementRules`, `TeamRitualRules` | Cuota por tamaño inicial; composición requerida y condiciones de inicio/continuidad/reinicio; reglas de inicio y cancelación del ritual. 100 % testeables en EditMode. | n/a (puras) |

### 5.2 Reglas de negocio (doc 06 / doc 02) que el código debe cumplir

- **Composición requerida de un equipo** = integrantes con participación `Raiding`: Activo o Abatido, **conectados o no**. Derrota definitiva y Abandono voluntario salen de la composición. Un desconectado sigue siendo requerido y **bloquea** hasta reconectar o hasta «Continuar sin compañero».
- **Countdown avanza solo si** el equipo tiene el ritual de **su** santuario completo y **todos** los requeridos están conectados y dentro del área. Inicio con `ContainsExact`; continuidad con `ContainsWithTolerance` según `CancelWhenLeavingArea` y `BoundaryTolerance`.
- **Cancela:** un requerido sale del área o se desconecta. **No cancela:** daño, enemigos en el área, pasar a Abatido conectado y dentro.
- **Reinicia:** cambia la composición requerida (p. ej. Derrota definitiva o «Continuar sin compañero»). No hay progreso parcial entre intentos.
- **No existe salida individual voluntaria** mientras el compañero siga activo/recuperable.
- **Resolución:** al expirar el countdown, todos los requeridos pasan a `Extracted` en el mismo tick; cada uno recibe su resultado individual por el camino existente. La extracción de un equipo **no** cierra el Raid de otros.
- **Cuota:** fijada por el tamaño inicial; no cambia por Abatido, derrota, abandono, desconexión ni «Continuar sin compañero». El progreso nunca se pierde y se satura en la cuota.
- **Aporte:** cada evento válido se aplica **una única vez** a la reserva del equipo; jamás se multiplica por integrantes.

---

## 6. Requerimientos técnicos

### 6.1 Arquitectura y Fusion
- Respetá State Authority / Input Authority / proxies. **Solo State Authority** escribe estado autoritativo.
- Simulación tick-driven en `FixedUpdateNetwork`, tolerante a resimulación. Sin efectos irreversibles desde simulación predicha sin garantizar resimulation-safety. Sin eventos C# ni RPC para avanzar estado de gameplay.
- `[Networked]` solo para lo que debe replicarse. Nada derivable (cuota, porcentajes, tiempos restantes) se replica.
- Orden de ejecución (conservar): ritual del santuario **90** → escaneo de zona **100** → `PlayerExtractionController` **110** → `TeamExtractionCoordinator` **115** (lee posiciones y estados finales del tick).
- Todo `Spawned()` que escriba estado debe ser **restore-safe** (`HostMigrationRestoreUtility.IsRestoreSpawn`) y todo estado nuevo debe sobrevivir a `CopyStateFrom`. Verificá la versión instalada de Fusion antes de usar APIs sensibles a versión.
- Dependencias de Unity por referencias serializadas; resolución de capabilities vía `EntityRegistry` (runner-scoped), **sin** búsquedas de escena en gameplay y **sin** singletons estáticos.

### 6.2 Principios SOLID aplicados
- **S:** reglas puras (cuota, requisitos, ritual) separadas de los `NetworkBehaviour`; el coordinador no toca geometría ni inventario; el participante no conoce reglas de equipo; presentación sin estado de simulación.
- **O:** agregar un tamaño de equipo o un criterio de requerido no obliga a tocar productores, zona ni presenters (cambia una regla pura + su config).
- **L:** `PlayerExtractionProgressController` sigue cumpliendo `IExtractionProgressReceiver/Reader` sin que los productores noten el cambio.
- **I:** capabilities pequeñas e independientes en `EntityRegistry` (`ITeamExtractionProgress`, `IExtractionSanctuary`, etc.); no crear una interfaz «gorda» de extracción.
- **D:** las reglas dependen de snapshots/structs puros, no de Fusion; Fusion queda en los bordes de red. Interfaces solo en fronteras reales o seams de test.

### 6.3 Clean Code
- Un tipo principal por archivo; `sealed` por defecto; `_camelCase` en campos privados serializados; `PascalCase` público; early returns; `nameof`.
- Preservar nombres de campos serializados (no migrar `_progressQuota`). No cambiar GUIDs de `.meta`.
- Sin LINQ, closures ni alocaciones en rutas por tick; buffers reutilizables; sin logs por tick.
- Validar dependencias obligatorias al inicializar y **fallar claro** (`Debug.LogError` con contexto). Sin fallback silencioso que oculte configuración inválida. Sin excepciones para flujo normal.
- Eliminar rutas muertas del modelo individual en lugar de dejar **dos fuentes de verdad** (progreso, asignación o countdown).
- Comentarios XML en contratos públicos; los de `Docs/Architecture/` en inglés, los walkthroughs en español.

### 6.4 Pruebas y validación
- Lógica determinista → **EditMode**. Lo atado a Fusion → PlayMode (extender el patrón de `ExtractionProgressPlayModeTests` / `ExtractionProgressSimulationDriver`) o checklist manual Host/Client.
- Evitá tests que lean el código fuente con `File.ReadAllText` + `Contains`: no prueban comportamiento. Si un test existente de ese estilo queda inválido, reemplazalo por uno de comportamiento y explicalo en el walkthrough.
- Tras cambios en Unity: revisá el diff completo, confirmá compilación y consola limpias, corré las suites relevantes con Unity MCP, revisá cambios generados y colaterales, y listá la validación manual pendiente.
- Para redes validá: autoridad, Host y Client, resimulación, efectos irreversibles y Host Migration.
- Usá Unity MCP para estado de Editor (prefabs, escena, referencias serializadas, compilación, Test Runner, Play Mode). No lo uses en lugar de la inspección de código.

---

## 7. Etapas

> Estado esperado entre etapas: el proyecto **compila y las suites verdes**, aunque la funcionalidad aún no esté completa. Documentá cualquier estado intermedio incoherente en el walkthrough.

### Etapa 1 — Línea base y bloqueante heredado de US-52
**Necesidad:** partir de una base verificada y evitar que «Continuar sin compañero» deje un estado inconsistente que impediría la extracción conjunta (TASK-191).

**Alcance**
1. Compilar, revisar la consola y correr **toda** la suite EditMode con Unity MCP. Registrar conteos y fallas previas como línea base (sin corregir lo ajeno).
2. Confirmar y registrar los valores de prueba actuales (`ReconnectBudgetConfig`, `DefaultDungeonPressureConfig`, `ExtractionConfig`). **No modificarlos.**
3. **Corrección H-1:** en `NetworkSpawnManager.TryConfirmTeammateAbsence`, si el avatar del objetivo no se puede resolver, hoy se invoca `TryMarkDefeated(null)` (devuelve `false`) y aun así se marca terminal, se quita del registro y se activa `IsAbsenceConfirmed`, dejando al participante en `Raiding`, sin avatar y sin forma de resolverse. Inspeccioná `TryResolveCurrentAvatar`, `TryMarkDefeated` e `IsCurrentRaidingAvatar` y aplicá el cambio **mínimo** que preserve la invariante: *nunca* terminal + fuera del registro + `Raiding`. Si la derrota forzada no pudo aplicarse, rechazar la confirmación sin efectos laterales (agregar un resultado explícito si hace falta).
4. Agregar tests EditMode de la regla pura afectada. Si el flujo requiere Fusion, documentá la validación manual.

**Fuera de alcance:** el resto de los follow-ups de US-52 (sección 11).
**Criterio de salida:** compilación limpia, suite en verde (o fallas preexistentes documentadas), H-1 corregido y probado.
**Instrucciones Unity esperadas:** ninguna manual.

### Etapa 2 — Contrato técnico y modelo puro de equipos
**Necesidad:** fijar el contrato compartido y su lógica determinista antes de tocar red.

**Alcance**
1. **Actualizar `ExtractionArchitecture.md`**: reemplazar «individual only» por los contratos de la sección 5 (reserva por equipo, asignación por equipo con santuario compartido, ritual por equipo, countdown conjunto, Host Migration, fuentes de verdad). Marcar secciones individuales como reemplazadas. Registrar la divergencia D-3 con el doc 06 como follow-up de Game Design.
2. `RaidSessionRules.MaxTeams` (= `MaxParticipants`).
3. `RaidTeamRoster` (C# puro) creado desde `IReadOnlyList<RaidLaunchParticipant>` (reutilizando `RaidParticipantIdAssignment`); sin romper `RaidInitialAffiliationSnapshot`. Validaciones de A-1 y A-2.
4. `ExtractionConfig`: `_duoProgressQuota` (≥1, 150), `_maxTeamsPerRaid` (1..`MaxTeams`, 4), `SoloProgressQuota` (campo `_progressQuota` intacto), `QuotaForTeamSize(int)`, `TryValidate`/`OnValidate` actualizados. Actualizar los consumidores de `ProgressQuota`.
5. Reglas puras `ExtractionTeamQuotaRules`, `TeamExtractionRequirementRules` (entrada: lista de snapshots por integrante con estado, conectividad, presencia exacta/tolerante; salida: conjunto requerido, `CanStart`, `ShouldContinue`, firma de composición, motivo de cancelación/reinicio) y `TeamRitualRules` (quién puede iniciar; cuándo se cancela, según A-4).
6. Tests EditMode: matriz Solo/Dúo × {Activo, Abatido, desconectado, Derrota, Abandono} × {dentro, fuera}; cuota inmutable; roster inválido; config.

**Fuera de alcance:** cualquier `NetworkBehaviour`, prefab o escena.
**Criterio de salida:** reglas puras verdes, doc actualizado, solo cambia el asset `ExtractionConfig` (campos nuevos con defaults) y se verifica.
**Instrucciones Unity esperadas:** confirmar los campos nuevos en el asset `ExtractionConfig` (Duo = 150, MaxTeams = 4) y que el diff del asset sea mínimo.

### Etapa 3 — Reserva de progreso por equipo (TASK-190, parte 1)
**Necesidad:** que ambos integrantes aporten y observen la misma reserva y cuota.

**Alcance**
1. `ITeamExtractionProgress` y `TeamExtractionProgress` (`NetworkBehaviour`) en el prefab `NetworkMatchController`, con registro/desregistro compensado en `EntityRegistry` (mapa independiente, reglas de conflicto vigentes).
2. Inicialización explícita de los slots desde `RaidTeamRoster` al bootstrap del Raid (verificá el orden respecto de `TryStartRaid`/spawn del match controller). `Spawned()` **restore-safe**; `CopyStateFrom` preserva slots.
3. `PlayerExtractionProgressController` → forwarder: conserva las validaciones actuales (State Authority, tick de simulación, contribuyente vivo y no `Extracted`) y aplica **una vez** a la reserva del equipo; `TryGetSnapshot` devuelve el snapshot del equipo. Quitar sus `[Networked]` de progreso. Reutilizar `ExtractionProgressRules` y `ExtractionProgressSnapshot`.
4. **Estado intermedio documentado:** hasta la Etapa 4 el trigger de asignación sigue usando el camino individual vigente (el jugador que completó la cuota). Dejalo explícito en código (comentario) y en el walkthrough.
5. **Herramienta solo de desarrollo (Editor/development):** campo de Inspector en `SessionConnectionCoordinator` (rama de lanzamiento de desarrollo, ~línea 1747) para agrupar `_developmentAdmittedProfileIds` en equipos de tamaño N. Con valor 0 mantiene el comportamiento actual (todos `TeamId = 1`). **No modificar** `TownRaidPreparationRules.TryCreateLaunchContext` (producción).
6. Tests: aporte único por equipo (nunca × integrantes), saturación, cuota por tamaño inicial, independencia de la composición, progreso conservado, dos equipos independientes, `PlayerExtractionProgressController` cumple el contrato para los productores. PlayMode/manual para la parte Fusion (Host y Client ven lo mismo).

**Fuera de alcance:** santuario, countdown, HUD.
**Instrucciones Unity esperadas:** agregar `TeamExtractionProgress` al prefab `NetworkMatchController`, asignar `ExtractionConfig`, actualizar los prefabs de jugador si el forwarder necesita referencias; verificar con Unity MCP; confirmar que no hay cambios colaterales.

### Etapa 4 — Santuario por equipo: asignación y ritual (TASK-190, parte 2)
**Necesidad:** que la cuota completa habilite **una sola vez** un santuario para el equipo, con ritual por equipo.

**Alcance**
1. `IExtractionSanctuary`/`ExtractionSanctuary` con slots por equipo `{TeamId, RitualState, RitualTimer}` (capacidad `RaidSessionRules.MaxTeams`). Reemplazar la API por jugador (`OwnerId`, `IsOwnedBy`, `TryReserve(EntityId)`) por la API por equipo. Varios equipos pueden compartir un santuario (D-3).
2. `ExtractionSanctuaryAssignmentService.TryAssign(teamId)` / `TryGetAssignment(teamId)`: selección entre **todos** los santuarios con `SanctuarySelectionPolicy` (adaptar la entrada a clave de equipo); determinista y estable en resimulación; idempotente (devuelve la asignación existente antes de reevaluar); un equipo con más de una asignación es estado inválido y se reporta; fallas de configuración se reportan una sola vez.
3. Trigger de asignación: la reserva marca `AssignmentRequested` del **equipo** al completar la cuota y el servicio asigna en el mismo tick (eliminar el camino individual temporal de la Etapa 3).
4. **Ritual por equipo (D-5, A-4):** inicia cualquier integrante Activo (vivo, no Abatido, conectado) del equipo asignado; no se cancela por movimiento/daño/Abatido/desconexión del iniciador; se cancela solo según `TeamRitualRules`; `Completed` y `Cancelled` terminales **por equipo**. Un equipo no puede iniciar, cancelar ni usar el ritual de otro.
5. Disponibilidad de `ExtractionZone`: disponible si algún equipo completó su ritual; la **autorización** es por equipo (`CanUseExtraction(teamId)`).
6. Host Migration: el santuario pasa a clave por equipo, por lo que se elimina el remapeo por `EntityId` de jugador en `HostMigrationSnapshotRestorer`; `Spawned()` restore-safe y `CopyStateFrom` verificados. Documentar el comportamiento de timers (A-7).
7. Tests: reescribir `SanctuaryAssignmentTests` (asignación compartida, determinismo, idempotencia, sin asignación antes de la cuota, un solo trigger, dos equipos en el mismo santuario); reglas de ritual por las reglas puras; PlayMode/manual para el ritual con Host y Client.

**Fuera de alcance:** countdown de extracción, HUD/minimapa (los consumidores existentes deben seguir compilando; adaptarlos mínimamente y listar el detalle para la Etapa 6).
**Instrucciones Unity esperadas:** revisar los 4 santuarios de `Gameplay` (composición y referencias intactas) y los prefabs afectados.

### Etapa 5 — Countdown conjunto y resolución de la extracción (TASK-191)
**Necesidad:** salir juntos, con todas las reglas de composición, conectividad y reinicio.

**Alcance**
1. `TeamExtractionCoordinator` (`NetworkBehaviour`, orden 115) en el prefab `NetworkMatchController`, con registro compensado, restore-safe, usando `TeamExtractionRequirementRules`. Slots por equipo con temporizador, zona activa y firma de composición.
2. Composición requerida y condiciones según la sección 5.2. La conectividad se evalúa **fail-closed**: si no se puede resolver participante o conectividad, el integrante cuenta como no conectado (corrige el fallo abierto de `IsParticipantConnected`).
3. Reinicio inmediato ante cambio de composición, incluida «Continuar sin compañero» (el ausente pasa a `Defeated`).
4. `PlayerExtractionController`: eliminar el inicio individual del countdown y su evaluación propia; mantener el estado terminal `Extracted`, restricciones y los hooks de resultado (`NotifyExtractionCompleted`, `ExtractionCompleted`). API `internal` dirigida por el coordinador para reflejar `InProgress`/zona/temporizador en cada requerido y para completar. Esto mantiene `TryGetProgress` y los presenters actuales sin cambios.
5. `ExtractionZone`: queda como proveedor de geometría, disponibilidad y ocupación. Retirar el inicio y la salida automáticos por participante si quedan sin uso, y actualizar `IExtractionParticipant` y sus tests sin degradar cobertura.
6. Resolución conjunta: todos los requeridos pasan a `Extracted` en el mismo tick. **Idempotente:** reintentos o reinicios nunca duplican resultado, loot ni consolidación (verificar `TryMarkExtracted` y `PlayerExtractionLootSaver`). Verificar que la extracción de un equipo no inicia el cierre del Raid mientras otros participantes sigan `Raiding`.
7. Tests EditMode y PlayMode/manual: Solo, Dúo, Abatido conectado dentro (extrae), Abatido desconectado (bloquea), desconexión durante countdown (cancela), compañero fuera (cancela), Derrota definitiva del compañero (reinicia con nueva composición), «Continuar sin compañero» (el superviviente extrae solo con la cuota original), sin extracción individual con compañero activo, dos equipos extrayendo en el mismo santuario sin interferirse.

**Fuera de alcance:** HUD (Etapa 6), cierre global de Dungeon, oleadas.
**Instrucciones Unity esperadas:** agregar `TeamExtractionCoordinator` al prefab `NetworkMatchController` y asignar referencias; revisar prefabs de jugador y la zona; verificar con Unity MCP.

### Etapa 6 — Presentación por equipo
**Necesidad:** que ambos integrantes **observen** el mismo progreso, cuota, santuario y estado del countdown.

**Alcance**
1. `RaidHudPresenter`: cuota y progreso desde el reader del equipo (debería requerir cambios mínimos); sección de santuario y countdown con presencia del equipo (p. ej. «n/m en el área», conectados). Mantener las tres secciones independientes con dirty check propio y la prioridad de estados vigente.
2. `RaidMinimapPresenter`: asignación por **equipo**; marcador privado para el equipo asignado.
3. `ExtractionSanctuaryPresenter`: privacidad por equipo (reservado/cancelado/ritual propio privados al equipo local) y agregado público para rivales (pulso global si algún ritual está en curso). Sin reproducir cues al unirse tarde o migrar; mantener baseline por snapshot. Sin estado de simulación nuevo, RPCs ni `[Networked]`.
4. Tests EditMode de presenters (patrón `PlayerExtractionPresenterTests`).

**Fuera de alcance:** nuevos assets o audio/VFX no acordados.
**Instrucciones Unity esperadas:** referencias/prefabs de HUD si se agregan elementos; validación visual manual (el usuario la hace).

### Etapa 7 — Validación integral y cierre documental
**Necesidad:** demostrar los criterios de aceptación y cerrar el contrato técnico.

**Alcance**
1. Suite completa EditMode + PlayMode relevantes con Unity MCP, y reporte honesto de lo no ejecutable.
2. Checklist manual Host/Client (Solo y Dúo): progreso compartido, cuota inmutable, asignación única, ritual iniciado por el compañero, Abatido dentro, desconexión, Derrota del compañero, «Continuar sin compañero», reintentos, varios equipos compartiendo santuario (con la herramienta de desarrollo).
3. **Host Migration:** progreso, cuota y asignación se conservan sin repetir contribuciones (checklist + pruebas posibles). Documentar el comportamiento de timers (A-7).
4. Revisión final de `ExtractionArchitecture.md`; matriz criterio → evidencia (sección 10); registro de follow-ups (sección 11) y de la divergencia D-3 con el doc 06.
5. Limpieza: sin rutas individuales muertas, sin tests de texto obsoletos, diff final sin cambios colaterales.

**Criterio de salida:** matriz de aceptación completa con evidencia **observada**, y la validación manual restante claramente listada.

---

## 8. Convenciones de commit

- **Idioma:** inglés. **Formato:** Conventional Commits (`feat`, `fix`, `refactor`, `test`, `docs`) con scope (`extraction`, `raid`, `networking`, `hud`).
- Título ≤ 72 caracteres, en imperativo; descripción en párrafos cortos con *qué* y *por qué*, y una lista `Tests:` con lo realmente ejecutado.
- Un commit por etapa (sugerido). Listar archivos incluidos y excluidos. **El walkthrough no se incluye en el commit de código**; se entrega aparte en `Project Grimhold/Docs/Walkthroughs/US-53/Etapa-N-<slug>.md`, salvo que el usuario indique lo contrario.

---

## 9. Plantilla de walkthrough (obligatoria al cerrar cada etapa)

```markdown
# Walkthrough — US-53 · Etapa N: <nombre>

## 1. Resumen de cambios
- Resultado entregado:
- Archivos creados / modificados / eliminados (con 1 línea de motivo cada uno):
- Qué quedó FUERA de la etapa y por qué:
- Estado de criterios de aceptación (TASK-190 / TASK-191) afectados por esta etapa:
- Decisiones técnicas tomadas y alternativas descartadas:
- Hallazgos clasificados (current-task / follow-up / blocker):
- Estado intermedio incoherente (si lo hay):

## 2. Ejecución real
- Compilación y consola:
- Tests ejecutados con Unity MCP (suite, cantidad, resultado):
- Tests NO ejecutados y por qué:
- Cambios serializados verificados (diff de prefabs/escenas/assets; sin colaterales):

## 3. Instrucciones de implementación manual en Unity
(Pasos exactos: objeto/prefab/escena, componente, campo del Inspector y valor. Escribir «No requiere» si no hay.)

## 4. Instrucciones de validación manual
(Pasos numerados Host/Client, resultado esperado de cada uno, qué reportar si falla.)

## 5. Información para el commit
**Título:** …
**Descripción:** …
**Archivos incluidos:** …
**Archivos excluidos:** (incluye este walkthrough)

## 6. Siguiente paso
Esperar confirmación explícita del usuario para la Etapa N+1.
```

---

## 10. Matriz de criterios de aceptación → etapas

| Criterio | Etapas |
| --- | --- |
| **T190** Ambos integrantes observan el mismo progreso, cuota y santuario | 3, 4, 6, 7 |
| **T190** Abatido, derrota, abandono y desconexión no reducen la cuota | 2, 3, 7 |
| **T190** El progreso adquirido no se pierde por cambios de composición | 3, 7 |
| **T190** Cada fuente aporta según sus reglas, sin duplicar por número de integrantes | 3 |
| **T190** Alcanzar la cuota habilita la asignación una sola vez | 3, 4 |
| **T190** Solo y Dúo cuentan con pruebas específicas | 2, 3, 4, 5, 7 |
| **T190** Host Migration conserva progreso, cuota y asignación sin repetir contribuciones | 3, 4, 7 |
| **T191** Un requerido desconectado o fuera del área impide finalizar | 5 |
| **T191** No existe salida voluntaria individual dejando atrás a un compañero activo/recuperable | 5 |
| **T191** Daño, Abatido o presencia enemiga no cancelan por sí solos | 5 |
| **T191** Un cambio de composición requerida reinicia el countdown | 5 |
| **T191** Los requeridos extraen conjuntamente y reciben resultados individuales | 5 |
| **T191** Loot y recompensas se consolidan una sola vez ante reintentos | 5, 7 |
| **T191** La extracción de un equipo no termina la Raid de otros | 5, 7 |
| **T191** Validación de Solo, Dúo, Abatido, desconexión y «Continuar sin compañero» en Host/Client | 1, 5, 7 |

---

## 11. Riesgos, follow-ups y deuda fuera de alcance

**Follow-ups de Game Design**
- Actualizar el doc 06 §10/§12 (D-3: santuario compartido entre equipos) y confirmar los valores de prueba de cuota (A-3) y `MaxTeamsPerRaid` (A-2).
- Confirmar A-1 (tamaño máximo de equipo = 2) y A-4 (condición de cancelación del ritual).

**Heredados de US-52 (no bloquean esta US; no corregir aquí)**
- Se relajó `Runner.IsSimulationUpdating` en `NetworkLootContainer` (3 puntos) y `PlayerCorpseGenerationController` para ejecutar la derrota forzada desde un RPC; afecta a todo el loot. Alternativa: diferir la ejecución al siguiente `FixedUpdateNetwork`.
- `CharacterBase.Health` se ensanchó a `protected set` y la derrota forzada asigna `Health = 0f` salteando el pipeline de daño.
- Los tests `RaidExtractionConnectivityTests` y el test de fallback leen el fuente con `File.ReadAllText`; `RaidTeammateAbsenceExecutionTests` no prueba la ejecución real (idempotencia, carrera reconexión↔confirmación).
- XP de la derrota forzada (20 %): confirmar contra el doc 05.
- `GameClosed` terminal inmediato en la reconexión puede descartar un ticket válido si el Host aún no propagó la reapertura.

**Riesgos de esta US**
- Cambio de contrato de `IExtractionSanctuary`/`IExtractionParticipant`: impacta presenters, minimapa, restorer de Host Migration y varios tests; mitigado por etapas pequeñas y verificación de compilación en cada una.
- Matchmaking multi-equipo inexistente en producción (A-5): el comportamiento multi-equipo se valida con la herramienta de desarrollo y pruebas puras hasta que exista.
- Timers de ritual/countdown en Host Migration (A-7): comportamiento actual, solo se documenta.

---

## 12. Checklist final de cada iteración

- [ ] Leí HacknPlan (solo lectura), `AGENTS.md`, Arquitectura y Game Design relevantes.
- [ ] Implementé **solo** la etapa pedida; no adelanté nada de etapas futuras.
- [ ] Compilación y consola revisadas; suites relevantes ejecutadas (o declaradas no ejecutadas).
- [ ] Diff completo revisado; sin cambios colaterales ni valores de prueba tocados.
- [ ] `Spawned()` restore-safe y estado nuevo cubierto por `CopyStateFrom` (cuando aplica).
- [ ] Walkthrough completo entregado en `Docs/Walkthroughs/US-53/` con información de commit.
- [ ] **Me detuve y espero la confirmación del usuario.**

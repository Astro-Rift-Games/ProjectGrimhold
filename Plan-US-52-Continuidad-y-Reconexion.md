# Plan de Implementación — US-52: Conservar la participación durante desconexiones recuperables

**Repositorio:** Astro-Rift-Games/ProjectGrimhold — branch `New-Testing`
**Unity:** 6000.5.1f1 (`ProjectSettings/ProjectVersion.txt`) — **Fusion:** 2.1.1 Stable build 2177 (`Assets/Photon/Fusion/build_info.txt`)
**Backend:** Node.js/Express en `/backend` (no interviene en esta US, ver §1.4)
**HacknPlan:** Board `MVP / Sprint 13`
- Story **#471 — US-52**
- **#472 — TASK-187** Alinear la arquitectura de continuidad y extracción con Game Design
- **#473 — TASK-188** Retener y recuperar al participante desconectado
- **#474 — TASK-189** Implementar «Continuar sin compañero»

**Fuentes de Game Design:** 01 Game Flow Principal · 02 Estados del Juego (§3, §4.5, §13, §14, §15, §16) · 06 Sistema de Extracción (§16–§20) · 07 Sistema de Loot (§19–§23). Para XP de Derrota definitiva: 05 (ver §3.3, punto P-1).

---

## 0. Instrucciones obligatorias para Antigravity

1. **Implementar UNA sola etapa por iteración.** Está **prohibido** implementar el plan completo o adelantar trabajo de etapas siguientes. Al terminar una etapa: detenerse, entregar el walkthrough y esperar aprobación explícita del usuario.
2. **Antigravity solo edita archivos.** No puede abrir Unity, compilar, correr el Test Runner ni modificar prefabs/escenas. Consecuencias:
   - Nunca editar a mano prefabs, escenas, `.meta`, `.asset`, `PlayerInputActions.inputactions` ni `ProjectSettings`. Toda configuración serializada la hace el usuario siguiendo las instrucciones del walkthrough.
   - Los componentes nuevos deben validar sus referencias serializadas en inicialización y fallar con `Debug.LogError` con contexto (sin fallback silencioso).
   - Nunca afirmar que algo compila, que un test pasó o que un flujo multijugador funciona. En el walkthrough declarar solo lo realmente ejecutado ("revisión estática del diff", "tests escritos, **no ejecutados**").
   - Ser conservador con Fusion (`[Networked]`, weaving, RPCs): el usuario compilará y devolverá errores; se corrigen dentro de la misma etapa.
   - Si el entorno tiene un compilador/analizador de C# capaz de validar solo código C# puro, puede usarlo, informando exactamente qué se validó.
3. **Seguir `AGENTS.md` (raíz) y `Project Grimhold/AGENTS.md`.** Orden de fuentes de verdad: código actual de `New-Testing` → `Docs/Architecture/` → `AGENTS.md` → Game Design vigente vía `grimhold-docs`. Cargar `skills/grimhold-task-execution/SKILL.md`.
4. **Antes de tocar código** de una etapa: leer HacknPlan (US #471 y la TASK correspondiente), los documentos de `Docs/Architecture/` que la etapa lista en "Leer antes" y el Game Design relevante (`search_documents` → `get_document` de la sección). Para trabajo cross-sistema leer **todos** los documentos que poseen comportamiento afectado.
5. **No inventar Game Design ni valores.** Todo plazo/valor no definido queda como decisión pendiente (§3.3) y se reporta. Si una fuente contradice a otra: identificar el conflicto, indicar qué fuente es dueña de cada responsabilidad, **reportar antes de crear otro contrato** y preguntar.
6. **Alcance acotado.** No hacer refactors no relacionados, no agregar dependencias/paquetes, no introducir DI frameworks, event buses, singletons ni abstracciones especulativas. Todo hallazgo fuera de la etapa se reporta como *follow-up* o *blocker*, no se corrige.
7. **No crear** `odd/tasks`, SDD ni mirrors de HacknPlan (HacknPlan es la fuente de seguimiento).
8. **Cada etapa deja el repo en un estado coherente**: sin código a medio integrar, sin código comentado obsoleto, con tests EditMode escritos para la lógica determinística.
9. **Walkthrough obligatorio al cerrar cada etapa** (plantilla en §6). Se entrega como archivo `.md` en `Project Grimhold/Docs/Walkthroughs/US-52/Etapa-N-<slug>.md`. Esa carpeta es solo de entrega: **no incluir los walkthroughs en el commit** salvo pedido del usuario. El mensaje de commit se redacta con la skill `grimhold-commit-message` (Conventional Commits, en inglés, subject ≤ 72 columnas, sin atribución de IA).
10. **Protocolo de iteración:** implementar → revisar el diff completo → entregar walkthrough con lo pendiente de validación del usuario → el usuario configura Unity/compila/corre tests/prueba Host+2 clientes y reporta → Antigravity corrige **solo dentro de la etapa** → el usuario aprueba → recién entonces se inicia la etapa siguiente.

---

## 1. Requerimientos técnicos

### 1.1 Arquitectura actual (verificada en `New-Testing`)

**Participación y avatar** (`RaidParticipantArchitecture.md`)
- `NetworkRaidParticipant` es el `PlayerObject` estable por `ProfileId`; el `NetworkPlayer` es un avatar temporal. Estados: `Raiding`, `Extracted`, `Defeated`, `Aborted` (`RaidParticipantState`). `Spectating` es presentación local, nunca estado.
- Co-ubicados en el participante: `PlayerExpeditionExperienceLedger`, `PlayerExpeditionProgressionResolver`, `RuntimeAttributeOverrideNetworkController`. Patrón a imitar para estado nuevo por participante.
- Derrota: `PlayerCharacter.HandleDeath()` → `PlayerCorpseGenerationController.TryConvertInventoryToCorpseLoot` → `RaidAvatarParticipantLink.NotifyCorpseConversionCompleted` → `NetworkRaidParticipant.TryMarkDefeated(avatar)` (resuelve `DefeatConfirmed`). Existe `PlayerCharacter.ResolveDefinitiveDefeatFromDowned()` (interno). El **Abandono voluntario** ya reutiliza el handoff material de Loot sin daño sintético ni Kill XP: es el precedente a estudiar para la Derrota forzada.

**Desconexión hoy** (`NetworkSpawnManager.OnPlayerLeft`, ≈ l.3686)
- Participante `Raiding` no Downed → se despawnea el avatar, `TryFinalizeDefinitiveDisconnectAfterMaterialClosure()` (Progresión `DefinitivelyDisconnected`, 0% XP) y se despawnea el participante. **Es terminal y contradice 02 §13.**
- `Raiding` + avatar Downed → ya se retiene (`_retainedDownedParticipants`, `RaidPlayerDeparturePolicy.ShouldRetainDownedRaider`): se quita input authority y el drain sigue en el Host. Es bookkeeping **runtime-only del Host**, fuera del snapshot de Host Migration.
- `Defeated` sin Controlled Return → se preservan participante y cadáver "para política futura de recuperación".
- **Detalle crítico:** `_controlledReturns.MarkTerminal(in departureKey)` se ejecuta *antes* de las ramas Extracted/Downed. Eso marca terminal a un perfil retenido y `TryAdmitPlayer` lo rechazaría al volver. Debe corregirse al introducir la retención.

**Reconexión hoy**
- No existe reconexión ordinaria mid-Raid (`ProgressionArchitecture.md`, "Host Migration and reconnection"). `TryAdmitPlayer` solo admite en `WaitingForPlayers` y rechaza claves terminales.
- La sesión Raid se cierra con `SessionInfo.IsOpen=false` (`NetworkMatchController` l.151/218/249; `FusionSessionLauncher` l.234). Raid con `IsVisible=false`.
- Host Migration tiene un rebind validado por `ProfileId` (`NetworkSpawnManager.TryRebindHostMigrationProfile`, ≈ l.1395) reutilizable como patrón (input authority del participante, `SetPlayerObject`, `_admittedPlayers`, `_spawnedPlayers`, `_admittedProfiles`, input authority del avatar, `_spawnedAvatars`).
- `OnConnectRequest` existe en `NetworkSpawnManager` (≈ l.1981).
- Cliente: `SessionConnectionCoordinator.OnRaidRunnerShutdown` → `RecoverFromUnexpectedShutdown` (≈ l.1549–1640) lo devuelve a Pueblo y, si la admisión ya estaba confirmada, **confirma** la reserva de loadout (no la revierte). `RaidAdmissionData` transporta `ReservationId`, loadout reservado, nivel/XP base y watermark: **no debe reutilizarse para reconectar** (reinicializaría la participación).

**Equipos / sesión**
- `RaidSessionRules.MaxParticipants = 16`; `RaidTeamId` + `RaidInitialAffiliationSnapshot.TryGetTeammateProfileId` (equipos de a 2). `RaidTeammateHudPresenter` ya resuelve al compañero congelado por `ProfileId` enumerando `NetworkObject`s.
- `NetworkMatchController` cierra por `NaturalCompletion` cuando deja de haber participantes `Raiding` (l.186–189); un participante retenido y `Raiding` cuenta como activo.

**Extracción** (`ExtractionArchitecture.md`)
- Hoy es **individual**: cuota por jugador (`PlayerExtractionProgressController`), Santuario con un único owner (`ExtractionSanctuary.OwnerId`), ritual y countdown por jugador (`PlayerExtractionController`). GD 06 define cuota, Santuario y countdown **compartidos por equipo**. **Conflicto de fuentes ya reportado y resuelto por el usuario:** US-52 solo define el contrato y aplica el gate de desconexión; la extracción de equipo completa es *follow-up/blocker* (§3.3).
- La zona inicia extracción para cualquier participante presente (con Santuario completado y siendo owner). Un avatar Activo desconectado dentro del área podría extraer: **debe bloquearse** (06 §16).

**Host Migration** (`HostMigrationRecoveryArchitecture.md`)
- Elegibles: participantes `Raiding` o `Defeated` no terminales. Los no recuperados al sellar se finalizan como `Raiding → Aborted` (`DefinitivelyDisconnected`). Limitación conocida: Downed + desconectado durante migración no está cubierto. **No se rediseña Host Migration** (fuera de alcance de la US).
- Patrón de estado restaurable: `[Networked]` + `CopyStateFrom` + guard `HostMigrationRestoreUtility.IsRestoreSpawn` en `Spawned()` (ej. `PlayerDownedStateNetworkController`).

**Tests**
- EditMode: `Assets/Tests/EditMode/Editor/Networking/` (ej. `RaidPlayerDepartureTests.cs`, `SessionConnectionStateMachineTests.cs`, `RaidParticipantSpawnRulesTests.cs`). PlayMode: `Assets/Tests/PlayMode/`. Existen tests de composición de prefabs que protegen `NetworkedBehaviours`.

### 1.2 Principios SOLID aplicados a esta US

| Principio | Aplicación concreta |
|---|---|
| **S** — Responsabilidad única | Reglas determinísticas en C# puro (sin `UnityEngine`/Fusion). Un `NetworkBehaviour` delgado por participante (`RaidParticipantConnectivity`) que solo posee conectividad + presupuesto. Routing de `PlayerRef` en `NetworkSpawnManager`. Presentación aparte. **`NetworkSpawnManager` ya tiene ≈ 4100 líneas: no sumar lógica de dominio ahí**; solo orquesta routing Fusion y delega a clases nuevas. |
| **O** — Abierto/cerrado | Extender la política de salida con un clasificador nuevo en vez de ramificar más `OnPlayerLeft`; el gate de extracción consulta una proyección de solo lectura sin modificar el protocolo de extracción. |
| **L** — Sustitución | Clases `sealed`; no se introducen jerarquías nuevas. |
| **I** — Segregación | Proyección inmutable `RaidConnectivitySnapshot` (solo lectura) para HUD y extracción; no exponer `NetworkRaidParticipant` completo. |
| **D** — Inversión | Las reglas puras reciben valores, no servicios. Dependencias de Unity por referencias serializadas; de C# puro por constructor/argumentos. Sin service locators, singletons ni event bus estático. Interfaces solo en fronteras reales o seams de test. |

### 1.3 Clean Code (reglas del repo)

- Un tipo principal por archivo; nombre de archivo = tipo; `sealed` salvo herencia deliberada; campos serializados privados `_camelCase`; early returns; `nameof`.
- Separar siempre: configuración estática (ScriptableObject inmutable) · estado persistente · estado runtime local · estado networked · estado de presentación.
- **No sincronizar estado derivable** (ej. "decisión disponible" se deriva de `IsDisconnected && presupuesto==0 && participante Raiding`; no se replica un flag).
- Simulación en `FixedUpdateNetwork` solo con State Authority; tolerar resimulación (`Runner.IsForward` donde corresponda); sin RPCs para estado continuo; el cliente no es autoritativo (el RPC no transporta el objetivo: el Host lo resuelve).
- Eventos solo para presentación; nunca avanzan estado autoritativo. Sin LINQ/alloc en rutas por tick. Sin logs por tick.
- Validar dependencias obligatorias al inicializar y fallar claro; sin fallback silencioso; sin excepciones para flujo normal.
- Preservar nombres de campos serializados, GUIDs y namespace strategy; no tocar `PlayerInputActions.cs` generado.
- Cada decisión de arquitectura transversal se documenta en `Docs/Architecture/`; Game Design no se edita para "calzar" la implementación.

### 1.4 Backend (`/backend`)

Solo expone `POST /character/raid/extraction-result` (publicado por el Host, firma HMAC). Esta US no cambia el contrato de resultados ni agrega endpoints. **Fuera de alcance**; si una etapa descubre lo contrario, reportarlo como blocker.

---

## 2. Objetivos derivados de la US y mapa de criterios

**Objetivo general:** separar la pérdida de conectividad de los resultados de gameplay: el personaje desconectado permanece en la Dungeon, expuesto y en su estado funcional; si su dueño vuelve, recupera la misma participación sin duplicar nada; si agota su presupuesto de reconexión, su compañero conectado puede resolver explícitamente la ausencia con «Continuar sin compañero».

| Criterio de aceptación | Etapa(s) |
|---|---|
| US: desconectar no equivale a abandonar, extraer ni a Derrota definitiva | 2, 3 |
| US: reconectar recupera la misma participación en su estado vigente | 4, 5 |
| US: no se duplican personajes, objetos, cadáveres ni resultados | 3, 4, 6 |
| US: decisión explícita documentada para desconexión prolongada | 1, 6 |
| T187: arquitectura distingue conectividad, abandono y Derrota definitiva | 1 |
| T187: cada transición tiene owner y condiciones; plazos no definidos = decisiones pendientes | 1 |
| T187: TASK-188/189 implementables sin inferir responsabilidades; revisión y aprobación previa | 1 (gate) |
| T188: retener participante, avatar, estado, objetos; reasociar jugador; no sobrescribir al reinicializar | 3, 4 |
| T188: Derrota ya resuelta no se revierte al reconectar | 4 |
| T188: validación Host/Client con activos y Abatidos; respeta Host Migration | 3, 4, 7 |
| T189: agotar presupuesto no aplica derrota; reconectar antes de confirmar elimina la opción | 2, 6 |
| T189: presupuesto agotado no se reinicia; confirmaciones repetidas no duplican; rechazada no modifica | 2, 6 |
| T189: superviviente conserva la cuota original y cumple extracción individual | 6, 7 |
| T189: flujo completo y carreras validados en Host/Client | 6, 7 |

---

## 3. Decisiones

### 3.1 Acordadas con el usuario

| # | Decisión |
|---|---|
| D-1 | Extracción compartida: US-52 define el **contrato**, la **composición requerida** (miembros activos/recuperables) y el **bloqueo por desconexión**. La extracción de equipo completa queda como follow-up/blocker. |
| D-2 | Re-join en Fusion: la Etapa 1 incluye una verificación documentada. Propuesta base: reabrir `IsOpen` **solo** mientras exista ≥1 participante retenido, validando por `ProfileId` + `RaidGenerationId` y desconectando a cualquier otro. |
| D-3 | El flujo cliente de reconexión **está en alcance** (mínimo). Por decisión del usuario, el jugador **vuelve a la Raid por decisión propia** (botón en Pueblo); **no hay reintento automático**. |
| D-4 | Solo / sin decisor disponible: se **mantiene el comportamiento actual** (finalización definitiva por desconexión). Se registra como decisión pendiente de Game Design. |
| D-5 | Presupuesto de reconexión: `ScriptableObject` inmutable **sin valor productivo por defecto**, validación fail-fast, valor de prueba solo en tests/asset creado por el usuario. **Por participante.** |
| D-6 | Presupuesto restante **networked**, co-ubicado en el participante, restaurado por `CopyStateFrom`. La limitación "Downed/Activo + desconectado durante Host Migration" se **documenta**, no se rediseña. |
| D-7 | Derrota forzada mediante API interna acotada en `PlayerCharacter`, reutilizando el camino de cadáver y `TryMarkDefeated`, **sin Kill XP ni progreso PvP**. Outcome `Defeated` (ver P-1). |
| D-8 | Reconexión de un participante ya `Defeated` (espectador/Resultados): **follow-up**. |
| D-9 | La decisión «Continuar sin compañero» se confirma con un **botón en pantalla** (HUD). |
| D-10 | Si el jugador desconectado vuelve a Pueblo, **sigue desconectado** (la participación permanece retenida y el presupuesto sigue consumiéndose). La decisión de volver a la Raid es suya; la de resolver su ausencia depende del compañero (02 §13–14, 06 §19). |
| D-11 | La Etapa 1 puede modificar los documentos de arquitectura listados; **nada se implementa hasta aprobar la Etapa 1**. |
| D-12 | Se puede tocar cualquier archivo de Player/Downed que haga falta. |
| D-13 | Antigravity solo edita código; el usuario configura Unity, compila, corre Test Runner y prueba Host + 2 clientes con builds. |
| D-14 | Walkthroughs entregados al cerrar cada etapa; no son permanentes (carpeta de entrega, fuera del commit). |

### 3.2 Asumidas por el plan (el usuario puede vetarlas)

| # | Asunción |
|---|---|
| A-1 | Mientras el cliente tenga un ticket de reconexión válido, la **Preparación de Raid en Pueblo queda bloqueada** (evita iniciar otra expedición con la participación anterior viva). |
| A-2 | El ticket de reconexión es **process-local, en memoria**; se pierde al cerrar la aplicación. |
| A-3 | Un rechazo de reconexión es genérico para el cliente ("participación no disponible"); el cliente descarta el ticket. |
| A-4 | Config del presupuesto ausente/ inválida → `Debug.LogError` explícito y **comportamiento legacy** (finalización definitiva) con log por cada salida; nunca silencioso. |
| A-5 | **Decisor** = compañero del mismo `RaidTeamId`, con participante `Raiding` y conectado. Un compañero `Defeated` (espectador) **no** decide. |
| A-6 | El presupuesto solo se consume mientras el participante está `Raiding` y desconectado; al pasar a `Defeated`/terminal deja de aplicar. |
| A-7 | Derrota forzada de un Abatido desconectado: se limpia primero el estado Downed (reserva descartada) y luego se resuelve la Derrota por el camino de cadáver. |

### 3.3 Pendientes de Game Design / follow-ups (reportar, no inventar)

- **P-1 (verificar en Etapa 6, antes de implementar):** consecuencia de XP de Derrota definitiva forzada de un jugador desconectado (documento 05; hoy `Defeated` retiene 20%). Si 05 dice otra cosa → **detenerse y preguntar**.
- **P-2:** fallback "sin decisor" (A-5/D-4): al quedar sin decisor un retenido, se finaliza por el camino legacy. Game Design debe confirmarlo.
- **P-3:** valores de plazo del presupuesto (Balance/Networking).
- **FU-1:** reconexión de `Defeated` → espectador/Resultados (D-8). Consecuencia a documentar: mientras no exista, el jugador forzado a derrota **no recibe su XP local** (no hay ACK de Input Authority; el watermark local no se consume).
- **FU-2:** extracción de equipo completa (cuota/Santuario/countdown compartidos; requisito grupal de 06 §15).
- **FU-3:** Host Migration con participantes retenidos desconectados.
- **FU-4:** reintento automático/UI de cuenta regresiva del lado cliente.
- **Riesgo conocido:** `ProfileId` llega sin autenticación fuerte en el payload de join (ya ocurre en Host Migration). No se corrige aquí; reportar en Etapa 4.

---

## 4. Contrato objetivo (semilla para la Etapa 1)

**Dimensiones independientes** (02 §3): estado funcional del participante (`RaidParticipantState` + Downed) y conectividad (`Connected`/`Disconnected`). Desconexión **no** es resultado ni estado de expedición.

**Estado nuevo por participante** — `RaidParticipantConnectivity` (`NetworkBehaviour`, co-ubicado en `NetworkRaidParticipant.prefab`, `[DisallowMultipleComponent]`):
- `[Networked] bool IsDisconnected`
- `[Networked] int RemainingBudgetTicks` (inicializado una vez en State Authority desde la config; **no se reinicia** al reconectar)
- marcador de inicialización; `Spawned()` respeta `HostMigrationRestoreUtility.IsRestoreSpawn`.
- Derivados (no replicados): `IsBudgetExhausted = RemainingBudgetTicks <= 0`; `IsDecisionAvailable = IsDisconnected && IsBudgetExhausted && State == Raiding`.

**Owners y transiciones**

| Transición | Owner | Condición |
|---|---|---|
| Salida de peer → retención | State Authority (Host) vía `NetworkSpawnManager.OnPlayerLeft`, delegando a las reglas puras | Participante `Raiding` (Activo o Downed), hay decisor, config válida |
| Consumo de presupuesto | `RaidParticipantConnectivity` (State Authority, `FixedUpdateNetwork`) | `IsDisconnected && State==Raiding && Remaining>0` |
| Presupuesto agotado → decisión disponible | derivado | `Remaining==0` (sticky) |
| Retenido → conectado (reconexión) | State Authority vía rebind validado | Perfil retenido, `RaidGenerationId` igual, `Raiding`, no resuelto; pausa presupuesto |
| Confirmar «Continuar sin compañero» | State Authority valida RPC del compañero | Ver reglas §Etapa 6; idempotente |
| Sin decisor → finalización legacy | `NetworkSpawnManager` | Último decisor desaparece (P-2) |
| Derrota natural del retenido (drain/daño) | camino existente `HandleDeath` → `TryMarkDefeated` | Sin cambios |

**Composición requerida (06 §15–16, 02 §15):** integrante activo/recuperable = participante `Raiding` (Activo o Downed) **sin importar la conectividad**. Excluidos: `Defeated`, `Aborted`, `Extracted`. Un desconectado **no puede completar extracción**.

---

## 5. Etapas

> Cada etapa cubre un objetivo específico. **Solo una por iteración.** Ninguna se inicia sin aprobación explícita de la anterior.

### Etapa 1 — Contrato técnico y verificación de re-join (TASK-187) · *solo documentación*

**Objetivo:** dejar un contrato técnico revisado y aprobado que permita implementar TASK-188/189 sin inferir responsabilidades. **Esta etapa es un gate: no hay código funcional.**

**Leer antes:** `RaidParticipantArchitecture`, `RaidDefeatAndSpectatorArchitecture`, `DownedAndReviveArchitecture` (§15–16), `HostMigrationRecoveryArchitecture`, `ExtractionArchitecture`, `ProgressionArchitecture`, `LobbyAndSessionTransitionArchitecture`, `SessionLifecycleAndSpawning`, `LootInteractionArchitecture`/`InventoryWorldDropArchitecture` (solo lo que toque retención de Loot); GD 01, 02, 06, 07 vía `grimhold-docs`.

**Alcance:**
1. Crear `Docs/Architecture/RaidConnectivityContinuityArchitecture.md` con: separación conectividad/gameplay/abandono/Derrota; modelo de §4 refinado; tabla completa de transiciones con **owner y condiciones explícitas**; composición requerida; definición de decisor; budget (cumulativo, sticky, por participante); política de **no marcar terminal** a un perfil retenido; política de reapertura de sesión; formato del payload de reconexión (identidad, **nunca** loadout/baseline); manejo de carreras reconexión↔confirmación; fallback sin decisor; integración con Player/Downed/Extracción/Match closure/Host Migration; lista de **plazos pendientes** sin valores inventados; follow-ups FU-1…FU-4.
2. Alinear los documentos que hoy describen la desconexión como terminal o la reconexión como "diferida": `RaidDefeatAndSpectatorArchitecture` ("Player departure"), `DownedAndReviveArchitecture` (§15 y filas de estado), `ProgressionArchitecture` ("Host Migration and reconnection", semántica de `DefinitivelyDisconnected`: pasa a ocurrir solo por fallback/no recuperación), `HostMigrationRecoveryArchitecture` (limitación para retenidos desconectados), `ExtractionArchitecture` (composición requerida, gate de conectividad y estado del conflicto individual vs. equipo), `LobbyAndSessionTransitionArchitecture` (regla de reapertura de sesión).
3. **Verificación de re-join de Fusion 2.1.1** (análisis de la fuente en `Assets/Photon/` y documentación oficial; **no se puede ejecutar Unity**): ¿un cliente puede unirse a una sesión cuyo `SessionInfo.IsOpen` fue puesto en `false` y luego reabierto en runtime por el Host? ¿Cómo comunica el Host el rechazo (`OnConnectRequest` vs `Disconnect`)? ¿Qué implica `IsVisible=false`? Entregar: mecanismo elegido, evidencia citada (archivos/líneas) y **qué queda por validar manualmente en Etapa 4**.
4. Inventariar los **puntos de integración con Player** (`PlayerCharacter`, Downed controller, corpse controller, extracción, HUD de compañero) que usarán las etapas 3–7.
5. Verificar (lectura) el comportamiento cliente tras caída: reserva de loadout confirmada, sin devolución al stash (descartar riesgo de duplicación).

**Fuera de alcance:** cualquier cambio de código, config o tests.

**Criterio de cierre:** documentos creados/alineados; transiciones con owner; plazos pendientes listados; decisión de re-join justificada. **El usuario revisa y aprueba explícitamente antes de la Etapa 2.**

**Configuración manual en Unity:** ninguna.

---

### Etapa 2 — Modelo de dominio puro: conectividad, presupuesto y decisión (TASK-188/189 · base)

**Objetivo:** implementar y testear **todas las reglas determinísticas** sin tocar red ni escena.

**Leer antes:** documento de la Etapa 1 aprobado; `RaidPlayerDeparturePolicy`, `RaidSessionRules`, `RaidInitialAffiliationSnapshot`, `ExtractionConfig` (patrón de config inmutable).

**Alcance (C# puro, sin `UnityEngine` ni Fusion salvo el `ScriptableObject`):**
1. `ReconnectBudgetConfig` (`ScriptableObject` inmutable, patrón de `ExtractionConfig`): duración en segundos, **sin default productivo**, `TryValidate`.
2. Reglas de presupuesto: consumo por tick, **acumulativo**, **sticky** al llegar a 0, **no se reinicia** al reconectar, conversión segundos→ticks con tick rate explícito.
3. Clasificador de salida (extiende, sin romper, `RaidPlayerDeparturePolicy`): entradas = estado del participante, `avatarIsDowned`, `hasEligibleDecider`, `configValid` → salida = `RetainDisconnected` | `FinalizeDefinitiveDisconnect` (legacy) | existentes (Defeated/Extracted/Abort). Debe cubrir A-4/A-5/D-4.
4. Reglas de decisión «Continuar sin compañero»: `Evaluate(...)` → resultados enumerados (`Confirmable`, `RejectedBudgetNotExhausted`, `RejectedReconnected`, `RejectedNotTeammate`, `RejectedConfirmerUnavailable`, `RejectedParticipantNotRaiding`, `RejectedAlreadyResolved`). Sin efectos.
5. Regla de composición requerida y de "decisor" (A-5) y `RaidConnectivitySnapshot` inmutable de solo lectura.

**Tests EditMode** (`Assets/Tests/EditMode/Editor/Networking/`): tabla completa de clasificación; consumo acumulativo con reconexiones intermedias; sticky tras reconectar y re-desconectar (habilita decisión de inmediato); cada rechazo de decisión; composición con las 6 combinaciones estado×conectividad de 02 §15; config inválida; no regresión de `RaidPlayerDepartureTests`.

**Fuera de alcance:** `NetworkBehaviour`, `OnPlayerLeft`, rebind, UI.

**Configuración manual en Unity:** ninguna obligatoria. (El asset de config se crea en la Etapa 3, cuando se asigna.) **Validación del usuario:** compilar y correr EditMode.

---

### Etapa 3 — Retención del participante desconectado en el Host (TASK-188 · parte 1)

**Objetivo:** que desconectarse **ya no sea terminal**: el participante y su avatar permanecen en la Dungeon, expuestos y en su estado funcional, con el presupuesto corriendo.

**Leer antes:** documento de arquitectura aprobado; `NetworkSpawnManager` (`OnPlayerLeft`, `_retainedDownedParticipants`, `HasUnresolvedRetainedDownedParticipant`, `AbortRaidingParticipantsForClosure`), `NetworkMatchController` (cierre), `PlayerDownedStateNetworkController`, `RaidAvatarParticipantLink`.

**Alcance:**
1. `RaidParticipantConnectivity` (`NetworkBehaviour`) según §4: init único en State Authority; `FixedUpdateNetwork` consume presupuesto; `Spawned()` restore-safe; proyección `RaidConnectivitySnapshot`.
2. Nuevo colaborador (no inflar `NetworkSpawnManager`) que mantiene el conjunto de **retenidos desconectados** (generaliza `_retainedDownedParticipants` a Activo **y** Downed, sin cambiar el comportamiento Downed existente) y aplica el clasificador de la Etapa 2.
3. `OnPlayerLeft`: cuando corresponda, **retener**: quitar routing del `PlayerRef`, quitar input authority de participante y avatar, marcar `IsDisconnected`, conservar objetos. **Mover `MarkTerminal` para que un perfil retenido NO quede terminal** (ver §1.1). Mantener intactas las ramas Defeated/Extracted/Aborted y el camino legacy cuando no hay decisor o config inválida (con log).
4. **Sin input = neutro:** verificar por lectura que movimiento, combate, interacción y extracción no dependen de que exista input authority ni repiten input viejo; corregir solo lo necesario.
5. Cierre de Raid: los retenidos cuentan como activos para `HasRaidingParticipants`; `AbortRaidingParticipantsForClosure` los finaliza al cerrar por Host/otro motivo sin fugas. Re-evaluación del fallback **sin decisor** (P-2) en el hook mínimo existente (justificar el punto elegido).
6. Interacción con Downed: el drain continúa; al Defeat natural se resuelve por el camino existente y el presupuesto/decisión dejan de aplicar (A-6). Revivir a un desconectado lo devuelve a Activo **sin** cambiar su conectividad.

**Tests:** EditMode para lo puro; PlayMode (escritos, **ejecuta el usuario**) para: retención Activa, retención Downed sin regresión, ausencia de despawn, perfil no terminal tras retención, legacy cuando no hay decisor.

**Fuera de alcance:** reconexión, UI, Continuar sin compañero, extracción.

**Configuración manual en Unity (el walkthrough debe dar pasos exactos):**
- Agregar `RaidParticipantConnectivity` a `Assets/Prefabs/NetworkRaidParticipant.prefab` (exactamente una instancia) y verificar que Fusion lo registre en `NetworkedBehaviours`.
- Crear el asset `ReconnectBudgetConfig` (valor de **prueba** elegido por el usuario; Balance define el real) y asignarlo en el serialized field que el walkthrough indique.
- Actualizar/ejecutar los tests de composición del prefab.

---

### Etapa 4 — Reconexión autoritativa en el Host (TASK-188 · parte 2)

**Objetivo:** que un perfil retenido pueda **reasociarse a su participación existente** sin duplicar ni reinicializar nada.

**Leer antes:** `TryRebindHostMigrationProfile`, `OnPlayerJoined`/`OnConnectRequest`/`TryAdmitPlayer`, `NetworkMatchController` (apertura/cierre de sesión), arquitectura aprobada (política de reapertura y payload).

**Alcance:**
1. **Rebind compartido:** extraer del rebind de Host Migration el núcleo común (input authority del participante, `SetPlayerObject`, tablas de routing, input authority del avatar) a un helper reutilizable, **sin alterar el comportamiento de Host Migration** (sus tests deben seguir verdes).
2. Payload de reconexión de **solo identidad** (`RaidCode`, `ProfileId`, `RaidGenerationId`), con codec y validación propios. **Prohibido** reutilizar `RaidAdmissionData` (loadout/baseline).
3. Camino de join para reconexión (en `OnConnectRequest`/`OnPlayerJoined`): validar perfil retenido, generación, participante `Raiding`, no terminal, no resuelto; **rechazar** (sin efectos) cualquier otro intento, incluido un `Defeated` (FU-1). Al aceptar: rebind, `IsDisconnected=false` (el presupuesto **se pausa y conserva**), sin `SpawnPlayer` ni inicialización fresca.
4. **Puerta de sesión:** `IsOpen=true` solo mientras exista ≥1 retenido (regla pura + aplicación en el dueño actual de `IsOpen`, `NetworkMatchController`), cerrando de nuevo al no quedar ninguno. Aplicar el mecanismo decidido en la Etapa 1.
5. Estado preservado: Activo y Abatido recuperan control **en su estado vigente** (incluidos cambios ocurridos durante la desconexión); una Derrota ya resuelta **no se revierte**; no se duplican avatar, inventario, cadáver ni resultados; la reinicialización no sobrescribe estado retenido (`Spawned()` restore-safe donde aplique).
6. Presentación del cliente que reconecta: HUD/cámara/minimapa se enlazan al avatar actual del participante (patrón `RecoveredAsClient`), sin inferir desde `runner.GetPlayerObject`.

**Tests:** EditMode (payload, reglas de puerta de sesión, validación); PlayMode escritos para rebind Activo/Downed, rechazo de no retenido/Defeated, no duplicación, regresión de Host Migration.

**Fuera de alcance:** flujo cliente en Pueblo, Continuar sin compañero.

**Configuración manual:** según lo que introduzca (probablemente ninguna; si hay referencias serializadas nuevas, pasos exactos). **Validación manual:** primer bloque de pruebas Host+cliente con builds (matar el proceso del cliente, volver a unirse con una herramienta/stub si el flujo cliente aún no existe — el walkthrough debe indicar cómo).

---

### Etapa 5 — Flujo cliente de reconexión (TASK-188 · parte 3)

**Objetivo:** que el jugador desconectado pueda **volver a la Raid por decisión propia** desde el Pueblo.

**Leer antes:** `SessionConnectionCoordinator` (`OnRaidRunnerShutdown`, `RecoverFromUnexpectedShutdown`, estados), `SessionConnectionState`/`StateMachine`, `TownRaidPreparationPresenter`/`View`, `RaidConnectionRequest`/`RaidCode`.

**Alcance:**
1. `RaidReconnectTicket` (**en memoria, process-local**, A-2): se crea al caer inesperadamente un cliente cuya participación estaba `Raiding`; contiene solo identidad de reconexión (RaidCode, ProfileId, generación). Se descarta al reconectar con éxito, ante rechazo del Host (A-3) o al cerrar la app.
2. Tras la caída, el flujo existente de retorno a Pueblo se **conserva** (incluida la confirmación de reserva ya existente) y se agrega la oferta de reconexión.
3. Transición de coordinador para reconectar (reutilizar `ConnectingRaid` con modo "reconexión" si la máquina de estados lo permite sin romper sus tests; si no, justificar y agregar estado mínimo): **no** crea ni confirma reservas de loadout, **no** usa `RaidAdmissionData`, envía el payload de la Etapa 4.
4. UI mínima en Pueblo: botón **"Volver a la Raid"** (+ mensaje de rechazo). **Bloqueo de la Preparación de Raid** mientras haya ticket válido (A-1).
5. Manejo de fallos: rechazo, Host inexistente, timeout → estado válido de Pueblo y ticket descartado/mantenido según causa (documentar tabla causa→efecto).

**Tests:** EditMode (state machine, creación/descarte del ticket, bloqueo de preparación, tabla causa→efecto).

**Fuera de alcance:** reintento automático/cuenta regresiva (FU-4); reconexión tras Derrota (FU-1).

**Configuración manual:** crear en el HUD de Pueblo el botón y asignar referencias serializadas (jerarquía y campos exactos en el walkthrough). **Validación manual:** caída de cliente → Pueblo → botón → vuelve al mismo avatar; intento con participación ya resuelta → rechazo limpio.

---

### Etapa 6 — «Continuar sin compañero» (TASK-189)

**Objetivo:** que el compañero conectado pueda resolver explícitamente la ausencia de quien agotó su presupuesto, forzando su Derrota definitiva.

**Leer antes:** GD 02 §13–14, 06 §18–20, 07 §23; **documento 05 vía `grimhold-docs` para P-1 (si contradice D-7 → detenerse y preguntar)**; `PlayerCharacter`, `PlayerCorpseGenerationController`, `NetworkRaidParticipant` (abandono voluntario como precedente), patrón de autorización de `RequestReturn` (RPC → `RpcInfo.Source` → `GetPlayerObject` → participante → `ProfileId`).

**Alcance:**
1. **Derrota forzada:** API interna acotada en `PlayerCharacter` que resuelve la Derrota por el camino existente (cadáver con Inventario+Equipamiento en la posición actual, Loot **no** pasa al superviviente), deja la vitalidad coherente (`IsAlive=false`), limpia Downed si corresponde (A-7) y registra `Defeated`/`DefeatConfirmed`. **Sin daño sintético, Kill XP ni progreso PvP.** Idempotente.
2. **RPC de confirmación** (Input Authority del compañero → State Authority): el cliente **no envía objetivo**; el Host resuelve al único compañero congelado, evalúa con las reglas de la Etapa 2 y aplica. Ordenamiento single-writer en el Host para resolver las **carreras**: gana el primero (reconexión o confirmación); el posterior falla limpiamente sin modificar la participación.
3. Garantías: agotar el presupuesto **no** aplica derrota; reconectar antes de confirmar elimina la opción (derivada); presupuesto agotado no se reinicia; confirmaciones repetidas **no** duplican cadáver, Loot, progreso ni resultados; una confirmación rechazada no cambia nada.
4. Efectos: el desconectado sale de la composición requerida; el superviviente continúa y **conserva la cuota original** (regresión: ningún camino recalcula la cuota; hoy es config inmutable por jugador); el resultado de Progresión resuelto es el definido en P-1.
5. **UI (botón en pantalla):** extender el HUD de compañero (`RaidTeammateHudPresenter/View`) para mostrar "Compañero desconectado" y, cuando `IsDecisionAvailable`, el botón **"Continuar sin compañero"** con confirmación. Debe resolver al compañero retenido por `ProfileId` (ya no tiene `PlayerRef`/PlayerObject). Presentación solo observa estado confirmado.

**Tests:** EditMode (reglas, idempotencia lógica); PlayMode escritos para forzado Activo y Abatido, doble confirmación, carrera reconexión↔confirmación, rechazo sin efectos.

**Fuera de alcance:** XP/Resultados del forzado más allá de lo existente (FU-1), extracción de equipo, inventar plazos.

**Configuración manual:** botón y textos en el HUD de compañero (jerarquía y referencias exactas en el walkthrough). **Validación manual:** escenario Duo en Host+2 clientes (ver §7).

---

### Etapa 7 — Integración con extracción, cierre de Raid y validación final

**Objetivo:** cerrar los bordes con extracción y Match closure y dejar la evidencia de validación Host/Client.

**Leer antes:** `ExtractionArchitecture`, `PlayerExtractionController`, `ExtractionZone`, `ExtractionSanctuary`, `NetworkMatchController`.

**Alcance:**
1. **Gate de extracción (06 §16–17):** un participante desconectado **no puede iniciar ni completar** su extracción y, si el countdown estaba en curso, **se cancela**; el ritual del Santuario **no** se cancela por desconexión (06 §11). Un Abatido conectado dentro del área sigue siendo válido.
2. Composición requerida: usar la regla de la Etapa 2 donde corresponda en el estado actual; el **requisito grupal Duo completo** queda como FU-2 (reportar como blocker de alineación total con GD 06).
3. Cierre de Raid: verificar sin fugas el cierre natural/forzado con retenidos, fallback sin decisor (P-2) y limpieza idempotente.
4. Regresión de Host Migration (sin retenidos) y documentación de la limitación con retenidos (D-6).
5. Actualizar la arquitectura: estados "Implemented", matriz de pruebas, limitaciones y follow-ups finales.

**Tests:** EditMode y PlayMode escritos; sin cambios de reglas de Game Design.

**Configuración manual:** ninguna nueva salvo que el walkthrough lo indique. **Validación manual:** batería completa de §7.

---

## 6. Plantilla de walkthrough (obligatoria al cerrar cada etapa)

Archivo: `Project Grimhold/Docs/Walkthroughs/US-52/Etapa-N-<slug>.md`

```markdown
# Walkthrough — US-52 · Etapa N: <título>

## 1. Resumen de cambios
- Resultado entregado (1–3 líneas) y TASK/AC cubiertos.
- Lista de archivos creados/modificados (ruta + 1 línea de propósito).
- Qué quedó FUERA de la etapa y por qué. Blockers / conflictos / follow-ups detectados.
- Estado de criterios de aceptación de la etapa (cumplido / parcial / pendiente de validación).
- Qué se ejecutó realmente (p. ej. "revisión estática del diff") y qué NO se ejecutó (compilación, tests, Play Mode, multijugador).

## 2. Instrucciones de implementación manual en Unity
Pasos numerados y exactos (o "No se requiere"): prefab/escena/asset, componente, jerarquía de UI,
campos serializados a asignar, creación de ScriptableObject (menú y valores de prueba), regeneración/verificación
de Fusion `NetworkedBehaviours` si aplica. Indicar qué tests de composición fallarán hasta completar cada paso.

## 3. Instrucciones de validación manual
- Compilación y tests a correr (EditMode/PlayMode: nombres de clases/tests).
- Escenarios Host + clientes con builds (precondiciones, pasos, resultado esperado, qué observar en Console).
- Qué reportar de vuelta a Antigravity si algo falla (logs, tests rojos).

## 4. Información para el commit
**Título** (Conventional Commit, inglés, ≤ 72 columnas, sin punto final)
**Descripción** (inglés, 72 columnas: resultado y motivo; contratos/config a verificar; tests agregados; docs actualizados)
**Archivos excluidos** (con motivo y acción sugerida, p. ej. cambios serializados accidentales o solo-EOL)
```

---

## 7. Batería de validación manual (Host + 2 clientes con builds)

Topologías: **T1** Host+C1 mismo equipo (Duo), C2 otro equipo · **T2** C1+C2 mismo equipo, Host solo.

| # | Escenario | Resultado esperado |
|---|---|---|
| S1 | C1 Activo pierde conexión (matar proceso) | Avatar permanece, expuesto; no se despawnea; sin Derrota/Abandono/Extracción |
| S2 | C1 vuelve con "Volver a la Raid" | Mismo avatar, vida, inventario y posición actual; sin duplicados |
| S3 | C1 Abatido se desconecta | El drain sigue; reconecta a mitad y retoma estado Abatido vigente |
| S4 | Presupuesto agotado sin reconexión | Sin derrota automática; el compañero ve el botón |
| S5 | Compañero confirma | C1 sufre Derrota definitiva, cadáver con su Loot en su posición; superviviente continúa; intento de reconexión posterior es rechazado limpio |
| S6 | Reconecta **antes** de confirmar | La opción desaparece; presupuesto sigue agotado; nueva desconexión la rehabilita de inmediato |
| S7 | Confirmación repetida/simultánea con reconexión | Un solo resultado; sin duplicar cadáver/Loot/progreso/resultados |
| S8 | C1 vuelve a Pueblo y no reconecta | Sigue retenido; Preparación bloqueada; el compañero puede decidir |
| S9 | Compañero muere/abandona mientras C1 retenido | Fallback sin decisor (P-2) documentado y sin fugas |
| S10 | Desconectado dentro del área de extracción | No extrae; countdown en curso se cancela; ritual no se cancela |
| S11 | Solo (sin compañero) se desconecta | Comportamiento legacy (D-4) |
| S12 | Regresión Host Migration sin retenidos | Igual que antes; con retenido: limitación documentada observada |

---

## 8. Riesgos y notas

- **Mayor incógnita técnica:** re-join en Fusion 2.1.1 hacia una sesión reabierta (Etapa 1 la analiza; Etapa 4/5 la confirman en la práctica con builds).
- `NetworkSpawnManager` es un archivo muy grande: cualquier lógica de dominio nueva va en colaboradores; cuidar los diffs.
- Los tests de composición de prefab fallarán hasta que el usuario complete los pasos manuales: es esperado y debe avisarse en cada walkthrough.
- La extracción sigue siendo individual: el bloqueo "grupal" de 06 §15 **no** queda garantizado por esta US (FU-2).
# E0 — Contrato funcional: menú personal de Town

## Estado y propósito

**Estado:** entregable de E0 reducido según [implementation-plan.md](implementation-plan.md#e0-primer-contrato-funcional-reducido). Pendiente de aprobación.

**Baseline:** rama `feat/qa-runner`, commit `152c01e3`, 2026-10-12. Los cambios de QA-003 sin commit no forman parte de este contrato.

Este documento fija la feature objetivo, sus expectativas con fuente propietaria, las precondiciones, la matriz de capacidades y los conflictos. No define plantillas visuales, regiones, tiempos, formato de evidencia ni esquema de escenario: se definen en E6/E10a sobre la UI vigente, porque la UI actual es provisoria (GD UI §12 tiene pendiente UI.2; `TownPlayerMenu.prefab` usa colores provisorios).

No autoriza implementar, ejecutar builds, enviar input ni capturar pantallas.

## 1. Feature y alcance

**Feature:** abrir y cerrar el menú personal de Town (`TownPlayerMenuPresenter`) desde el teclado, en la pestaña Inventario.

### Caso principal

| ID | Paso | Acción |
|---|---|---|
| P1 | Abrir | Tab con el menú cerrado. |
| P2 | Cerrar | Tab con el menú abierto en Inventario. |
| V1 | Cerrar con Escape | Tab para abrir, luego Escape. |

### Variantes documentadas y excluidas

| ID | Variante | Motivo de exclusión |
|---|---|---|
| X1 | C abre/cierra Atributos | Conflicto C4 con GD UI §5.4. |
| X2 | Escape con el menú cerrado abre Options | Comportamiento distinto; Options incluye Log out/Exit, con riesgo de terminar la sesión. |
| X3 | Tab rechazado mientras otro panel tiene supresión | Requiere interactuar con un NPC (E11). |
| X4 | Tab con otra pestaña abierta cambia a Inventario | Cobertura posterior; no aporta al caso mínimo. |
| X5 | Bloqueo y liberación del input de gameplay | No observable por imagen; requiere E8 (se evalúa en E10b). |
| X6 | Pestaña Abilities | No tiene tecla propia; requiere click en la barra de pestañas. |

Fuera de alcance: combate, persistencia de Equipment/Off Hand, mutaciones de inventario, multijugador y exploración.

## 2. Precondiciones

| ID | Precondición | Fuente | Cubre |
|---|---|---|---|
| PRE-1 | Build identificada (commit/configuración). | Plan §4 | E3 |
| PRE-2 | Cuenta QA con personaje ya creado. Sin personaje, el login deriva a creación de personaje (`MainMenuController.HandleLoginFlowResult`). | Código | E1 |
| PRE-3 | Autenticación por login real: no hay recuperación de sesión (ver C1a); el token vive en memoria (`LoginFlowController`). | Código | E5/E7 |
| PRE-4 | Host en Town: login exitoso → `CreateRoom` → `SessionConnectionCoordinator.ConnectToTownAsync` → escena `Lobby-Town`. Requiere sesión Fusion/Photon configurada. | Código | E1/E7 |
| PRE-5 | `SocialPlayer` local con Input Authority y menú enlazado: `TownInventoryBinder` exige perfil del join igual al de `ApplicationStashContext`, `LoadoutService`, `LocalInputContext`, catálogo y `PlayerInputReader` no nulo. Sin enlace, Tab no hace nada. | Código | E7 (observable solo por E10a/E8) |
| PRE-6 | Ningún panel con supresión de input abierto: diálogo, Atributos, Merchant, invitación de party, Stash, Preparación de Expedición. No se encontró apertura automática al aparecer. | Código | Fixture |
| PRE-7 | Mission Board cerrado (lee Escape por la API legacy; ver C5). | Arquitectura | Fixture |
| PRE-8 | Menú cerrado y personaje lejos de NPC/interactuables (el prompt de interacción no debe aparecer). | Arquitectura/código | Fixture |
| PRE-9 | Estado No Listo. Listo no bloquea la apertura (solo mutaciones de equipamiento), pero puede mostrar indicaciones de bloqueo (verificado en la pestaña Abilities) que alterarían la observación. | GD 02 §7; código | Fixture |
| PRE-10 | Ventana del proceso propio con foco y tamaño de área cliente medido. Default 960×540, modo ventana, no redimensionable (`ProjectSettings.asset`), pero no se debe asumir el tamaño. | Configuración | QA-002/E4 |
| PRE-11 | Al menos un HUD registrado visible antes de abrir (candidato: HUD de progresión). Si no hay ninguno, EXP-4/EXP-7 no son discriminables y se reportan INCONCLUSIVE. | Arquitectura | Confirmar en E10a |

## 3. Expectativas

Fuentes: **GD** = Game Design vía `grimhold-docs`; **Arq** = `Project Grimhold/Docs/Architecture/TownPlayerMenuArchitecture.md`; **Código** = implementación en la baseline.

| ID | Paso | Esperado | Fuente | Observación | Evalúa | Sin evidencia |
|---|---|---|---|---|---|---|
| EXP-1 | P1 | La ventana del menú se hace visible. | Arq (Ownership, `TownMenuTabState`); Código (`TryOpenTab` → `Opened`) | Imagen | E10a | INCONCLUSIVE |
| EXP-2 | P1 | La pestaña seleccionada es Inventario (título "Inventory"). | Arq (Tab registry); Código (`TownMenuTabIds`, `TownPlayerMenuView`) | Imagen | E10a | INCONCLUSIVE |
| EXP-3 | P1 | Inventario y Equipamiento visibles a la vez. | GD UI §5.2; Arq (Inventory tab hospeda `RaidInventoryView`) | Imagen | E10a | INCONCLUSIVE |
| EXP-4 | P1 | Los HUD registrados quedan ocultos mientras el menú está abierto. | Arq (HUD while the menu is open) | Imagen (requiere PRE-11) | E10a | INCONCLUSIVE |
| EXP-5 | P1 | El input de gameplay queda bloqueado mientras el menú está abierto. | Arq (Ownership). GD 16 §6 lo define solo para Dungeon (C3). | Estado | E10b | Excluido de E10a |
| EXP-6 | P2 | La ventana se cierra. | Arq; Código (`Open` sobre la misma pestaña → `Closed`) | Imagen | E10a | INCONCLUSIVE |
| EXP-7 | P2 | Los HUD registrados vuelven a verse como antes de abrir. | Arq (`TownHudVisibility` restaura valores) | Imagen (requiere PRE-11) | E10a | INCONCLUSIVE |
| EXP-8 | P2/V1 | El input de gameplay se libera al cerrar. | Arq | Estado | E10b | Excluido de E10a |
| EXP-9 | V1 | Escape cierra el menú y la misma pulsación no abre Options. | Arq (Escape ownership); Código (`InventoryCloseRequested` consumido) | Imagen | E10a | INCONCLUSIVE |

Un resultado contradicho por observación confiable es FAIL. Un oráculo ausente nunca produce PASS (plan §4). En E10a, EXP-5 y EXP-8 figuran como exclusiones declaradas de cobertura, no como aserciones aprobadas.

Las PlayMode tests existentes (`TownPlayerMenuPresenterPlayModeTests`, `TownMenuTabStateTests`, `PlayerInputReaderPlayModeTests`) ya cubren esta lógica en aislamiento. El valor del recorrido es comprobarla sobre una build real con input real.

## 4. Evidencia requerida (conceptual)

- Capturas del área cliente antes de P1, después de P1 y después de P2/V1.
- Tamaño medido del área cliente y build identificada.
- Ninguna captura de la pantalla de login ni de credenciales.
- Formato, nombres, tiempos y regiones: E3/E6.

## 5. Matriz de capacidades

| Capacidad | Necesaria para | Estado actual | Cubre |
|---|---|---|---|
| Lanzar, supervisar y limpiar el proceso | Todo | Existe (QA-001) | — |
| Ventana por PID/HWND, foco exacto, captura | Todo | Existe (QA-002) | — |
| Teclas Tab y Escape | P1, P2, V1 | Falta: `DEMO_KEYS` no las incluye | E6 |
| Detección de menú principal listo | Entrada | Parcial: QA-003 sin commit, sin comprobación real | Precondición de E4 |
| Click en *Create Room*/Login | PRE-3, PRE-4 | Falta | E4 |
| Escritura de credenciales | PRE-3 | Falta | E5 |
| Cuenta QA con personaje y reset | PRE-2 | Falta (`seed-test-accounts.js` no alcanza) | E1 |
| Configuración Fusion/Photon del ambiente QA | PRE-4 | Sin verificar | E1 |
| Detección de Town listo / jugador enlazado | PRE-4, PRE-5 | Falta (no hay logs ni instrumentación) | E7/E10a |
| Referencias visuales del menú y HUD | EXP-1–4, 6, 7, 9 | Falta; UI provisoria | E10a |
| Log del Player por ejecución | Diagnóstico | Falta (`usePlayerLog: 1` en configuración) | E3 |
| Recorrido de varios pasos | P1→P2 | Falta | E6 |
| Observación de supresión de input | EXP-5, EXP-8 | Falta | E8 |

## 6. Conflictos y vacíos registrados

| ID | Tema | Fuentes | Impacto en el contrato | Decisión pendiente |
|---|---|---|---|---|
| C1a | Recuperación de sesión | GD UI §4.1: recupera sesión al iniciar. Código: sin persistencia de token ni restore. | Hoy cada proceso nuevo arranca sin sesión; PRE-3 usa login real. Si se implementa, E4 y E7 deben garantizar el estado de sesión. | Producto/backend: ¿se implementa? |
| C1b | Opciones del menú principal | GD UI §4.2 lista Jugar/Configurar/Créditos/Salir/sesión. GD 02 §5 es permisivo ("según UI/UX"). Código: solo *Create Room* (Join oculto). | Ninguno directo; el recorrido usa *Create Room*. | Producto/UI |
| C2 | Teclas, ventana con pestañas, HUD oculto | Solo Arq; GD no define el menú personal. | EXP-1, 2, 4, 6, 7, 9 se respaldan en Arquitectura. | Confirmar que Arquitectura es dueña de estas reglas. |
| C3 | Bloqueo de input en Town | GD 16 §6 lo define para Dungeon; en Town solo Arq. | EXP-5/EXP-8 se respaldan en Arquitectura. | Game Design |
| C4 | Atributos como pestaña | GD UI §5.4: estadísticas y build en interfaz independiente. Implementación: pestaña del mismo menú. | X1 excluida. | Game Design/UI |
| C5 | Escape en Mission Board | Arq (Known limits): API legacy puede abrir Options. | PRE-7 lo evita. | Deuda técnica conocida |
| C6 | Ocultamiento del entorno | GD UI §3: interfaces de gameplay activo no deben ocultar todo el entorno. No está claro si aplica a Town. | No se usa como expectativa. | Game Design/UI |

## 7. Decisión sobre QA-003

QA-003 no es precondición de E0. Es precondición de E4 cuando tenga commit propio y una comprobación real del gate registrada. Para E10a no alcanza: Town necesita su propia detección de "listo". Estado actual: sin commit, con pruebas sintéticas y sin ejecución real registrada.

## 8. Verificaciones realizadas

Lectura de código, prefabs, configuración y documentos; no se ejecutaron builds, Unity ni tests.

- Bindings: `Assets/Input/PlayerInputActions.inputactions` (Tab, Escape, C en el mapa `LocalUI`; sin gamepad).
- Reglas del menú: `TownPlayerMenuPresenter.TryOpenTab` y handlers de Escape; `TownMenuTabState.Open` (`Ignored`/`Opened`/`Switched`/`Closed`). Sin logs de apertura/cierre.
- Enlace: `TownInventoryBinder.TryBindLifecycle` y `OnInputReaderChanged`; `SocialPlayer.prefab` cablea `TownPlayerMenuPresenter`, `TownInventoryBinder` y `TownAttributeAssignmentPresenter._menu` (Atributos es pestaña hospedada).
- HUD: `_hudRoots` = `TownInteractionHud` y `TownPartyHud`; HUD de progresión registrado en runtime. Party HUD oculto en solo. Town no tiene minimapa.
- Supresión: `DialoguePresenter`, `TownAttributeAssignmentPresenter`, `TownMerchantPresenter`, `TownPartyInvitationPresenter`, `TownStashPresenter`, `TownRaidPreparationPresenter`. Ready no bloquea la apertura (`TownInventoryBinder.CanMutateEquipment`).
- Sesión: sin `PlayerPrefs` ni almacenamiento de token en `Assets/Scripts`; `MainMenuController.CreateRoom` muestra login sin sesión; *Back* vuelve al menú principal.
- Ventana: sin `Screen.SetResolution` en `Assets/Scripts`; `ProjectSettings.asset` 960×540, `fullscreenMode: 3`, `resizableWindow: 0`, `runInBackground: 1`, `usePlayerLog: 1`.
- Escenas habilitadas: `MainMenu`, `Gameplay`, `Lobby-Town` (`EditorBuildSettings.asset`).
- GD vía `grimhold-docs`: UI Auditoría §3, §4, §5, §12; 02 §5, §6, §7, §19; 16 §6; 01 §5.

### Sin verificar

- Que el HUD de progresión sea visible en reposo (PRE-11).
- Estado por defecto de la raíz de `TownInteractionHud`.
- Que la `PlayerPrefs` de FusionMenu (`Photon.Menu.Resolution`) no se aplique en otras escenas.
- Configuración de AppID/región de Photon.
- Detalle de `ConnectToTownAsync` y de los disparadores de Stash/Merchant/invitación.

## 9. Criterios de aceptación de E0

- [x] Cada expectativa tiene fuente propietaria (§3).
- [x] Precondiciones y resultado evaluable inequívocos, con su comportamiento ante evidencia ausente (§2, §3).
- [x] Conflictos registrados con decisión pendiente (§6).
- [x] Ningún detalle depende de UI provisoria (§4, sin plantillas ni regiones).
- [x] Decisión sobre QA-003 registrada (§7).

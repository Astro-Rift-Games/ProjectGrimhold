# Plan de Implementación — Sistema de Misiones (Story #373 US-42)

**Repositorio:** Astro-Rift-Games/ProjectGrimhold — branch `Mission-System`
**Unity:** 6.5.1f1 — **Networking:** Photon Fusion (Host/Client en Raid, Shared Mode en Pueblo)
**Fuente funcional:** `12 - Sistema de Misiones` (Google Drive, ídem PDF adjunto)
**Alcance:** TASK-161 a TASK-169 (9 tareas, misma User Story)

---

## 0. Instrucciones para Antigravity (obligatorias)

1. **Nunca implementar más de una Etapa por iteración.** Al terminar una Etapa, Antigravity se detiene, entrega el walkthrough `.md` correspondiente y espera aprobación explícita antes de tocar código de la Etapa siguiente.
2. Cada Etapa debe dejar el proyecto **compilando** y con **todos los tests automatizados en verde** (EditMode como mínimo). No se entregan etapas con código "a medio integrar".
3. Seguir estrictamente `Project Grimhold/AGENTS.md`: fuentes de verdad (código actual > `Docs/Architecture/` > `AGENTS.md` > Game Design en Drive), separación dominio puro C# / frontera Fusion, sin frameworks nuevos, sin abstracciones especulativas, diffs acotados al resultado pedido.
4. Antes de escribir código de cualquier etapa, releer el/los documento(s) de `Docs/Architecture/` que esa etapa toca (listados en cada sección) y el documento `12 - Sistema de Misiones`.
5. Cada Etapa entrega:
   - Código + tests.
   - Un archivo `Docs/Architecture/MissionSystem*.md` cuando la etapa introduce una decisión arquitectónica nueva (se indica en cada etapa si aplica).
   - Un walkthrough `docs/plan-mission-system/Etapa-N-<slug>.md` con: resumen de cambios, archivos tocados, mensaje de commit sugerido (subject en inglés, conforme a `AGENTS.md`), pasos de validación manual en el Editor de Unity, y configuración de Inspector/escena necesaria (si aplica).
   - Lista explícita de qué quedó **fuera** de esa etapa y por qué.
6. No declarar una etapa completa si queda un criterio de aceptación de su TASK sin resolver. Si durante la implementación aparece trabajo que pertenece a otra etapa o a otro sistema, reportarlo en vez de expandir el alcance.
7. No reclamar validación manual, Play Mode o pruebas multijugador que no se hayan ejecutado realmente.

---

## 1. Hallazgo previo que requiere tu confirmación

`Docs/Architecture/` no contiene ningún documento de arquitectura técnica del Sistema de Misiones. TASK-161 depende de "TECH — Definir arquitectura técnica del Sistema de Misiones" como si ya existiera ("siguiendo la arquitectura aprobada"), pero esa tarea no está entre las 9 que me diste y el documento no existe en el repo.

Decisión que tomé para no bloquear el plan, sujeta a tu aprobación: **la Etapa 1 incluye la redacción de `Docs/Architecture/MissionSystemArchitecture.md`** como entregable técnico, antes de escribir código de dominio. Ese documento fija las decisiones que todas las etapas siguientes dan por sentadas (ver §2). Si preferís que esa arquitectura se defina en una iteración separada, previa a la Etapa 1, decímelo y ajusto el plan.

---

## 2. Decisiones de arquitectura transversales (a formalizar en Etapa 1)

Estas decisiones surgen de inspeccionar el código y los documentos de arquitectura existentes, y son las que hacen que las 9 etapas encajen sin choques posteriores. Se documentan en `MissionSystemArchitecture.md` y no se repiten en cada etapa.

### 2.1 Separación de capas (Clean Code / SOLID)

Siguiendo el patrón ya usado en `Progression/` y `Scenario/Extraction/` (ej. `ExpeditionExperienceRules` puro + `PlayerExpeditionExperienceLedger` como frontera Fusion):

- **Dominio puro C#** (sin `UnityEngine`, sin Fusion): definiciones de Misión, catálogo, máquina de estados de ciclo de vida, motor de progreso de objetivos/fases. Testeable 100% en EditMode sin Play Mode.
- **Frontera de persistencia**: extensión de `LocalProfileStore` / `ILocalProfileRepository` (agregado de perfil local, proceso, no Fusion), siguiendo el mismo patrón transaccional que Stash/Loadout/Progresión.
- **Frontera de red (Raid, Host/Client)**: adaptadores `NetworkBehaviour` delgados que traducen hechos autoritativos ya resueltos (una eliminación, una interacción con cofre) en eventos de contribución a Misiones. No hay lógica de reglas de Misión dentro de estos adaptadores.
- **Frontera de red (Pueblo, Shared Mode)**: NPC interactuable siguiendo el patrón de `TownStashNpcInteractable`/`TownMerchantNpcInteractable` — el `NetworkBehaviour` solo valida la interacción; aceptar/reclamar una Misión es una transacción contra `LocalProfileStore`, igual que Stash.

### 2.2 Dónde vive el estado de Misiones

`LocalPlayerPersistenceArchitecture.md` y `Persistence-Strategy-Vertical-Slice.md` son explícitos: el agregado de perfil (`LocalProfileStore` + `InMemoryLocalProfileRepository`) es el único almacén persistente del MVP; el backend real (`/backend`) todavía no hidrata este agregado. Por lo tanto:

- El catálogo estático de Misiones (definiciones) es contenido — ScriptableObjects, igual que `LootDefinitionCatalog`. No es parte del perfil.
- El **estado mutable** de Misiones (activas, fase actual, progreso, pendiente de Claim, Reputación de Guild, rotación diaria/semanal) se agrega a `LocalProfileSnapshot` como una sección más del agregado, con su propio bump de `SchemaVersion` (mismo mecanismo que la migración de Weapon Sets ya documentada).
- Esto es consistente con el "Fuera de alcance: Persistencia física" declarado explícitamente en TASK-163.

### 2.3 Cómo llega el progreso desde una Raid (Host/Client) hasta el perfil local (proceso del cliente dueño)

Este es el punto que requiere una decisión explícita porque el proyecto ya resolvió un problema estructuralmente idéntico para Loot extraído y XP de Kill, y Misiones debe seguir el mismo patrón en vez de inventar uno nuevo:

- Un hecho autoritativo (eliminación fatal, apertura válida de cofre) ocurre bajo **State Authority**, que en Host/Client puede ser el proceso de *otro* jugador del Dúo. `LocalProfileStore` es local a cada proceso — el Host no puede escribir el perfil del Cliente directamente.
- Patrón existente a reutilizar (`DamageResolver.TryAwardFatalKillExperience`, `ProgressionArchitecture.md` §"Kill Experience producer"): State Authority resuelve el hecho, lo entrega de forma síncrona a un componente co-ubicado en el `NetworkRaidParticipant` del jugador dueño (una nueva pieza, p. ej. `PlayerMissionContributionLedger`, `[Networked]`, mínima), que lo replica.
- El **Input Authority** de ese participante (el proceso del jugador dueño de la Misión) es el único que evalúa la contribución contra sus propias definiciones y estado de Misión (motor de TASK-164, puro y determinístico) y commitea el resultado a su propio `LocalProfileStore` — mismo principio que ya usa el flujo de extracción ("State Authority retains the raid result snapshot while Input Authority commits it to its application-level Loadout").
- Esto respeta el diseño funcional: cada Misión es propiedad individual del jugador, el motor de progreso no decide si el hecho ocurrió (lo recibe ya resuelto), y evita construir un servicio de misiones "autoritativo" en Fusion que no existe hoy ni se pide en el alcance.
- La contribución cooperativa en Dúo (tabla §8.2 del documento de diseño) se resuelve en el productor (`DamageResolver`, `NetworkLootContainerInteractable`): cuando la familia lo permite, el productor entrega el hecho al ledger de **todos** los participantes elegibles presentes en la misma Zona, no solo al atacante/interactor.

### 2.4 Identificador de Zona

Hoy el proyecto solo tiene identificadores de zona para Extracción (`ExtractionZone`). No existe un identificador de Zona genérico para combate/exploración. El documento de diseño lo anticipa ("condiciones por Zona cuando exista un identificador estable disponible", TASK-165) y lo deja fuera de alcance de Misiones ("La implementación de una zona... corresponde a Dungeon/Level Design").

**Decisión:** el motor de objetivos (TASK-164) soporta condiciones de Zona en su contrato de datos (un `string`/ID opcional), pero mientras no exista una fuente autoritativa de Zona genérica en el proyecto, ninguna familia de objetivos del vertical slice (Eliminación PvE, Interacción) exigirá Zona como condición obligatoria, y la evaluación de presencia conjunta en Dúo (§8.3 del diseño) se implementa usando el identificador de zona más específico que sí exista y sea válido para ese evento (p. ej. el `RaidGenerationId`/instancia actual como aproximación de "mismo Dungeon"), documentando la limitación. Esto no bloquea ninguna de las 9 tareas y evita inventar un sistema de Zonas que no fue pedido.

### 2.5 Estructura de carpetas propuesta

Siguiendo la convención existente (una responsabilidad por carpeta, sin capas artificiales tipo MVC):

```
Assets/Scripts/Missions/                 (dominio puro: definiciones, estado, motor)
Assets/Scripts/Missions/Persistence/     (extensión de LocalProfileStore, snapshot, reglas)
Assets/Scripts/Missions/Networking/      (adaptadores Fusion: ledger de contribución, NPC de Guild)
Assets/Scripts/Scriptable Objects/Missions/  (assets de contenido: catálogo, contratos)
Assets/Tests/EditMode/Missions/          (tests de dominio y persistencia)
```

Antigravity puede ajustar nombres puntuales si un archivo encaja mejor en una carpeta ya existente (p. ej. si el ledger de contribución termina siendo más natural en `Networking/` junto a los demás adaptadores de Town/Raid), siempre que la separación dominio/persistencia/red se mantenga.

---

## Resumen de etapas

| Etapa | TASK | Entregable central |
|---|---|---|
| 1 | TASK-161 | Arquitectura técnica + Definiciones y catálogo de Misiones (dominio puro) |
| 2 | TASK-162 | Estado y ciclo de vida de una Misión aceptada (dominio puro) |
| 3 | TASK-163 | Persistencia del estado de Misiones en el perfil local |
| 4 | TASK-164 | Motor de progreso de Objetivos y Fases (dominio puro) |
| 5 | TASK-165 | Integración con Eliminaciones PvE (primer vertical slice jugable) |
| 6 | TASK-166 | Integración con Interacción de Cofres |
| 7 | TASK-167 | Oferta y aceptación de Contratos de Guild (NPC del Pueblo) |
| 8 | TASK-168 | Claim idempotente de Misiones |
| 9 | TASK-169 | Integración de recompensas de Misiones con XP (cierre del vertical slice) |

Cada etapa depende únicamente de la anterior (mismo orden que las dependencias declaradas en los tickets).

---

## Etapa 1 — TASK-161: Arquitectura técnica + Definiciones y catálogo de Misiones

### Objetivo
Fijar la arquitectura técnica del sistema (§2 de este plan, formalizada en un documento) e implementar la representación estática y configurable de Misiones: Misión, Fases, Objetivos, Condiciones, Parámetros, Recompensas, Tipo, Rango, IDs, y el catálogo que las resuelve.

### Documentos a leer antes de implementar
`12 - Sistema de Misiones` (Drive) §3, §5, §6, §13, §18. `Project_Grimhold_Contratos_Base_v1.md` (para el estilo de contratos value-type ya establecido). `LootDefinitionCatalog.cs` como referencia directa de patrón de catálogo por ID.

### Alcance
- `Docs/Architecture/MissionSystemArchitecture.md`: documenta las decisiones de §2 (capas, dónde vive el estado, flujo Raid→perfil, límite de Zona) como arquitectura aprobada del sistema, referenciando este plan.
- Tipos de dominio puro (sin `UnityEngine`, sin Fusion) para:
  - `MissionId`, `PhaseDefinition`, `ObjectiveDefinition`, `ObjectiveFamily` (enum: EliminacionPvE, EliminacionEspecifica, Exploracion, Interaccion, Obtencion, ExtraccionDeObjetos, Extraccion, PvP — vocabulario cerrado del §6 del documento de diseño), `ObjectiveCondition` (target específico, zona opcional, cantidad requerida), `RewardDefinition` (XP, Reputación, Oro, Objetos, Equipamiento — como datos, sin ejecutar ninguna entrega todavía), `MissionRank` (E/D/C, con B/A/S declarados pero fuera de contenido), `MissionType` (Normal/Única/Semanal).
- ScriptableObjects de contenido: `MissionDefinition` (compone las fases/objetivos de arriba) y `MissionDefinitionCatalog` (resuelve por ID, detecta duplicados, comportamiento determinista ante ID inválido — mismo patrón que `LootDefinitionCatalog`).
- Validación de que una definición inválida falla de forma explícita (no silenciosa) al cargar el catálogo.
- Contenido mínimo de prueba: los 6 Contratos Rango E que exige el documento de diseño (§18), como assets `.asset` reales, para poder validar el catálogo con datos reales — no placeholders vacíos.

### Fuera de alcance de esta etapa
Progreso mutable del jugador, persistencia, UI, contenido de Rango D/C, cadenas narrativas.

### Criterios de aceptación (copiados del ticket, deben quedar 100% cubiertos)
Una Misión declarable sin código específico por Misión; una definición con una o varias fases; una fase con uno o varios objetivos; parámetros y condiciones declarables; recompensas describibles sin aplicarlas todavía; IDs estables y únicos; una definición inválida falla de forma explícita; catálogo cubre resolución, duplicados y configuraciones inválidas en tests.

### Validación manual esperada en Unity
Crear los 6 assets de Contrato Rango E desde el menú `Create > Grimhold > Missions > ...`, asignarlos al catálogo, confirmar en el Inspector que el catálogo no reporta duplicados y que forzar un ID duplicado dispara el error esperado (log, no excepción silenciosa).

---

## Etapa 2 — TASK-162: Estado y ciclo de vida de una Misión

### Objetivo
Representar el estado individual de una Misión aceptada por un personaje y las transiciones válidas de su ciclo de vida.

### Documentos a leer antes de implementar
`12 - Sistema de Misiones` §9, §9.1, §10, §16 (casos borde de ciclo de vida). `MissionSystemArchitecture.md` (Etapa 1).

### Alcance
- `MissionInstanceState`: identidad de la Misión, estado actual (`Disponible → Activa → Completada → PendienteDeReclamar → Reclamada`, más `Abandonada`), fase actual, progreso por objetivo, fases completadas, flags de pendiente-de-Claim y reclamado — todo como dominio puro, separado de la definición estática (Etapa 1).
- Reglas de transición puras (`MissionLifecycleRules` o similar, mismo estilo que `CharacterProgressionRules`/`ExpeditionExperienceRules`): qué transiciones son válidas, y rechazo explícito de transiciones inválidas (no se puede reclamar una Misión incompleta, una Misión reclamada no vuelve a un estado anterior, etc.).
- Restricciones estructurales: máximo de 3 Misiones Normales activas simultáneas (§10), Misiones Únicas consumen ese mismo límite, Semanales quedan fuera de ese límite y usan su propio contrato de vigencia (preparado en el tipo pero sin implementar rotación todavía — eso es TASK-167 en adelante).

### Fuera de alcance de esta etapa
Persistencia física, integraciones con gameplay real, reclamo de recompensas, UI.

### Criterios de aceptación
Estado mutable separado de la definición estática; solo transiciones válidas permitidas; no se reclama una Misión incompleta; una Misión reclamada no regresa a estado anterior; el progreso conserva correctamente la fase activa; los límites de slots definidos por Game Design pueden validarse; tests cubren transiciones válidas e inválidas.

### Validación manual esperada en Unity
Ninguna specific de escena (dominio puro sin MonoBehaviours todavía). Confirmar únicamente que el proyecto compila y los tests EditMode nuevos corren desde el Test Runner.

---

## Etapa 3 — TASK-163: Persistir el estado de Misiones en el perfil

### Objetivo
Integrar `MissionInstanceState` (Etapa 2) con `LocalProfileStore` para que aceptación, progreso y Claim sobrevivan entre sesiones dentro del mismo proceso.

### Documentos a leer antes de implementar
`LocalPlayerPersistenceArchitecture.md` completo (especialmente la sección de migración de schema y el patrón transaccional). `Persistence-Strategy-Vertical-Slice.md` §1, §5 (qué queda explícitamente fuera del backend hoy).

### Alcance
- Extender `LocalProfileSnapshot` con la sección de Misiones: Misiones activas, fase actual, progreso de objetivos, Misiones completadas pendientes de Claim, y el estado mínimo necesario para impedir Claims duplicados (ver Etapa 8).
- Bump de `SchemaVersion` con migración explícita: un perfil sin datos de Misiones previos sigue siendo válido (valores por defecto: sin Misiones activas).
- Transacción en `LocalProfileStore` para escribir el estado de Misiones, siguiendo el mismo patrón atómico usado por Stash/Loadout/Progresión (candidato → validación del repositorio → publicación de `ProfileCommitted` solo tras éxito).

### Fuera de alcance de esta etapa
Progreso generado durante gameplay real (Etapa 4/5/6), UI, recompensas, rotaciones.

### Criterios de aceptación
El estado de Misiones es parte del perfil persistente existente; no se crea un segundo sistema de persistencia paralelo; guardar y volver a cargar reconstruye el mismo estado; un perfil sin datos previos de Misiones sigue siendo válido; tests cubren round-trip y perfil sin datos de Misiones.

### Validación manual esperada en Unity
Desde una escena de Town ya existente (o un harness EditMode-only si no hace falta Play Mode), confirmar que el agregado de perfil serializa/deserializa sin errores con y sin datos de Misiones. Si no se requiere Play Mode para esto, decirlo explícitamente en el walkthrough.

---

## Etapa 4 — TASK-164: Motor de progreso de Objetivos y Fases

### Objetivo
Implementar el núcleo que recibe una acción de gameplay ya confirmada (autoritativa) y determina qué objetivos, fases y Misiones deben avanzar. El motor no decide si el hecho ocurrió; lo recibe ya resuelto.

### Documentos a leer antes de implementar
`12 - Sistema de Misiones` §5, §6, §7, §8.2, §16 completo (casos borde de integridad). `MissionSystemArchitecture.md` (contrato de "hecho autoritativo" definido en Etapa 1, §2.3 de este plan).

### Alcance
- `MissionContributionEvent` (value type, dominio puro): familia de objetivo, cantidad, target específico opcional, zona opcional (ver §2.4), identidad del jugador propietario, flag de "contribución de compañero".
- `MissionProgressEngine` (o nombre equivalente), puro: dado un `MissionContributionEvent` y el estado+definiciones actuales del jugador, determina qué objetivos de la fase activa progresan, aplica incremento parcial, límite máximo (sin overflow), completado de objetivo, avance de fase (solo la fase activa procesa eventos), completado de la última fase → Misión Completada.
- Soporta objetivos paralelos dentro de una fase y fases secuenciales entre sí.
- Define el contrato de entrada mínimo necesario para que TASK-165/166 puedan integrarse sin cambiarlo (familias iniciales: Eliminación PvE, Interacción — las que efectivamente se integran en este vertical slice).

### Fuera de alcance de esta etapa
Detectar acciones de gameplay reales (eso es Etapa 5/6), persistencia de sistemas externos, UI, contenido.

### Criterios de aceptación
El motor no depende directamente de Combate, Loot, Interacción o Extracción; una acción solo afecta objetivos compatibles; los filtros de enemigo, Zona, objeto y cantidad pueden evaluarse; el progreso nunca supera el requisito; completar todos los objetivos requeridos completa la fase; completar la última fase completa la Misión; puede procesar objetivos paralelos; puede procesar fases secuenciales; tests cubren progresión parcial, completado, filtros y duplicados (un mismo evento no puede aportar progreso duplicado — §16).

### Validación manual esperada en Unity
Ninguna de escena. Confirmar compilación y suite EditMode completa (incluye ahora Etapas 2, 3 y 4).

---

## Etapa 5 — TASK-165: Integrar Misiones con Eliminaciones PvE

### Objetivo
Conectar el resultado autoritativo de una eliminación PvE con el motor de progreso, habilitando el primer Contrato Rango E jugable como vertical slice completo.

### Documentos a leer antes de implementar
`Project_Grimhold_Contratos_Base_v1.md` §"Individual extraction progress contracts" y §"Kill Experience producer" en `ProgressionArchitecture.md` (patrón exacto a replicar). Código actual: `Combat/DamageResolver.cs`, `Combat/EntityRegistry.cs`, `Core/IExtractionProgressDefeatSource.cs`, `Scenario/Extraction/ExtractionProgressDefeatSource.cs`.

### Alcance — decisión arquitectónica de esta etapa
Esta etapa introduce la pieza de red descrita en §2.3: un componente `[Networked]` co-ubicado en `NetworkRaidParticipant` (ledger de contribución de Misiones) que State Authority escribe tras una eliminación fatal válida, replicado al Input Authority dueño, que evalúa localmente contra el motor de la Etapa 4 y commitea a su `LocalProfileStore` (Etapa 3). Se sigue el mismo orden síncrono ya exigido por `ProgressionArchitecture.md` para productores one-shot (validar disponibilidad → pedir aplicación → confirmar solo tras aceptación, sin await ni RPC intermedio del lado State Authority).

- Nueva capability de entidad (patrón `IExtractionProgressDefeatSource`) para identificar el tipo/identidad de enemigo requerida por Misiones, sin acoplar el motor a clases concretas de enemigo.
- Hook en `DamageResolver` (método nuevo, análogo a `TryAwardFatalProgress`/`TryAwardFatalKillExperience`) que, tras una eliminación fatal aplicada, resuelve el/los participante(s) elegibles (incluye Dúo cuando la familia lo permite, §8.2) y entrega un `MissionContributionEvent` (Etapa 4) a cada ledger correspondiente.
- Soporta "Derrota X enemigos" y "Derrota X enemigos de tipo Y"; condición por Zona solo si existe identificador estable disponible en ese punto de integración (ver §2.4 — si no existe, se documenta explícitamente como no soportado en esta etapa, sin bloquear el resto).

### Fuera de alcance de esta etapa
PvP, XP de combate, sistema de daño, balance de enemigos.

### Criterios de aceptación
Una muerte PvE válida puede incrementar una Misión compatible; una muerte no compatible no modifica progreso; puede filtrarse por tipo de enemigo; el mismo resultado fatal no genera dos contribuciones; un proxy o cliente no puede inventar una eliminación (State Authority-only); no se modifica el contrato autoritativo actual de Combate; cobertura automatizada para éxito, filtro y duplicado.

### Validación manual esperada en Unity
- Configurar el nuevo campo de identidad de enemigo en al menos un prefab de enemigo existente.
- En una sesión Host/Client (Solo primero, luego Duo si es viable en el entorno de test), aceptar el Contrato "Derrota X enemigos" (requiere que Etapa 7 exista — **si Etapa 7 todavía no está implementada, la validación de esta etapa se hace con un harness de test o forzando estado inicial por código/Inspector**, y se debe aclarar esto en el walkthrough), derrotar enemigos y confirmar en un log/Inspector que el progreso avanza.
- Confirmar que una eliminación de un enemigo no compatible con el filtro no mueve el progreso.

---

## Etapa 6 — TASK-166: Integrar Misiones con Interacción de Cofres

### Objetivo
Permitir que una interacción válida con un cofre contribuya a objetivos de Misiones de tipo Interacción, reutilizando la plomería de red creada en la Etapa 5.

### Documentos a leer antes de implementar
`LootInteractionArchitecture.md` completo, en particular la sección del productor de "First-open Exploration Experience" sobre `NetworkLootContainerInteractable` (patrón casi idéntico al que se necesita acá).

### Alcance
- Hook en `NetworkLootContainerInteractable` (o componente hermano co-ubicado, sin modificar su contrato de interacción existente) que, tras una apertura válida, entrega un `MissionContributionEvent` de familia Interacción al mismo ledger de contribución creado en la Etapa 5.
- La contribución depende únicamente del punto autoritativo que confirma la interacción, no de la UI local ni de observar visualmente el cofre (igual que la XP de first-open).
- Reabrir el mismo cofre no debe generar progreso adicional cuando la regla exige primera apertura; la contribución conserva identidad suficiente para impedir duplicados.
- Contribución en Dúo para Interacción respeta la tabla §8.2 (sí contribuye).

### Fuera de alcance de esta etapa
Transferencia de Loot, objetivos de Obtención (son una familia distinta), contenido del cofre, UI de Misiones.

### Criterios de aceptación
Una interacción válida con un cofre puede incrementar el objetivo correspondiente; una interacción rechazada no genera progreso; reabrir el mismo cofre no genera progreso adicional cuando la regla lo exige; la contribución conserva identidad suficiente para impedir duplicados; se respetan las reglas de contribución en Dúo; no se modifica el ownership existente de Loot/Interacción.

### Validación manual esperada en Unity
Con al menos un cofre en escena, interactuar y confirmar avance del objetivo de tipo Interacción; reabrir el mismo cofre y confirmar que no vuelve a progresar cuando corresponde primera apertura únicamente.

---

## Etapa 7 — TASK-167: Ofrecer y aceptar Contratos de Guild

### Objetivo
Implementar el flujo mínimo por el cual el jugador consulta Contratos disponibles y acepta una Misión Normal, mediante un NPC en el Pueblo. Sin rotación diaria todavía: oferta fija/configurada.

### Documentos a leer antes de implementar
`12 - Sistema de Misiones` §4 completo. Código actual: `Networking/TownStashNpcInteractable.cs`, `Networking/TownMerchantNpcInteractable.cs`, `Networking/TownMerchantNetworkController.cs`, `Player/Presentation/TownMerchantView.cs`/`TownMerchantPresenter.cs` (patrón de presentación a replicar para la UI mínima de consulta/aceptación).

### Alcance
- `GuildContractNpcInteractable` (Shared Mode, patrón `TownStashNpcInteractable`): solo valida la interacción y expone el punto de entrada; no muta estado de Misión directamente.
- Flujo de aceptación como transacción de `LocalProfileStore` (Etapa 3): consulta de Contratos disponibles (desde el catálogo de la Etapa 1, con una oferta fija configurada — sin pesos ni rotación), validación de rango requerido, validación de slots disponibles (límite de la Etapa 2), prevención de aceptación duplicada, creación del estado persistente de la Misión.
- Oferta inicial del vertical slice: Contrato de Eliminación PvE + Contrato de Interacción con cofres (los dos ya integrados en Etapas 5 y 6).
- UI mínima (puede ser deliberadamente simple/funcional, sin arte final) para consultar y aceptar, siguiendo el patrón de presentación ya usado para Merchant/Stash.

### Fuera de alcance de esta etapa
Rotación diaria, Semanales, cadenas únicas, UI final del NPC (arte/UX pulido).

### Criterios de aceptación
El jugador puede consultar una oferta configurada; puede aceptar un Contrato válido; aceptar crea el estado de Misión correspondiente; el estado aceptado se persiste; no puede superar el límite de Misiones Normales; no puede aceptar dos veces una Misión cuando sus reglas lo impiden; un Contrato bloqueado por rango no puede aceptarse.

### Validación manual esperada en Unity
Colocar/confirmar el NPC de Guild en la escena de Town, interactuar, consultar la oferta, aceptar un Contrato, salir a una Raid, generar progreso real (Etapas 5/6) y confirmar en Inspector/log que el estado persiste al volver a Town.

---

## Etapa 8 — TASK-168: Claim idempotente de Misiones

### Objetivo
Implementar el flujo por el cual una Misión Completada puede reclamarse exactamente una vez y pasar a Reclamada. Define la transacción de Claim, sin implementar todavía la entrega concreta de cada tipo de recompensa.

### Documentos a leer antes de implementar
`12 - Sistema de Misiones` §14 completo, §16 (reglas de integridad de Claim). `LocalPlayerPersistenceArchitecture.md` §"Progression transaction" (mecanismo de watermark/receipt at-most-once ya usado para XP consolidada — mismo principio a reutilizar para Claim).

### Alcance
- Flujo de Claim contra el NPC de Guild (mismo componente de la Etapa 7): solicitud de Claim, validación de estado (solo Pendiente de reclamar admite Claim), validación de contexto/NPC requerido, resolución de las recompensas configuradas (Etapa 1) sin aplicarlas todavía a sistemas concretos, transición atómica a Reclamada, prevención de Claims repetidos ante reintentos/desconexión.
- Mecanismo de idempotencia explícito (reutilizando el patrón watermark/receipt del perfil, no una solución nueva): un Claim repetido con la misma solicitud no debe reclamar dos veces ni reaplicar recompensas.
- El sistema puede trabajar con implementaciones concretas de recompensa sin conocer su lógica interna (contrato preparado para que la Etapa 9 lo complete).

### Fuera de alcance de esta etapa
Implementar Oro, Objetos o Equipamiento como recompensas reales; balance de recompensas; UI final.

### Criterios de aceptación
Solo una Misión Pendiente de reclamar admite Claim; una Misión incompleta rechaza el Claim; una Misión ya reclamada rechaza nuevos Claims; retry/repetición de la misma solicitud no duplica recompensas; la transición a Reclamada y la aplicación de recompensas respetan atomicidad; tests cubren Claim válido, inválido y duplicado.

### Validación manual esperada en Unity
Completar una Misión en Raid (progreso vía Etapa 5/6), volver a Town, interactuar con el NPC, ejecutar Claim y confirmar transición a Reclamada; forzar un segundo Claim (doble click / reenvío) y confirmar que no se duplica nada.

---

## Etapa 9 — TASK-169: Integrar recompensas de Misiones con XP

### Objetivo
Implementar la primera recompensa real conectando el Claim de Misiones con la Progresión persistente del personaje, cerrando el vertical slice sin depender todavía de Economía u Objetos.

### Documentos a leer antes de implementar
`ProgressionArchitecture.md` completo (ya leído en profundidad para este plan) — en particular `ConsolidatedExperienceApplicationRules` y el mecanismo de `LastAppliedProgressionResultSequence` en `LocalPlayerPersistenceArchitecture.md`, que es el patrón que esta etapa debe reutilizar (XP de Misión se aplica al perfil local igual que la XP consolidada de expedición, **no** a través del `PlayerExpeditionExperienceLedger` de Raid, porque el Claim ocurre en Town).

### Alcance
- Cuando un Claim válido (Etapa 8) incluye una recompensa de XP: resolver el personaje propietario, aplicar la cantidad configurada mediante `CharacterProgressionRules`/el mecanismo existente de `LocalProfileStore`, respetando las reglas actuales de persistencia e idempotencia (mismo watermark, sin abrir un segundo camino de aplicación de XP paralelo al ya existente).
- Confirmar el resultado al Claim (la Misión no queda "colgada" si la aplicación de XP fue exitosa).

### Fuera de alcance de esta etapa
Reputación de Guild, Oro, Objetos, Equipamiento (quedan preparados por el contrato de Etapa 8 pero no implementados).

### Criterios de aceptación
Una recompensa XP válida incrementa la progresión persistente; un Claim duplicado no genera XP adicional; Misiones no recalcula niveles ni duplica las reglas de Progresión (las reutiliza); el resultado persiste correctamente; tests cubren entrega y duplicación.

### Validación manual esperada en Unity
Reclamar una Misión con recompensa de XP configurada y confirmar en el Inspector/HUD de Town que el nivel/XP persistente del personaje aumenta la cantidad esperada; reclamar dos veces (ver Etapa 8) y confirmar que la XP no se duplica.

---

## 3. Qué NO cubre este plan (fuera de alcance de las 9 tareas)

Reputación de Guild como progresión jugable, rangos D/C con contenido real, rotación diaria (pesos, reset global), Misiones Semanales, cadenas narrativas de NPC, PvP, Objetos/Equipamiento/Oro como recompensas reales, persistencia durable contra el backend Node/Mongo, y todo lo listado en la sección 20 del documento de diseño. Cualquiera de estos puntos, si aparece como necesario durante la implementación de las 9 etapas, se reporta como trabajo fuera de alcance en vez de incorporarse silenciosamente.

# Plan de implementación — Autoridad de servidor (#361, #362, #363)

**Branch:** `feature/server-authoritative-state`
**Repos afectados:** `backend/` (Node/Express + Mongoose/MongoDB) y `Project Grimhold/` (Unity, cliente)
**Tickets:** #363 (Raid Reservation), #362 (Attribute Points), #361 (Extracción de Raid)

---

## 0. Reglas de proceso (obligatorias, aplican a todas las etapas)

1. **Una sola etapa por iteración.** No se avanza a la siguiente etapa sin aprobación explícita del usuario.
2. Al terminar cada etapa, se debe entregar:
   - Resumen breve del cambio realizado.
   - Instrucciones de validación manual (qué probar y cómo).
   - Info de commit (mensaje sugerido, listo para usar).
3. No modificar archivos fuera del alcance declarado de la etapa en curso, aunque se detecten otras mejoras posibles (anotarlas para un ticket aparte).
4. Cada etapa debe dejar el proyecto en estado compilable/ejecutable — no se permiten etapas que rompan el build a mitad de camino.
5. Las etapas que tocan backend Y cliente están separadas explícitamente (una de backend, una de Unity) porque se validan de formas distintas (`npm test` vs Play Mode en el Editor).

---

## Índice de etapas

| # | Ticket | Repo | Descripción |
|---|--------|------|-------------|
| 1 | #363 | Backend | Derivar la Raid Reservation desde el inventario persistido |
| 2 | #363 | Unity | Adaptar el cliente al nuevo contrato de reserva (solo `reservationId`) |
| 3 | #362 | Backend | Validación autoritativa de un punto de atributo |
| 4 | #362 | Unity | Adaptar el cliente al nuevo contrato de asignación de atributos |
| 5 | #361 | Backend | Endurecer el flujo de extracción (sin fallback al payload del cliente) |
| 6 | #361 | Documentación | Dejar planteado el gap arquitectónico real (no es código) |

---

## Etapa 1 — Backend: Raid Reservation autoritativa (#363)

### Objetivo
Que `savePendingReservation` derive el contenido de la reserva desde lo que el backend ya tiene persistido (`character.inventory.loadout` + `character.inventory.preparedEquipment`), no desde lo que manda el cliente en el body.

### Archivos a tocar
- `backend/src/services/InventoryService.js` → función `savePendingReservation`
- `backend/src/validators/inventory.validators.js` → validador usado por la ruta de reserva
- `backend/src/routes/inventory.routes.js` (solo si la ruta lee campos del body que dejan de existir)
- Nuevo archivo: `backend/tests/inventory.reservation.test.js`

### Especificación técnica

**Contrato actual (a reemplazar):**
```
POST /character/me/inventory/reservation
Body: { reservationId, items: [...], preparedEquipment: {...} }
```
El body completo se persiste tal cual en `character.pendingReservation`.

**Contrato nuevo:**
```
POST /character/me/inventory/reservation
Body: { reservationId }
```

**Lógica de `savePendingReservation(accountId, reservationId)`:**
1. Buscar el `Character` por `accountId`.
2. **Idempotencia:** si `character.pendingReservation` ya existe y su `reservationId` coincide con el recibido, devolver esa reserva existente sin volver a mutar nada (soporta reintentos del cliente sin duplicar ni perder estado).
3. Si no hay reserva pendiente (o tiene otro `reservationId` obsoleto — definir si se sobrescribe o se rechaza; recomendado: se sobrescribe, ya que una reserva vieja sin confirmar se considera abandonada):
   - Construir el contenido de la reserva **leyendo directamente** `character.inventory.loadout` y `character.inventory.preparedEquipment` tal como están persistidos en ese momento.
   - Escribir esa data en `character.pendingReservation` (incluyendo el nuevo `reservationId`).
   - Vaciar `character.inventory.loadout` y `character.inventory.preparedEquipment` (mismo comportamiento final que hoy, pero el contenido sale de la DB, no del request).
   - Guardar todo en una sola operación atómica (mismo mecanismo que ya usa el código actual — revisar si usa `.save()` con documento completo o `findOneAndUpdate`; mantener ese patrón para no introducir una inconsistencia nueva).
4. Devolver la reserva resultante (items + preparedEquipment) en la respuesta, para que Unity pueda mostrar exactamente qué se reservó (aunque ya no lo haya mandado él).

### Criterios de aceptación (mapeados del ticket)
- [ ] El cliente no puede introducir items inexistentes en una reserva (ya no puede introducir ninguno — el body no los acepta).
- [ ] El cliente no puede alterar los Equipment Slots mediante el snapshot de reserva.
- [ ] `pendingReservation` coincide exactamente con el estado persistido inmediatamente anterior a la reserva.
- [ ] Una reconexión (mismo `reservationId`) restaura el mismo estado sin pérdida ni duplicación.

### Tests a crear (`inventory.reservation.test.js`, siguiendo el patrón de mocks existente en `tests/`)
- Reserva se construye a partir de `loadout`/`preparedEquipment` persistidos, ignorando cualquier `items`/`preparedEquipment` que venga en el body (mandar body con basura y verificar que se ignora).
- Segunda llamada con el mismo `reservationId` devuelve la misma reserva sin mutar de nuevo (idempotencia).
- `loadout` y `preparedEquipment` quedan vacíos en `character.inventory` tras reservar.

### Validación manual
1. Correr `npm test` en `backend/` y confirmar que pasan los tests nuevos y los existentes (nada se rompe).
2. Con Postman/curl o el propio cliente (aunque el cliente todavía no esté actualizado en esta etapa, se puede probar con curl): loguear un usuario de test, armar loadout/equipo vía los endpoints existentes, y hacer `POST /character/me/inventory/reservation` con `{ "reservationId": "test-1" }` — confirmar en la respuesta (o consultando el Character en Mongo) que la reserva refleja exactamente lo que estaba persistido, y que enviar `items`/`preparedEquipment` falsos en el body no tiene ningún efecto.
3. Repetir la misma llamada con el mismo `reservationId` y confirmar que no duplica ni cambia nada.

### Info de commit
```
fix(backend): derive raid reservation from persisted inventory instead of client payload

savePendingReservation ahora construye el contenido de la reserva
leyendo character.inventory.loadout y preparedEquipment ya persistidos,
en vez de confiar en los items/preparedEquipment que manda el cliente.
El endpoint pasa a aceptar solo { reservationId }. Se agrega idempotencia:
reintentar con el mismo reservationId devuelve la reserva existente sin
duplicar ni perder estado.

Archivos:
- backend/src/services/InventoryService.js
- backend/src/validators/inventory.validators.js
- backend/tests/inventory.reservation.test.js (nuevo)

Ref: TASK-363
```

---

## Etapa 2 — Unity: Adaptar el cliente al nuevo contrato de reserva (#363)

### Objetivo
Dejar de mandar `items`/`preparedEquipment` al crear una reserva, ya que el backend los ignora desde la Etapa 1.

### Archivos a tocar
- `Assets/Scripts/Backend/InventoryDataTransferObjects.cs` → `SaveReservationRequest`
- `Assets/Scripts/Backend/InventoryClient.cs` → `SavePendingReservationAsync`
- `Assets/Scripts/Stash/RemoteInventoryService.cs` → `SavePendingReservationAsync`
- Revisar `Assets/Scripts/Networking/SessionConnectionCoordinator.cs` (único caller fuera de los archivos de backend) por si depende de campos que dejan de mandarse.

### Especificación técnica
1. `SaveReservationRequest` pasa a tener solo `reservationId` (sacar `items` y `preparedEquipment` del struct).
2. `RemoteInventoryService.SavePendingReservationAsync(PendingLoadoutReservation reservation)`:
   - Ya no arma `items`/`preparedEquipment` para el request.
   - **Importante:** el chequeo actual de `WeaponSetAOffHand`/`WeaponSetBOffHand` (que hoy bloquea la reserva si hay algo en offhand, devolviendo `UNSUPPORTED_EQUIPMENT_LAYOUT`) debe **mantenerse** — sigue siendo una limitación real del backend (no tiene esos campos en el schema), independientemente de este cambio.
   - Puede simplificarse la firma para solo mandar `reservationId`, o mantenerla recibiendo `PendingLoadoutReservation` por compatibilidad con el caller pero ya no serializando su contenido — evaluar cuál genera menos fricción en `SessionConnectionCoordinator.cs`.
3. Si el backend ahora devuelve el contenido real de la reserva en la respuesta (ver Etapa 1, punto 4), evaluar si conviene que el cliente actualice su estado local con esa respuesta en vez de asumir que su cálculo local es el vigente (refuerza la autoridad del servidor también del lado de la UI).

### Validación manual
1. Abrir el proyecto en el Editor de Unity, entrar en Play Mode con un usuario de test contra el backend local (levantado desde la Etapa 1).
2. Equipar ítems, armar loadout, entrar a una raid (disparar el flujo de reserva).
3. Confirmar en los logs que la request de reserva ya no incluye `items`/`preparedEquipment`, y que la reserva se aplica igual que antes desde la perspectiva del jugador.
4. Confirmar que si hay algo en un slot offhand, la reserva sigue fallando con `UNSUPPORTED_EQUIPMENT_LAYOUT` como antes (no se rompió ese caso).

### Info de commit
```
fix(client): stop sending inventory snapshot when creating a raid reservation

El cliente ya no manda items/preparedEquipment al crear una reserva —
el backend los deriva del inventario persistido (ver commit anterior
en backend/). SaveReservationRequest pasa a llevar solo reservationId.

Archivos:
- Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- Assets/Scripts/Backend/InventoryClient.cs
- Assets/Scripts/Stash/RemoteInventoryService.cs

Ref: TASK-363
```

---

## Etapa 3 — Backend: Validación autoritativa de Attribute Points (#362)

### Objetivo
Reemplazar el `$set` directo de `characterAttributes` por un modelo de intención de un punto por vez, validado y calculado en el servidor.

### Archivos a tocar
- `backend/src/services/ProgressionService.js` → `commitProgression`
- `backend/src/validators/` → validador de la ruta (crear si no existe uno específico)
- `backend/src/routes/progression.routes.js`
- Nueva constante de balance: `maximumAttributeValue = 25` (mirror exacto de `ProgressionBalanceDefaults.InitialMaximumAttributeValue` en Unity) — ubicarla en `backend/src/config/progressionBalance.js`, junto a las constantes de XP/niveles que ya existen ahí.
- Nuevo archivo: `backend/tests/progression.attributes.test.js`

### Especificación técnica

**Contrato actual (a reemplazar):**
```
POST /character/me/progression/commit
Body: { characterAttributes: { vitality, resistance, strength, dexterity, intelligence, luck, availablePoints } }
```
Se persiste tal cual vía `$set`.

**Contrato nuevo:**
```
POST /character/me/progression/commit
Body: { attribute: "Vitality" | "Resistance" | "Strength" | "Dexterity" | "Intelligence" | "Luck" }
```
(Usar los mismos nombres que el enum `CharacterAttribute` de Unity — ver mapeo abajo — para minimizar fricción en el cliente.)

**Mapeo de nombres (Unity `CharacterAttribute` enum → campo Mongo en `characterAttributes`):**
| Unity | Mongo |
|---|---|
| `Vitality` | `vitality` |
| `Resistance` | `resistance` |
| `Strength` | `strength` |
| `Dexterity` | `dexterity` |
| `Intelligence` | `intelligence` |
| `Luck` | `luck` |

**Lógica de `commitProgression(accountId, attribute)` (replica `CharacterAttributeAssignmentRules.TryAssign` de Unity):**
1. Buscar el `Character` por `accountId`, leer `characterAttributes` persistido.
2. Validar que `attribute` sea uno de los 6 valores permitidos (400 si no).
3. Validar `availablePoints > 0` (si no, rechazar — no se modifica el personaje).
4. Validar que el valor actual del atributo pedido sea `< maximumAttributeValue` (25) (si no, rechazar).
5. Si pasa validación: incrementar ese atributo en 1, decrementar `availablePoints` en 1, persistir ambos cambios en una única mutación atómica (mismo documento, un solo `findOneAndUpdate`/`save`).
6. Devolver el `characterAttributes` resultante completo (para que el cliente actualice su vista con el estado confirmado por el servidor).

### Criterios de aceptación (mapeados del ticket)
- [ ] El cliente no puede crear Attribute Points (no hay forma de mandar un valor absoluto).
- [ ] No puede asignar más puntos de los disponibles (`availablePoints` se valida antes de aplicar).
- [ ] Una petición inválida no modifica el personaje (ej. `availablePoints == 0`, o atributo ya en el tope).
- [ ] Una operación válida descuenta exactamente los puntos gastados (1 por request).
- [ ] Cerrar y volver a abrir el juego recupera exactamente los atributos confirmados por backend (esto ya lo garantiza la persistencia normal del Character; con esta etapa además queda garantizado que lo persistido es válido).

### Tests a crear (`progression.attributes.test.js`)
- Asignación válida: incrementa el atributo pedido en 1 y descuenta `availablePoints` en 1.
- Rechazo cuando `availablePoints == 0` (el documento no cambia).
- Rechazo cuando el atributo ya está en `maximumAttributeValue` (25) (el documento no cambia).
- Rechazo de un `attribute` inválido/inexistente (400, el documento no cambia).
- Dos requests consecutivas válidas acumulan correctamente (no hay condición de carrera básica — usar la misma operación atómica en ambas llamadas).

### Validación manual
1. `npm test` en `backend/`.
2. Con curl/Postman: loguear un usuario de test con `availablePoints > 0`, mandar `POST /character/me/progression/commit` con `{ "attribute": "Vitality" }` varias veces y confirmar en Mongo que `vitality` sube de a 1 y `availablePoints` baja de a 1.
3. Vaciar `availablePoints` (a mano en Mongo o gastándolos todos) y confirmar que una request adicional es rechazada sin cambiar nada.
4. Subir un atributo a 25 (a mano en Mongo) y confirmar que una request adicional sobre ese atributo es rechazada.

### Info de commit
```
fix(backend): validate attribute point assignment authoritatively

commitProgression ya no acepta el objeto characterAttributes completo
del cliente. Ahora recibe la intención de asignar un punto a un
atributo puntual, valida contra el estado persistido (availablePoints
disponibles, tope de 25 por atributo) y calcula/persiste el nuevo
estado en el servidor.

Archivos:
- backend/src/services/ProgressionService.js
- backend/src/routes/progression.routes.js
- backend/src/config/progressionBalance.js
- backend/tests/progression.attributes.test.js (nuevo)

Ref: TASK-362
```

---

## Etapa 4 — Unity: Adaptar el cliente al nuevo contrato de asignación de atributos (#362)

### Objetivo
Que el cliente mande la intención (un atributo) en vez del estado completo, y que refleje el resultado confirmado por el backend.

### Archivos a tocar
- `Assets/Scripts/Backend/ProgressionDataTransferObjects.cs` → `CommitProgressionRequest`
- `Assets/Scripts/Player/Presentation/TownAttributeAssignmentPresenter.cs`
- Revisar `RemoteInventoryService`/`ProgressionClient` (el que arme y mande el request) según corresponda.

### Especificación técnica
1. `CommitProgressionRequest` pasa a llevar `attribute` (string, uno de los 6 valores) en vez del objeto `characterAttributes` completo.
2. En `TownAttributeAssignmentPresenter`, el flujo actual es: `_store.TryAssignCharacterAttribute(attribute, ...)` (mutación local optimista) → armar DTO con el estado completo → `CommitProgressionAsync`. Pasa a ser: `_store.TryAssignCharacterAttribute(...)` (se mantiene, sigue siendo válido como UX optimista para que la UI responda al instante) → mandar solo `{ attribute }` → **al recibir la respuesta del backend, reconciliar el estado local con el `characterAttributes` que confirma el servidor** (en vez de asumir que el cálculo local es el definitivo). Si el backend rechaza (ej. sin puntos disponibles — no debería pasar si la UI ya lo valida localmente, pero puede pasar por desync), revertir la mutación local optimista.
3. Mantener la regla de "un punto por click" que ya existe en la UI (no cambia el flujo de interacción, solo el contrato de red).

### Validación manual
1. Play Mode contra el backend local (con la Etapa 3 aplicada).
2. Asignar puntos de atributo desde la UI de ciudad, click por click, y confirmar que sube de a 1 y que `availablePoints` baja correctamente en pantalla.
3. Gastar todos los puntos disponibles y confirmar que la UI ya no permite asignar más (validado tanto local como por el rechazo del servidor si se fuerza).
4. Cerrar sesión y volver a loguear: confirmar que los atributos asignados persisten exactamente como se dejaron.

### Info de commit
```
fix(client): send attribute assignment intent instead of full state

TownAttributeAssignmentPresenter ahora manda { attribute } al backend
en vez del characterAttributes completo, y reconcilia el estado local
con la respuesta autoritativa del servidor tras cada asignación.

Archivos:
- Assets/Scripts/Backend/ProgressionDataTransferObjects.cs
- Assets/Scripts/Player/Presentation/TownAttributeAssignmentPresenter.cs

Ref: TASK-362
```

---

## Etapa 5 — Backend: Endurecer el flujo de extracción de Raid (#361)

### Alcance realista de esta etapa (confirmado con el usuario)
No se construye un productor genuinamente confiable de `AuthoritativeExtractionResult` en esta etapa — eso requiere un componente que hoy no existe (ver Etapa 6). Esta etapa **endurece lo que sí es responsabilidad del backend**: dejar de confiar en el payload del cliente cuando no hay un resultado autoritativo, y cerrar las puertas traseras (endpoints legacy/mock) en producción.

### Archivos a tocar
- `backend/src/services/ExtractionCommitService.js` → método `commit`
- `backend/src/routes/inventory.routes.js` → endpoint `/debug/mock-fusion-result` y endpoint legacy `/me/inventory/extraction`
- `backend/src/config/env.js` → nueva variable de entorno para gatear endpoints de desarrollo
- Nuevo archivo: `backend/tests/extraction.commit.hardening.test.js` (o extender el test existente `extraction.commit.test.js`)

### Especificación técnica

**1. Quitar el fallback "Stage 1" en `ExtractionCommitService.commit`:**
Hoy (confirmado en el código): si `AuthoritativeExtractionResult.findOne({ raidId, accountId })` no encuentra nada, el servicio cae a usar `payload.items`/`payload.progression`/el equipo mandado por Unity. Este fallback se elimina: si no existe un `AuthoritativeExtractionResult` para esa `raidId`+`accountId`, la operación **falla explícitamente** (ej. 409/422 con un error claro tipo `NO_AUTHORITATIVE_RESULT`) y **no se modifica el Character**.

**2. Gatear (no necesariamente borrar todavía) los endpoints de desarrollo:**
- `/debug/mock-fusion-result`: envolver con un chequeo `if (env.nodeEnv === 'production') return 404` (agregar `nodeEnv` a `env.js`, leyendo `process.env.NODE_ENV`). En dev/staging sigue disponible, porque **hoy es el único productor de `AuthoritativeExtractionResult` que existe** — sin él, no hay forma de probar ni de operar el juego en ningún ambiente hasta que exista un productor real (ver Etapa 6). Documentarlo claramente con un comentario en el código.
- `/me/inventory/extraction` (endpoint legacy): mismo criterio — si nada más lo usa, gatearlo igual detrás de `nodeEnv !== 'production'`, o retirarlo directamente si se confirma que no tiene ningún consumidor activo (revisar en Unity: buscar referencias a este endpoint específico antes de decidir).

**3. Idempotencia ante reintentos (ya parcialmente cubierta):** confirmar que si se llama a `commit` dos veces con el mismo `AuthoritativeExtractionResult` ya consumido, la segunda llamada no vuelve a aplicar loot/XP (revisar si el modelo ya marca el resultado como "consumido" — si no, agregar un flag `consumedAt`/`applied` al `AuthoritativeExtractionResult`).

### Criterios de aceptación (mapeados del ticket, con el alcance acordado)
- [ ] Un cliente no puede otorgarse loot ni XP modificando el request (ya no hay fallback al payload).
- [ ] Si no existe un resultado autoritativo para la Raid, no se modifica el personaje.
- [ ] Repetir el mismo resultado no duplica ninguna recompensa (idempotencia vía flag de consumo).
- [ ] Los endpoints legacy/debug no permiten saltarse el flujo autoritativo **en producción** (quedan gateados; en dev siguen siendo necesarios porque son el único productor existente — esto se deja explícito, no oculto).

### Tests a agregar/actualizar
- `commit` sin `AuthoritativeExtractionResult` existente → rechaza, Character sin cambios.
- `commit` con resultado existente → aplica loot/progresión/equipo desde el resultado, ignorando cualquier `payload` alternativo que mande el request.
- `commit` llamado dos veces con el mismo resultado → segunda vez no duplica.
- Endpoint `/debug/mock-fusion-result` con `NODE_ENV=production` → 404.

### Validación manual
1. `npm test`.
2. Con `NODE_ENV` sin setear (dev): usar `/debug/mock-fusion-result` para crear un resultado, y confirmar que `commit` lo aplica correctamente.
3. Intentar `commit` con una `raidId` que no tiene resultado cargado → confirmar rechazo y que el Character no cambió en Mongo.
4. Setear `NODE_ENV=production` localmente y confirmar que `/debug/mock-fusion-result` devuelve 404.
5. Repetir un `commit` ya aplicado y confirmar que no se duplica loot/XP.

### Info de commit
```
fix(backend): remove client-payload fallback from extraction commit

ExtractionCommitService.commit ya no cae al payload del cliente cuando
no existe un AuthoritativeExtractionResult para la raid — la operación
se rechaza explícitamente y el Character no se modifica. Los endpoints
/debug/mock-fusion-result y /me/inventory/extraction (legacy) quedan
gateados fuera de producción vía NODE_ENV, ya que hoy son el único
productor de resultados autoritativos disponible (ver TASK-361 para
el gap arquitectónico pendiente: no hay servidor dedicado de Fusion).

Archivos:
- backend/src/services/ExtractionCommitService.js
- backend/src/routes/inventory.routes.js
- backend/src/config/env.js
- backend/tests/extraction.commit.hardening.test.js

Ref: TASK-361
```

---

## Etapa 6 — Documentar el gap arquitectónico real de #361 (no es código)

### Objetivo
Dejar explícito, por escrito, lo que esta ronda de cambios **no resuelve** y por qué, para que quede como decisión consciente del equipo y no como algo que se dio por solucionado sin serlo.

### Contenido a documentar (como comentario en el ticket #361, o un doc en `backend/docs/` si el repo tiene ese hábito)
- Las raids corren en `GameMode.Host` de Photon Fusion: un jugador hace de host de la partida. No hay ningún proceso de servidor controlado por Astro Rift Games que participe en la simulación de la raid.
- Por lo tanto, hoy no existe una fuente de verdad "del lado servidor" genuinamente confiable para producir el `AuthoritativeExtractionResult` — cualquier productor que reciba datos "de la partida" en última instancia recibe datos de la máquina de un jugador (el host), que puede ser modificada.
- El único productor existente (`/debug/mock-fusion-result`) es, y seguirá siendo tras esta ronda, un mock — gateado fuera de producción en la Etapa 5, pero **necesario en dev/staging** porque no hay reemplazo todavía.
- Opciones reales para cerrar este gap (a evaluar como iniciativa aparte, no parte de este plan):
  1. Migrar las raids a `GameMode.Server`/dedicated server de Photon Fusion, con el backend (u otro proceso propio) como el único productor confiable de resultados.
  2. Mantener el host-peer actual, pero agregar validación server-side de la partida (replay/simulación paralela) antes de aceptar un resultado — mucho más costoso de construir.
  3. Aceptar el riesgo de cheating vía host-manipulación como riesgo conocido del MVP, y priorizar esta migración para después del lanzamiento.
- Esta etapa termina con una decisión registrada (comentario en el ticket, o ticket nuevo de arquitectura), no con un commit de código.

---

## Notas finales

- El orden de las etapas respeta las dependencias reales encontradas en el código: la Etapa 1 (#363) se apoya en que el fix de persistencia de equipamiento (hecho previamente) ya garantiza que lo persistido está al día.
- Las etapas 2 y 4 (Unity) requieren tener el backend de la etapa anterior corriendo localmente para poder probarse — no se pueden validar de forma aislada.
- Ninguna etapa de este plan debe ejecutarse sin la aprobación explícita previa, incluso si el resultado de la etapa anterior salió bien.

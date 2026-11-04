# Grimhold Backend

Node.js + Express + Mongoose backend para Project Grimhold.

## Requisitos

- Node.js 20+
- MongoDB Atlas o instancia local

## Setup local

1. Copiar `.env.example` a `.env` y completar los valores locales (nunca versionar `.env` ni credenciales reales).
2. `npm install`
3. `npm run dev`

## Variables de entorno

| Variable         | Requerida                  | Descripción                                                                 |
|------------------|----------------------------|-----------------------------------------------------------------------------|
| `PORT`           | Sí                         | Puerto del servidor                                                         |
| `NODE_ENV`       | No (default `development`) | Entorno de ejecución (`development`, `test`, `staging`, `production`)       |
| `MONGODB_URI`    | Sí                         | Connection string de MongoDB (sin placeholders en producción)               |
| `JWT_SECRET`     | Sí                         | Secreto HMAC-SHA256 para firmar JWT (mín. 32 caracteres de alta entropía en prod) |
| `JWT_EXPIRES_IN` | No                         | Duración del token en segundos (default: `3600`)                            |
| `WEBHOOK_SECRET` | Sí en `production`         | Secreto compartido para endpoints internos/webhooks (mín. 32 caracteres en prod) |
| `INVENTORY_PROTOCOL_GATE` | No (default `false`) | Sólo `true` exige protocolo de inventario 2 y documentos migrados; leído desde `src/config/env.js` |
| `INVENTORY_SPLIT_ENABLED` | No (default `false`) | Sólo `true` habilita split por slot v2; leído desde `src/config/env.js`, independiente del gate |
| `ENABLE_DEV_DEBUG_ROUTES` | No (default `false`) | Sólo `true` habilita `/character/debug/mock-fusion-result` fuera de producción |
| `ENABLE_LEGACY_EXTRACTION` | No (default `false`) | Sólo `true` habilita `/character/me/inventory/extraction` fuera de producción |

## Endpoints disponibles

- `GET /health` - Estado del servidor (y de la base de datos)

## Inventario: B1 expand (slots físicos)

B1 amplía el formato y protege los datos; **no es el cutover**. B2 (T3, abajo) añade
operaciones por slot y recepción/capacidad v2. La migración explícita es T2.

### Formato y lectura

- `inventory.inventorySchemaVersion`: ausente o `1` = listas agregadas legacy; `2` = slots físicos.
  El esquema Mongoose tiene default `1`. La creación por `POST /character/me` y el seed
  crean versión `2` sólo con el gate activo, de lo contrario `1`. El seed usa la configuración
  validada del backend; nunca cambia la versión de personajes existentes.
- Las entradas de `stash`, `loadout` y `pendingReservation.items` aceptan
  `{ slot?, lootId, amount }`. `slot` es un entero no negativo opcional para compatibilidad v1.
  Los subdocumentos siguen siendo estrictos: campos futuros deben declararse en su propio expand;
  no se persisten campos arbitrarios recibidos del cliente.
- `GET /character/me/inventory` incluye `inventorySchemaVersion`. En v2 devuelve las tres listas
  ordenadas por `slot`, conservando duplicados de `lootId`, huecos y slots de overflow.
  El ordenamiento afecta sólo la respuesta, nunca el documento persistido.
- Ninguna lectura migra aliases, fusiona stacks, reasigna slots, incrementa `revision` ni guarda.
  Los documentos legacy con duplicados permanecen intactos hasta una migración explícita.
- En el contrato v2 los slots son únicos por lista, capacidad Loadout 30 / Stash 60 y overflow
  sólo reducible. B2 aplica estas reglas en las escrituras v2; ninguna lectura migra datos.

### Header y superficie del gate

Con `INVENTORY_PROTOCOL_GATE=true`, enviar **`X-Grimhold-Inventory-Protocol: 2`** además del JWT
(`Authorization: Bearer ...`). La comprobación ocurre antes de validar el body o ejecutar la
operación. Sin header o con otro valor: `426 CLIENT_UPGRADE_REQUIRED`; con header correcto y
personaje no v2: `409 INVENTORY_MIGRATION_REQUIRED`. Con el gate apagado el header se ignora.
No se migra durante la petición. Los hosts también deben actualizarse; se comprueba el documento
del destinatario de resultados y del participante de tickets, además del caller autenticado.

Rutas protegidas (prefijo `/character`):

| Método | Ruta |
| --- | --- |
| GET | `/me/inventory` |
| PUT | `/me/inventory/order`, `/me/inventory/prepared-equipment` |
| POST | `/me/inventory/stash/move-to-loadout`, `/me/inventory/loadout/move-to-stash`, `/me/inventory/slots/move`, `/me/inventory/slots/split` (si está habilitado) |
| POST | `/me/inventory/shop/sell`, `/me/inventory/shop/buy`, `/me/inventory/shop/buyback` |
| POST / DELETE | `/me/inventory/reservation` |
| POST | `/me/inventory/reservation/admit`, `/me/inventory/extraction`, `/me/extraction/commit` |
| POST | `/debug/mock-fusion-result`, `/me/raid/admission-ticket`, `/raid/verify-admission`, `/raid/extraction-result` |
| POST | `/me` (crea las listas; permite crear sin personaje previo) |

Debug y extracción legacy permanecen `404 NOT_FOUND` en producción (o sin su flag), incluso
con el gate activo. Nunca se habilitan por enviar el header.

`GET /me` y `GET /me/profile` sólo devuelven identidad/perfil, no listas: no se gatean.
Ability purchase, progression commit y Town session ticket quedan exentos porque sus updates
son path-scoped y no reescriben listas (auditoría V3-V5 abajo). `PATCH /me/profile` tampoco
reescribe listas y utiliza el esquema ampliado al guardar.

### Escrituras durante B1

- v1 conserva las operaciones por `lootId`. Los aliases se comparan canónicamente sin reparar
  el resto del inventario. Move/sell/buy/buyback rechazan duplicados legacy **del item abordado**
  dentro de cada lista afectada con `409 INVENTORY_MIGRATION_REQUIRED`, incluyendo aliases que
  resuelvan al mismo ID. Order rechaza duplicados de toda su lista; Equipment y creación de
  reserva comprueban todas las listas que pueden usar (incluida la reserva al recrearla).
  No se fusionan duplicados para hacer posible una escritura. Otras entradas no se reorganizan.
- Entrada por slot (`slot`, `fromSlot`, `toSlot`, `sourceSlot`, `destinationSlot`, `slotOrder`)
  sobre una ruta existente con documento v1 devuelve `409 INVENTORY_MIGRATION_REQUIRED` incluso
  con gate apagado. El nuevo endpoint por slot forma parte de B2.
- B1 rechazaba todas las escrituras de listas v2. B2 reemplaza esos rechazos por el contrato de
  slots descrito abajo: sólo fuentes sin dirección y order por `lootIds` siguen rechazándose.
- v2 permite lectura, admisión de reserva y consumo como pérdida (sin rollback), que sólo cambian
  sus paths específicos. Los tickets agregan los items por `lootId` exclusivamente en el payload
  de admisión a Raid; nunca reemplazan los subdocumentos persistidos.
- Extracción v1 conserva el agregado de su payload de recompensa, sin normalizar Stash ni reserva
  durante el lookup. Resultados autoritativos y receipts no cambian de formato.

| HTTP | Código | Significado |
| --- | --- | --- |
| 426 | `CLIENT_UPGRADE_REQUIRED` | El gate exige header de protocolo `2` |
| 409 | `INVENTORY_MIGRATION_REQUIRED` | Documento legacy con gate activo, input por slot en v1 o item legacy ambiguo |
| 409 | `INVENTORY_SLOT_ADDRESS_REQUIRED` | Move/sell v2 sin `sourceSlot`, u order v2 sin `slotOrder` o con `lootIds` |

### Auditoría de writers (V3, V4, V5)

Todos los moves/copies de entradas almacenadas llevan el subdocumento completo, no una
reconstrucción `{lootId, amount}`. No se elimina `slot` ni `inventorySchemaVersion` en los
writers permitidos; borrar una reserva como pérdida es consumo deliberado, no pérdida de campos.

| Writer | Persistencia | Preservación / gate |
| --- | --- | --- |
| `AbilityService` | `findOneAndUpdate`, `$inc inventory.currency` + abilities/receipt/revision | No reescribe listas/version; exento |
| `ExtractionCommitService` | Update path-scoped de Loadout, Equipment, reserva, receipts y progresión | Agrega sólo payload Raid, aplica recepción v2 antes del claim; conserva Stash |
| `InventoryService` | Updates path-scoped con revisión; sólo extracción legacy v1 usa `save()` | Copia entradas completas; recepción, order, move y reserva/rollback v2 slot-aware; admisión/pérdida path-scoped |
| `RaidResultService` | Sólo lee Character; crea `AuthoritativeExtractionResult` | No escribe inventory/version; payload Raid agregado separado; gate caller/target |
| `SessionTicketService` | Town: `$set inventory.buyback`; Raid: sólo lectura | Listas/version intactas; Town exento; Raid gated y agrega sólo DTO |
| `CharacterService` | Creación y perfil con `save()` (Mongoose guarda paths modificados) | Schema slot-aware; creación selecciona versión; perfil sólo modifica `profile.*`, sin reconstruir inventory |
| `ProgressionService` | `$inc characterAttributes.*` y revision | No escribe inventory; exento |
| `CurrencyService` | Sólo lectura | No escribe inventory |
| `scripts/seed-test-accounts.js` | Upsert, `$setOnInsert` de nombre y versión | Sólo nuevos documentos; no reemplaza listas ni cambia versión existente |
| `scripts/migrate_legacy_data.js` | Driver raw, pipeline `$set` de atributos/revision | No escribe inventory ni version; no necesita cambios |
| `scripts/migrate_revisions.js` | `updateMany`, `$set revision` | No escribe inventory ni version; no necesita cambios |

Despliegue: completar B1 y drenar instancias viejas antes de migrar. No activar el gate ni
migrar para uso productivo hasta completar T2/T3 y actualizar clientes/hosts. Tras migrar,
no volver a backend anterior a B1 ni desactivar el gate; rollback sólo según el plan aprobado.

## Inventario: B2 (T3) — contrato v2

Estas operaciones se habilitan por `inventorySchemaVersion: 2`, incluso con gate apagado.
El gate de B1 no cambia; **no se activa ni se migra por desplegar B2**. Prefijo HTTP `/character`;
JWT obligatorio y header `X-Grimhold-Inventory-Protocol: 2` cuando el gate está activo.
El nuevo `POST /me/inventory/slots/move` usa el mismo gate. Split se añade por separado en T6 (abajo).

### Reglas de slots (C2, C5–C7)

- Loadout 30 / Stash 60 **entradas ocupadas**, no IDs distintos ni cantidades. Recibir incrementa
  el stack compatible de menor `slot` **dentro de capacidad**; si no existe, usa el menor hueco
  libre y requiere occupied count < capacidad. Si no cabe: **409 `INVENTORY_FULL`**.
  Un stack de overflow nunca recibe, aunque coincida su ID; sí puede venderse, equiparse o salir.
- Transferir/vender elimina sólo la entrada direccionada cuando llega a cero: quedan huecos.
  No se fusionan ni renumeran las demás entradas. `lootId` es una guarda del origen.
- Slot move mueve el stack entero: destino vacío → move; mismo ID → merge total; otro ID → swap.
  También funciona dentro de la misma lista. `toSlot` debe estar dentro de capacidad y un swap
  no puede colocar el desplazado en un `fromSlot` de overflow. A la misma dirección es no-op
  (devuelve snapshot sin incrementar revisión). Una inserción entre listas exige espacio ocupado;
  un merge/swap no aumenta el número de entradas destino.
- Order exige una permutación exacta de los slots ocupados. `slotOrder[i]` identifica la entrada
  que pasa a `slot: i`, sin fusionar IDs repetidos. Es la única compactación explícita. Rechaza
  listas con más entradas que capacidad con `INVENTORY_FULL`, para no colocar en overflow.
- Cada mutación usa una escritura atómica guardada por `revision`; rechazos no cambian moneda,
  listas, receipts ni revisión. V1 conserva el contrato agregado y las comprobaciones B1.

### Requests Unity (JSON exacto de ejemplo)

| Operación | Body |
| --- | --- |
| POST `/me/inventory/stash/move-to-loadout` | `{"lootId":"bone","amount":2,"sourceSlot":9,"expectedRevision":4}` |
| POST `/me/inventory/loadout/move-to-stash` | `{"lootId":"bone","amount":2,"sourceSlot":3,"expectedRevision":5}` |
| PUT `/me/inventory/order` | `{"list":"stash","slotOrder":[9,3],"expectedRevision":6}` |
| POST `/me/inventory/slots/move` | `{"fromList":"stash","fromSlot":9,"toList":"loadout","toSlot":2,"lootId":"bone","expectedRevision":7}` |
| POST `/me/inventory/shop/sell` | `{"transactionId":"sale-1","lootId":"placeholder_helmet","amount":1,"sourceSlot":8,"expectedRevision":8}` |
| POST `/me/inventory/shop/buy` | `{"transactionId":"buy-1","lootId":"healthpotion","amount":1,"expectedRevision":9}` |
| POST `/me/inventory/shop/buyback` | `{"transactionId":"rebuy-1","entryId":"sale-line-uuid","amount":1,"expectedRevision":10}` |
| PUT `/me/inventory/prepared-equipment` | `{"weaponSlot1":"arming_sword","weaponSlot2":"","offHand1":"","offHand2":"","helmet":"","armor":"","gloves":"","boots":"","expectedRevision":11}` |
| POST `/me/inventory/reservation` | `{"reservationId":"reservation-1","preparedAbilities":{"slot1":"","slot2":""}}` |
| DELETE `/me/inventory/reservation?rollback=true` | Sin body |
| POST `/me/extraction/commit` | `{"raidId":"raid-1","resultSequence":1}` |

`list`/`fromList`/`toList`: `stash` o `loadout`. Slots son números enteros no negativos
(no strings, null ni fracciones). `sourceSlot` es obligatorio sólo para moves/sell v2;
v1 con ese campo o `slotOrder` devuelve `409 INVENTORY_MIGRATION_REQUIRED`.
V2 sin fuente o con order `lootIds` devuelve `409 INVENTORY_SLOT_ADDRESS_REQUIRED`.
`declaredPrice`/`declaredSellValue` siguen opcionales y no afectan el precio autoritativo.
El receipt sell v2 vincula también `sourceSlot`: reusar el transactionId con otro slot devuelve
`TRANSACTION_ID_REUSED`, incluso si coincide el ID y la cantidad. Buyback conserva líneas sin slot.

### Responses

Order, ambos transfers y slot move devuelven HTTP 200:

```json
{"stash":[{"slot":3,"lootId":"bone","amount":4}],"loadout":[{"slot":2,"lootId":"bone","amount":2}],"revision":8}
```

Shop devuelve HTTP 200 (replay añade `"alreadySecured":true`):

```json
{"revision":9,"currency":505,"stash":[],"loadout":[],"preparedEquipment":{"weaponSlot1":"arming_sword","weaponSlot2":"","offHand1":"","helmet":"","armor":"","gloves":"","boots":"","offHand2":""},"buyback":{"entries":[{"entryId":"sale-line-uuid","lootId":"placeholder_helmet","amount":1,"unitPrice":8}]}}
```

Equipment devuelve `{preparedEquipment,stash,loadout,revision}` (mismas formas).
GET inventory mantiene el snapshot B1 con `inventorySchemaVersion:2`, listas ordenadas por slot,
`currency`, `preparedEquipment`, `pendingReservation` (o null), `buyback`,
`lastAppliedExtractionReceipt` (o null) y `revision`. **No usar índice del array como slot**.
Reserva (201), admisión (200) y replay devuelven `{pendingReservation,revision}`, donde:

```json
{"pendingReservation":{"reservationId":"reservation-1","admittedAt":null,"items":[{"slot":9,"lootId":"bone","amount":2}],"preparedEquipment":{"weaponSlot1":"arming_sword","weaponSlot2":"","offHand1":"","helmet":"","armor":"","gloves":"","boots":"","offHand2":""}},"revision":12}
```

DELETE devuelve `{"pendingReservation":null,"revision":13}`; reconciliar GET para listas restauradas.
Unified extraction devuelve HTTP 201 (replay 200): `{alreadySecured,loadout,level,experience,
characterAttributes,revision}`; el payload autoritativo sigue agregado, y `loadout` devuelve slots v2.

### Reserva, Equipment y extracción (V1, V2, V9)

- **V1 audit:** order y nuevas operaciones shop (buy/sell/buyback) bloquean con
  `409 RESERVATION_PENDING`; replay shop ya aplicado sigue permitido. Los transfers legacy y
  Equipment no bloqueaban por reserva: se conserva esa conducta para v1 y v2. El nuevo slot move
  sí bloquea, igual que order. No confundir V1 (auditoría) con schema v1.
- **V2 audit:** sell lee/escribe sólo Loadout, moneda, receipt y buyback elegible; nunca Stash.
  Buyback escribe Loadout, moneda, línea/receipt; nunca Stash. La línea es
  `{entryId,lootId,amount,unitPrice}` (sin slot), con precio congelado ceil(sell × 1.5), FIFO 16 y
  epoch Town existentes. Recompra parcial reduce la línea; total la elimina. Ambas recepciones
  aplican capacidad antes de débito/receipt.
- Equipment v2 reasigna primero unidades ya equipadas entre assignments sin recepción intermedia;
  para unidades adicionales consume el stack de menor slot Loadout, luego Stash, antes de devolver desplazados
  a Loadout con la regla de recepción; capacidad se evalúa sobre el intercambio final. No compacta.
- Reserva guarda entradas completas con slots exactos. El arma recovery consume el stack de menor
  slot si existe antes de garantizar una. Más de 30 entradas Loadout rechaza `INVENTORY_FULL`.
  Rollback pre-admisión conserva posiciones originales libres primero; colisiones usan menor hueco
  libre y luego menor overflow libre. Nunca fusiona ni pierde unidades, aunque haya Loadout nuevo.
  Equipment reservado no sobrescribe asignaciones nuevas: se restaura en slots vacíos y los
  desplazados se conservan en Loadout mediante la misma restauración sin pérdidas.
- Tickets de admisión siguen agregando por `lootId` sólo en `snapshot.items` (sin slots ni IDs
  repetidos), compatible con el contrato RaidAdmission; no reescriben la reserva persistida.
- Extracción empieza con Loadout vacío (`LOADOUT_NOT_EMPTY` en otro caso), agrega el payload por
  lootId y recibe en slots 0..n−1; más de 30 IDs distintos rechaza defensivamente antes del claim
  del resultado, nunca trunca. Stash no cambia. La ruta legacy no productiva aplica igual colocación.

### Errores B2

| HTTP | Código | Condición |
| --- | --- | --- |
| 409 | `INVENTORY_FULL` | Sin capacidad o intento de placement/retorno swap en overflow |
| 409 | `INVENTORY_SLOT_ADDRESS_REQUIRED` | Fuente/order v2 no direccionados |
| 409 | `INVENTORY_MIGRATION_REQUIRED` | Operación por slot sobre v1 (o gate B1) |
| 409 | `RESERVATION_PENDING` | Order, slot move o nueva transacción shop durante reserva |
| 409 | `REVISION_CONFLICT` | Revisión stale o carrera del write atómico |
| 409 | `TRANSACTION_ID_REUSED` | Replay con intención distinta, incluido sourceSlot sell |
| 409 | `INSUFFICIENT_STASH_ITEMS` / `INSUFFICIENT_LOADOUT_ITEMS` / `INSUFFICIENT_ITEMS` | Guarda/cantidad de transfer o venta no coincide |
| 422 | `ITEM_NOT_FOUND` | Guarda del source slot move inválida o equipo no disponible |
| 400 | `VALIDATION_FAILED` | DTO inválido o slotOrder no es permutación exacta |
| 400 | `INTEGER_OVERFLOW` | Merge v2 excede cantidad entera segura |

## Inventario: T6 — split autoritativo (TASK-281, C8)

`POST /character/me/inventory/slots/split` divide un stack en la misma lista; no mueve entre
listas, no fusiona ni intercambia entradas. Requiere JWT y `INVENTORY_SPLIT_ENABLED=true`.
Por defecto devuelve **403 `FEATURE_DISABLED` antes de leer el inventario**, incluso con el gate
activo. Si está habilitado, usa el middleware de protocolo de B1: gate activo sin header
`X-Grimhold-Inventory-Protocol: 2` → 426; documento v1 (con o sin gate) → 409, sin migración.
La flag no activa el gate ni migra documentos. TASK-282 (UI) no forma parte de este endpoint.

### Request y respuesta

```json
{"list":"stash","sourceSlot":9,"amount":8,"destinationSlot":1,"lootId":"bone","expectedRevision":4}
```

- `list`: `stash` o `loadout`; `sourceSlot`, `destinationSlot` y `expectedRevision`: números
  enteros seguros no negativos, no strings. `lootId` no vacío es una guarda de identidad del
  slot fuente, no un selector de otro stack con el mismo ID.
- `amount`: número entero seguro en `[1, sourceAmount - 1]`; deja al menos una unidad en origen.
  No hay `StackMax` ni `Origin` en este contrato Town. Se copia el subdocumento fuente completo
  al destino, cambiando sólo `slot` y `amount` (campos futuros declarados viajan con él).
- Destino distinto del origen, vacío y menor que capacidad (Loadout 30 / Stash 60).
  El número de entradas ocupadas, incluidas las de overflow, debe ser menor que capacidad.
  Una fuente de overflow puede dividirse hacia un hueco dentro de capacidad sólo bajo esa regla;
  nunca crece el stack de overflow ni se coloca el nuevo stack en overflow.
- Rechaza durante una reserva pendiente. Una única escritura atómica guardada por revisión y
  ausencia de reserva modifica sólo la lista elegida e incrementa `revision` una vez. Rechazos
  no cambian cantidades, otras listas/campos ni revisión; no hay retry automático.

HTTP 200, misma forma que slot move, listas ordenadas por slot. Ejemplo con 13 unidades en slot 9:

```json
{"stash":[{"slot":1,"lootId":"bone","amount":8},{"slot":9,"lootId":"bone","amount":5}],"loadout":[],"revision":5}
```

### Errores split

| HTTP | Código | Condición |
| --- | --- | --- |
| 403 | `FEATURE_DISABLED` | Split apagado; precede al gate y a lecturas de inventario |
| 426 | `CLIENT_UPGRADE_REQUIRED` | Split habilitado y gate activo sin header de protocolo 2 |
| 409 | `INVENTORY_MIGRATION_REQUIRED` | Documento legacy/ausente versión 2 |
| 404 | `CHARACTER_NOT_FOUND` | No existe personaje para la cuenta autenticada |
| 400 | `VALIDATION_FAILED` | Lista/slots/lootId/revisión inválidos, o destino igual al origen |
| 400 | `INVALID_QUANTITY` | Cantidad ausente, no entera segura, menor que 1 o no deja unidades en origen |
| 409 | `INSUFFICIENT_STASH_ITEMS` / `INSUFFICIENT_LOADOUT_ITEMS` | Fuente ausente o guarda `lootId` no coincide |
| 409 | `SLOT_OCCUPIED` | Destino ocupado, incluso por el mismo `lootId` |
| 409 | `INVENTORY_FULL` | Destino fuera de capacidad o occupied count >= capacidad |
| 409 | `RESERVATION_PENDING` | Hay una reserva pendiente al validar |
| 409 | `REVISION_CONFLICT` | Revisión stale o carrera del write atómico (incluida una reserva concurrente) |

## Atlas Network Access

Si se usa MongoDB Atlas, agregar la IP del equipo en **Network Access** antes de iniciar el servidor.

## Deployment en Railway

1. Crear el proyecto en Railway enlazado a este repositorio.
2. Railway detectará Node.js automáticamente. Configurar el Root Directory a `backend` si el repositorio contiene otras carpetas.
3. Configurar en Variables de Entorno:
   - `NODE_ENV=production`
   - `MONGODB_URI` (El de Atlas de testing/prod)
   - `JWT_SECRET` (Fuerte, único y de al menos 32 caracteres)
   - `WEBHOOK_SECRET` (Fuerte, único y de al menos 32 caracteres)
   - `JWT_EXPIRES_IN` (Opcional, por ej. `3600`)
   - **NO configurar `PORT`**, Railway lo inyecta automáticamente.
4. En **MongoDB Atlas Network Access**, agregar `0.0.0.0/0` (Allow from anywhere) para que Railway pueda conectarse a la DB.
5. Ir a Settings -> Deploy en Railway y configurar el Healthcheck:
   - Healthcheck Path: `/health`
   - Healthcheck Timeout: `30`
6. Ir a Settings -> Networking -> Public Networking y hacer clic en **Generate Domain**.
7. Copiar el dominio `https://<tu-app>.up.railway.app` y usarlo en Unity (`BackendConfiguration_Remote.asset`).

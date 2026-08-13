# Estrategia de Persistencia para el Vertical Slice

## 1. Estado actual

Actualmente, el ciclo de juego (Town-Raid-Town) maneja la información de la siguiente manera:

* **Stash y Loadout**: Se almacenan en un `LocalProfileStore` que utiliza un `InMemoryLocalProfileRepository`. Este agregado sobrevive a los cambios de escena y al reemplazo del `NetworkRunner` porque su contexto (`ApplicationStashContext`) está marcado como `DontDestroyOnLoad`.
* **Perfil / Progresión**: No existe una progresión duradera. El `ProfileId` es generado localmente (`LocalProfileProvider`) y es único por cada ejecución del proceso de la aplicación.
* **Configuración o datos persistentes existentes**: Cualquier archivo local como `grimhold-profile.json` o datos en `PlayerPrefs` actualmente se ignoran de manera intencional en favor de la memoria temporal.
* **Identidad del jugador**: Está ligada al proceso actual de la aplicación. Photon Fusion utiliza `PlayerRef` como identidad de sesión, pero esto no representa una identidad de persistencia de gameplay.
* **Estado de una expedición**: Es completamente temporal y autoritativo bajo Photon Fusion. La salud, inventario recolectado, y progreso de extracción viven en la simulación.
* **Dependencia de Photon Fusion**: Fusion maneja la autoridad de la partida (State Authority), la detección de zonas de extracción (`ExtractionZone`), y el proceso de extracción en sí (`PlayerExtractionController`), pero *no* es dueño del stash o loadout local.
* **Supervivencia al destruir `NetworkRunner`**: El inventario asegurado (Stash, Loadout, y Recibos de Extracción) sobrevive porque vive en la capa de aplicación de Unity, fuera del ciclo de vida del Runner de Fusion.
* **Limitaciones actuales**: Al cerrar o crashear la aplicación, se pierde todo el stash, loadout y loot asegurado. Reabrir la aplicación genera un nuevo `ProfileId`. No hay forma de validar contra trampas si el cliente modifica la memoria, ya que no hay validación externa.

## 2. Problema a resolver

El sistema en memoria fue diseñado exclusivamente para probar el flujo de las mecánicas sin depender de infraestructura externa. Sin embargo, para un Vertical Slice jugable, es imperativo que el esfuerzo del jugador tenga significado y perdure. El sistema actual no permite conservar el progreso (Stash, Loadout) entre distintas sesiones de juego (cierres de aplicación), y la falta de una identidad estable impide construir cualquier sistema de economía, progresión o matchmaking persistente. Se requiere introducir un backend externo que actúe como fuente de verdad duradera.

## 3. Propuesta para el Vertical Slice

Se propone introducir un backend ligero que reemplace el almacenamiento en memoria temporal, actuando como la fuente de verdad del perfil del jugador.

* **Alcance de los datos persistentes**: Se persistirá exclusivamente la identidad del jugador, el contenido del Stash y el Loadout equipado.
* **Estado temporal (no persistente)**: La posición del jugador, estado de salud, inventario en partida (antes de extraer), estado de enemigos y tiempos de extracción seguirán siendo puramente estado de la sesión de Fusion.
* **Identidad persistente**: Se implementará un mecanismo de autenticación básico (por ejemplo, Device ID o un login simple) para proveer un identificador de perfil estable a Unity en lugar de generarlo localmente.
* **Fuente de verdad**: El Backend será la única fuente de verdad para el Stash y Loadout. Photon Fusion seguirá siendo la fuente de verdad de la simulación durante el Raid.
* **Responsabilidad de Unity**: Autenticarse contra el backend, mantener el estado sincronizado en el cliente para presentación (UI), enviar intenciones de modificación de Loadout, y reportar la extracción completada.
* **Responsabilidad de Photon Fusion**: Simular la expedición, validar colisiones, mecánicas de daño, e indicar algorítmicamente cuándo un jugador ha extraído exitosamente (generando el `ExtractionReceipt`).
* **Responsabilidad del Backend**: Validar la identidad, proveer el perfil inicial al conectarse, validar transferencias entre Stash/Loadout, y validar/procesar los recibos de extracción para añadir loot al perfil.

### Flujos generales

* **Flujo de carga (Town)**: Unity autentica con Backend -> Backend devuelve Profile (Stash/Loadout) -> Unity inicializa su `LocalProfileStore` con estos datos.
* **Flujo de guardado (Town)**: Unity envía transferencias (ej. mover de Stash a Loadout) vía HTTPS -> Backend valida disponibilidad y actualiza la base de datos -> Devuelve OK -> Unity actualiza UI.
* **Flujo de extracción**: Fusion confirma extracción -> Genera `ExtractionReceipt` (State Authority) -> Unity envía el Receipt + Loot al Backend -> Backend verifica que el receipt no haya sido usado, añade el loot al Stash, y lo marca como consumido -> Unity limpia su inventario de raid temporal.

### Evaluación del Tech Stack

La propuesta tecnológica (`Node.js + Express + Mongoose + MongoDB Atlas` comunicado vía `HTTPS`) es altamente adecuada para el Vertical Slice. 
MongoDB, siendo una base de datos orientada a documentos, se alinea perfectamente con la estructura jerárquica de perfiles, stash e inventarios (que generalmente se serializan como objetos JSON). Express permite levantar rutas rápidamente, y es lo suficientemente flexible para evolucionar. Minimiza el overhead de infraestructura sin comprometer la capacidad de expandirse en el futuro.

## 4. Alcance y fuera de alcance

**En alcance (Vertical Slice):**
* Autenticación básica (Login simple o UUID por dispositivo).
* Fetching y guardado del estado del Stash y Loadout en la base de datos.
* Endpoint para procesar el final de una expedición (Extracción) y consolidar el loot.
* Sincronización básica de errores (rechazo de operaciones inválidas por parte del servidor).

**Fuera de alcance:**
* Validación server-side estricta de cada paso del raid (Anti-cheat avanzado). Por ahora, el backend confiará en los recibos generados por el cliente (Host/Client mode).
* Sistemas de progresión complejos, XP o árboles de habilidades.
* Economía entre jugadores, trading o subastas.
* Sistema de clanes, amigos o matchmaking basado en rangos (MMR).
* Host Migrations complejas con recuperación de loot a nivel backend (se apoyará en el sistema actual de Fusion).

## 5. Dependencias

La introducción de este sistema impactará o dependerá de los siguientes módulos actuales:

* **Stash y Loadout**: Deberán refactorizarse para comunicarse asíncronamente con el backend en lugar de solo operar sobre el snapshot en memoria (`LocalProfileStore`).
* **Extraction**: El `PlayerExtractionController` y la emisión de `ExtractionReceipt` deberán engancharse con la llamada a la API del backend.
* **Autenticación / Identidad**: Sistema completamente nuevo que deberá inyectarse antes de cargar la escena principal (Town).
* **Photon Fusion**: El inicio de la sesión de Fusion deberá utilizar el ID y el loadout validados por el backend, en lugar de datos generados al vuelo.

## 6. Roadmap

* Etapa 1: Definición de Contratos (API) y Esquemas de Base de Datos.
* Etapa 2: Implementación de Autenticación e Identidad Persistente.
* Etapa 3: Integración de Persistencia de Stash y Loadout en Town.
* Etapa 4: Flujo de Consolidación de Loot post-Extracción (Raid to Town).
* Etapa 5: Resiliencia, Manejo de Errores y Desconexiones.

## 7. Decisiones y puntos pendientes

### Decisiones arquitectónicas principales
* El backend utilizará un modelo de persistencia asíncrona vía HTTP/REST (o RPC sobre HTTP). No se mantendrá un socket persistente con el backend para la gestión de inventario en esta fase.
* El perfil local de Unity actuará como una caché predictiva o reaccionará al estado confirmado por el servidor; la única fuente de verdad autoritativa para el metajuego es MongoDB.

### Supuestos
* Se asume que para el Vertical Slice, la confianza en el reporte de extracción generado por el cliente (Host/State Authority en topología cliente-servidor de Fusion) es aceptable, postergando soluciones anti-cheat complejas.

### Puntos pendientes
* **Mecanismo exacto de Autenticación**: Definir si se usará vinculación anónima por Device ID de Unity, o si se requerirá un prompt de usuario/contraseña simple para facilitar pruebas en builds standalone en una misma PC.
* **Gestión de fallos de red durante la extracción**: Determinar la política de reintentos si la petición HTTPS para asegurar el loot falla tras una desconexión exitosa del servidor de Fusion.
* **Migración del `LocalProfileStore`**: Definir si se mantiene el `ILocalProfileRepository` inyectando una implementación remota, o si se refactoriza el servicio completo para acomodar la latencia de red inherente.

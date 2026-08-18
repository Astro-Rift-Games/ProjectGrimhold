# Game Feel — Stage 1: Screen Shake

## Resumen de cambios

Se implementó un sistema de screen shake basado en **trauma** (modelo matemático: `offset = shake^exponent * noise`). El shake es local, no replicado, y opera completamente en la capa de presentación.

### Archivos nuevos

| Archivo | Descripción |
|---------|-------------|
| `CameraShakeConfig.cs` | ScriptableObject con todos los parámetros configurables desde el Inspector |
| `CameraShakeController.cs` | MonoBehaviour en el GameObject de la cámara. Acumula trauma y genera el offset por Perlin noise |
| `LocalPlayerCameraShakeBinder.cs` | NetworkBehaviour en el prefab del player. Observa cambios de `Health` y el evento `CombatFeedbackResolved` para disparar el shake |

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `LocalCameraController.cs` | Cachea `CameraShakeController` en `Awake()` y aplica `ShakeOffset` al final de `LateUpdate()` |
| `LocalPlayerHudBinder.cs` | Agrega referencias serializadas a `LocalPlayerCameraShakeBinder` y `CameraShakeConfig`; llama `Bind()`/`Unbind()` en el ciclo de vida del HUD |

---

## Comportamiento

| Trigger | Condición | Shake |
|---------|-----------|-------|
| Daño recibido | El player local pierde HP (cualquier fuente) | `ReceiveDamageIntensity` + `ReceiveDamageDuration` |
| Daño realizado | El player local golpea a un `CharacterBase` con daño > 0 | `DealDamageIntensity` + `DealDamageDuration` |
| Daño a breakable | El player daña un objeto rompible | **Sin shake** (no son `CharacterBase`) |

---

## Configuración en el Editor de Unity

### 1. Crear el ScriptableObject de configuración

1. En el **Project window**, clic derecho → **Create → Grimhold → Presentation → CameraShakeConfig**.
2. Nómbralo `DefaultCameraShakeConfig` (u otro nombre descriptivo).
3. Ajustar los parámetros según la intensidad deseada. Valores de referencia:

| Campo | Default | Descripción |
|-------|---------|-------------|
| Receive Damage Intensity | 0.4 | Trauma añadido al recibir daño (0–1) |
| Receive Damage Duration | 0.25 s | Duración de la caída del trauma |
| Deal Damage Intensity | 0.15 | Trauma al golpear a un character |
| Deal Damage Duration | 0.12 s | Duración de la caída al golpear |
| Decay Exponent | 2 | `shake = trauma^exponent`. Valores altos → caída más brusca |
| Max Offset | 0.18 | Desplazamiento máximo en unidades de mundo al trauma = 1 |

### 2. Añadir CameraShakeController a la cámara

1. Seleccionar el **GameObject de la cámara** en la escena (el que tiene `LocalCameraController`).
2. **Add Component → CameraShakeController**.

> **IMPORTANTE**: `CameraShakeController` debe estar en el **mismo GameObject** que `LocalCameraController`. El `LocalCameraController` lo busca con `GetComponent` en `Awake`.

### 3. Añadir LocalPlayerCameraShakeBinder al prefab del player

1. Abrir el prefab del **player** (el que contiene `LocalPlayerHudBinder`).
2. **Add Component → LocalPlayerCameraShakeBinder** en el root del prefab.
3. Asignar las referencias en el Inspector:
   - **Player Character**: referencia al `PlayerCharacter` del prefab.
   - **Combat Controller**: referencia al `PlayerCombatNetworkController` del prefab.

### 4. Configurar LocalPlayerHudBinder

En el Inspector del `LocalPlayerHudBinder` del prefab del player, asignar:
- **Camera Shake Binder**: el `LocalPlayerCameraShakeBinder` recién añadido.
- **Camera Shake Config**: el asset `DefaultCameraShakeConfig` creado en el paso 1.

> **NOTA**: Ambas referencias son opcionales: si quedan vacías, el HUD funciona normalmente sin shake.

---

## Validación manual

1. **Solo**: player recibe daño de enemy → pantalla tiembla brevemente.
2. **Solo**: player golpea a enemy → pantalla tiembla con menor intensidad.
3. **Solo**: player rompe un objeto breakable → **sin shake**.
4. **Multijugador**: el shake solo ocurre en la pantalla del jugador afectado.
5. **Ajuste de parámetros**: modificar el asset de config para calibrar la intensidad sin recompilar.

---

## Commit

### Title
```
feat(presentation): add trauma-based camera shake for combat events
```

### Description
```
Implements a local-only screen-shake system driven by two combat events:

- Damage received (any source): detected via Health change observation in
  LocalPlayerCameraShakeBinder.Render(), compatible with both host and
  client because [Networked] Health is replicated before Render executes.

- Damage dealt to characters: driven by CombatFeedbackResolved event on
  PlayerCombatNetworkController. Breakable objects are excluded by checking
  whether the target EntityId maps to a CharacterBase in the EntityRegistry.

New components:
- CameraShakeConfig (ScriptableObject): inspector-configurable intensities,
  durations, decay exponent and max offset.
- CameraShakeController (MonoBehaviour): trauma model with Perlin-noise
  spatial offset, per-session random seed, additive multi-request support.
- LocalPlayerCameraShakeBinder (NetworkBehaviour): player-side event bridge.

Modified components:
- LocalCameraController: reads ShakeOffset from colocated CameraShakeController.
- LocalPlayerHudBinder: binds/unbinds the shake binder with the HUD lifecycle.

No networked state added. No simulation code modified.

Inspector configuration required — see Docs/GameFeel_Stage1_ScreenShake.md.
```

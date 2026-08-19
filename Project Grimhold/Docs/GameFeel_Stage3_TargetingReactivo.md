# Game Feel — Stage 3: Targeting Reactivo (Aggro por Daño)

## Resumen de cambios

Se implementó un sistema de aggro que hace que un enemigo entre inmediatamente en persecución cuando recibe daño de un player, sin requerir Line of Sight. La activación dura un tiempo configurable; si expira sin LOS, el enemigo vuelve a su estado anterior.

### Archivos nuevos

| Archivo | Descripción |
|---------|-------------|
| `IAggroReceiver.cs` | Interfaz implementada por `EnemyMovementAIController`. Contrato para recibir alertas de aggro |

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `EnemyMovementAIController.cs` | Implementa `IAggroReceiver`. Agrega `_aggroAlertDuration` config, `[Networked] TickTimer AggroAlertTimer`, `ReceiveAggroAlert()`, y bypass de LOS en `EvaluateActiveTarget()` |
| `EntityRegistry.cs` | Agrega `TryGetTransform(EntityId, out Transform)` y `IsPlayerEntity(EntityId)` |
| `DamageResolver.cs` | Paso 6: Llama `ReceiveAggroAlert()` si el target implementa `IAggroReceiver` y el attacker es un `PlayerCharacter` |

---

## Comportamiento

| Escenario | Resultado |
|-----------|-----------|
| Player golpea a enemy (con o sin LOS) | Enemy adquiere player como target y lo persigue inmediatamente |
| Aggro timer activo, enemy no establece LOS | Enemy continúa persiguiendo durante `_aggroAlertDuration` segundos |
| Aggro timer activo, enemy establece LOS natural | Timer cancelado; pursuit continúa vía flujo normal (LOS-based) |
| Aggro timer expira sin LOS | Enemy vuelve a Idle/Patrol según su estado previo |
| Trampa/Breakable daña a enemy | **Sin aggro** (no son `PlayerCharacter` en el registry) |
| Enemy daña a player | **Sin aggro** (no dispara `ReceiveAggroAlert`) |

---

## Flujo técnico

```
DamageResolver.Resolve()
  └── target is IAggroReceiver?
      └── registry.IsPlayerEntity(attacker)?
          └── registry.TryGetTransform(attacker)?
              └── aggroReceiver.ReceiveAggroAlert(attackerId, attackerTransform)
                  ├── _currentTarget ← attacker
                  ├── _pursuitLostTickCount ← 0
                  └── AggroAlertTimer ← TickTimer.CreateFromSeconds(Runner, _aggroAlertDuration)

EnemyMovementAIController.EvaluateActiveTarget() [siguiente tick]
  ├── aggroActive = !AggroAlertTimer.ExpiredOrNotRunning(Runner)
  ├── hasLOS check
  │   ├── hasLOS = true  → cancelar AggroAlertTimer, pursut normal
  │   ├── hasLOS = false, aggroActive = true  → IsOnPursuit = true (bypass)
  │   └── hasLOS = false, aggroActive = false → grace period normal → eventual disengage
```

---

## Configuración en el Editor de Unity

### Ajustar Aggro Alert Duration por tipo de enemigo

En cada prefab de enemigo, seleccionar el componente **EnemyMovementAIController** e inspeccionar el header **Aggro**.

| Campo | Descripción |
|-------|-------------|
| **Aggro Alert Duration** | Tiempo en segundos que el enemigo persigue sin LOS tras recibir daño de un player. Default: `5f`. Valor `0` desactiva el sistema para ese enemigo |

**Valores de referencia:**
- Enemigo básico (melee): `4–6 s`
- Enemigo rápido (ranged): `6–8 s`
- Enemigo lento/tanque: `8–12 s`

> **IMPORTANTE**: No se requiere ninguna otra asignación en el Inspector para esta feature. El sistema se activa automáticamente en tiempo de ejecución a través de `DamageResolver`.

---

## Validación manual

1. **Persecución sin LOS**: colocar un muro entre player y enemy → player golpea a enemy → enemy cruza el muro para perseguir.
2. **Timer de aggro**: tras golpear al enemy, alejarse y esperar más de `_aggroAlertDuration` segundos sin LOS → enemy vuelve a Idle/Patrol.
3. **Cancelación por LOS**: durante aggro activo, el player entra en línea de visión del enemy → timer se cancela → enemy continúa en pursuit normal.
4. **Exclusión de trampas**: trampa daña a enemy → enemy **no** entra en aggro (no hay react visible).
5. **Multijugador**: el aggro timer es `[Networked]` → host y client ven el mismo comportamiento de persecución.

---

## Commit

### Title
```
feat(ai): reactive targeting — enemies aggroed by player damage ignore LOS
```

### Description
```
Implements damage-triggered enemy alerting. When a player deals damage to an
enemy, the enemy immediately acquires the player as its target and pursues
without LOS for a configurable duration (AggroAlertDuration, Inspector).

Architecture:
- IAggroReceiver: contract on EnemyMovementAIController.
- EntityRegistry.IsPlayerEntity() + TryGetTransform(): allow DamageResolver
  to identify player attackers and supply their Transform without coupling
  to PlayerCharacter or MonoBehaviour directly in the resolver.
- DamageResolver.Resolve() step 6: calls ReceiveAggroAlert() after a
  successful hit when attacker is a PlayerCharacter. Traps and breakables
  are excluded because they are not registered as PlayerCharacter.

EnemyMovementAIController changes:
- New [SerializeField] _aggroAlertDuration (default 5s).
- New [Networked] AggroAlertTimer (TickTimer).
- ReceiveAggroAlert(): forces target acquisition + starts timer.
- EvaluateActiveTarget(): during active aggro, skips LOS check.
  Natural LOS reestablishment cancels the timer early.

No FSM state changes required. The existing IsOnPursuit flag drives the
EnemyChaseState/EnemyCombatState transitions unchanged.

Inspector configuration: set Aggro Alert Duration on enemy prefabs.
See Docs/GameFeel_Stage3_TargetingReactivo.md.
```

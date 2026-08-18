# Game Feel — Stage 2: Knockback

## Resumen de cambios

Se implementó un sistema de knockback physics-respetante para todos los ataques. El target recibe un desplazamiento en dirección opuesta al impacto, resuelto contra la geometría del mundo por el motor de movimiento existente.

### Archivos nuevos

| Archivo | Descripción |
|---------|-------------|
| `IKnockbackMotor.cs` | Interfaz implementada por los controladores de movimiento. Recibe el impulso de knockback |
| `IKnockbackReceiver.cs` | Interfaz implementada por `CharacterBase`. Puente entre el resolver y el motor de movimiento |

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `DamageRequest.cs` | Agrega `KnockbackForce` (float, default `0f`). Backward compatible |
| `ProjectileSpawnRequest.cs` | Agrega `KnockbackForce` (float, default `0f`). Backward compatible |
| `AttackConfig.cs` | Agrega `[SerializeField] _knockbackForce` con tooltip, min 0. Expone `KnockbackForce` property |
| `MeleeAttack.cs` | Pasa `config.KnockbackForce` al `DamageRequest` |
| `RangedAttack.cs` | Pasa `config.KnockbackForce` al `ProjectileSpawnRequest` |
| `NetworkProjectile.cs` | Agrega `[Networked] float KnockbackForce`. Inicializa desde spawn request y pasa al `DamageRequest` en impacto |
| `PlayerMovementNetworkController.cs` | Implementa `IKnockbackMotor`. Agrega `[Networked] Vector2 PendingKnockback` consumido en `FixedUpdateNetwork` |
| `EnemyMovementAIController.cs` | Implementa `IKnockbackMotor`. Mismo patrón con `PendingKnockback` |
| `CharacterBase.cs` | Implementa `IKnockbackReceiver`. Cachea `IKnockbackMotor` en `Awake`, delega en `ReceiveKnockback` |
| `DamageResolver.cs` | Paso 5: llama `ReceiveKnockback` si el target implementa `IKnockbackReceiver` y `KnockbackForce > 0` |

---

## Comportamiento

- Cualquier ataque melee o proyectil con `KnockbackForce > 0` en su `AttackConfig` empuja al target.
- La dirección del empuje es `-request.Direction` (opuesta al vector de ataque).
- El desplazamiento se calcula como `force * DeltaTime` y se pasa al motor existente, respetando colisiones de mundo.
- Múltiples impactos en el mismo tick son aditivos.
- Enemigos y players reciben knockback por igual.

---

## Configuración en el Editor de Unity

### Ajustar KnockbackForce en AttackConfigs existentes

Para cada `AttackConfig` (melee o ranged) en `Assets/ScriptableObjects/`:

1. Seleccionar el asset de configuración del ataque.
2. En el Inspector, buscar el campo **Knockback Force** (en el mismo bloque que `Damage` y `Cooldown`).
3. Ajustar el valor. Referencia inicial:
   - Melee ligero: `4–6` unidades/s
   - Melee pesado: `10–15` unidades/s
   - Proyectil: `3–8` unidades/s (varía por velocidad del proyectil)

> **NOTA**: El campo aparecerá después de recompilar el proyecto. Unity puede pedir reserializar los assets si el nuevo campo no tiene un valor previo; el default de `5f` se aplicará automáticamente.

---

## Validación manual

1. Player melee golpea enemigo → enemigo se desplaza en dirección opuesta al swing.
2. Proyectil impacta player → player se desplaza en dirección opuesta al proyectil.
3. `KnockbackForce = 0` en config → ningún desplazamiento (backward compatible).
4. El knockback respeta colisiones: el target no atraviesa paredes.
5. Host y client: la posición replicada refleja el knockback correctamente (se aplica bajo State Authority, NetworkTransform lo replica).

---

## Commit

### Title
```
feat(combat): implement universal knockback for all character attacks
```

### Description
```
Adds physics-respecting knockback to all melee and ranged attacks.

Architecture:
- IKnockbackMotor: received by PlayerMovementNetworkController and
  EnemyMovementAIController. Accumulates PendingKnockback ([Networked])
  consumed each FixedUpdateNetwork before motor.Move().
- IKnockbackReceiver: implemented by CharacterBase. Bridges DamageResolver
  to the movement motor without the resolver knowing motor internals.
- DamageResolver.Resolve() step 5: calls ReceiveKnockback after successful
  damage if KnockbackForce > 0. Breakables are excluded (not IKnockbackReceiver).

Data propagation:
  AttackConfig.KnockbackForce
  → MeleeAttack → DamageRequest.KnockbackForce
  → RangedAttack → ProjectileSpawnRequest → NetworkProjectile.[Networked] → DamageRequest

Both DamageRequest and ProjectileSpawnRequest use optional parameters with
default 0f for full backward compatibility with existing call-sites.

Displacement direction: -request.Direction (opposite of impact).
Magnitude: force * Runner.DeltaTime per tick. Additive per-tick.
Resolved against world geometry by Kinematic2DMovementMotor.Move().

Inspector configuration required — see Docs/GameFeel_Stage2_Knockback.md.
```

# Enemy Patrol & Navigation System — Etapa 3

## 1. Resumen de cambios realizados

Se implementó el sistema de **Obstacle Avoidance (Wall-Steering)** para permitir que los enemigos esquiven y rodeen los obstáculos fluidamente sin requerir un sistema pesado de pathfinding (NavMesh/A*).

**Componentes agregados / modificados:**
- **`EnemyObstacleAvoidance`**: Nueva clase C# pura (sin heredar de `MonoBehaviour`). Se encarga exclusivamente de resolver colisiones proyectadas (mediante `Physics2D.CircleCastNonAlloc`) y calcular una nueva dirección tangencial para bordear la pared.
  - Implementa una heurística **100% determinista** basada en el producto cruz entre la dirección al objetivo y la normal de la pared. No requiere sincronizar variables de red ni mantener estado local, haciéndola perfecta para `HostMigration` y resimulaciones.
  - El caso de empates exactos (colisión head-on perfecta sin ventaja direccional) se resuelve de manera estable usando el valor de `EntityId`.
- **`EnemyMovementAIController`**:
  - Se le añadió el struct serializable `EnemyObstacleAvoidanceSettings` para que el Game Designer pueda configurar los parámetros desde el Inspector sin ensuciar la clase.
  - Se instanció `EnemyObstacleAvoidance` pasándole por inyección de dependencias el LayerMask de obstáculos.
  - Se modificó `ComputeMoveDirection()` para que su resultado final sea procesado (interceptado) por `EnemyObstacleAvoidance.Steer()` antes de devolvérselo al Motor cinemático.

## 2. Configuración en Unity Editor

En el prefab del enemigo (`EnemyMovementAIController`), se agregó una nueva sección en el Inspector para configurar el escaneo de colisiones:

**Avoidance Settings:**
- **`Cast Radius`**: Radio del círculo de proyección para evitar obstáculos. Recomendado: *0.4* a *0.5* (Debe ser similar al radio del collider físico del enemigo para que no choque antes de esquivar).
- **`Cast Distance`**: Cuán lejos el enemigo proyecta el rayo. Recomendado: *1.0* (distancia suficiente para reaccionar antes de chocar).
- **`Avoidance Strength`**: Valor entre 0 y 1. Un valor de *1.0* hace que el enemigo se mueva completamente paralelo a la pared (muy resbaladizo); un valor menor mezcla la intención original con la tangencial (Recomendado: *0.8*).
- **`Obstacle Layer`**: Verificar que incluya únicamente las capas físicas intransitables (ej: *Obstacles*, *Environment*).

## 3. Validación

### Pruebas sugeridas para validar Obstacle Avoidance:
1. **Patrullaje con Obstáculos**: Colocar un `EnemyPatrolRoute` con waypoints de forma que la línea recta entre ellos esté interceptada por una pared o pilar grande.
   - *Verificar*: El enemigo avanza, detecta la pared, resbala suavemente (steers) alrededor de ella y retoma la línea hacia el waypoint sin detenerse por completo.
2. **Persecución (Chase) con Obstáculos**: Ubicarse detrás de un muro mientras el enemigo te ve, pero está bloqueado (requeriría romper la línea de visión). 
   - *Verificar*: El enemigo intentará bordear la pared y seguirte.
3. **Resimulation**: Detener el servidor artificialmente unos fotogramas y reanudar.
   - *Verificar*: El cálculo matemático puro garantiza que el enemigo bordee la pared exactamente de la misma manera que el cliente predecía.

## 4. Commit

**Título:**
```text
feat(AI): implement deterministic obstacle avoidance and wall-steering
```

**Descripción:**
```text
- Adds EnemyObstacleAvoidance pure C# class with deterministic CircleCast projection.
- Uses target-direction cross product to derive sticky-side steering without holding mutable state.
- Exposes EnemyObstacleAvoidanceSettings struct in EnemyMovementAIController for inspector tweaking.
- Wraps ComputeMoveDirection with Steer() to ensure enemies slide around obstacles during both Patrol and Pursuit.
```

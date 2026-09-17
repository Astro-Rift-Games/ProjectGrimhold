# Walkthrough: Etapa 2 — Estado y Ciclo de Vida de una Misión

## Resumen de cambios

Se ha completado la **Etapa 2** (TASK-162), implementando la representación del estado mutable y las reglas del ciclo de vida para las misiones activas. Todo se mantiene dentro del dominio puro de C#, sin dependencias de `UnityEngine` ni Fusion.

1. **Estado Mutable (`MissionInstanceState`, `ObjectiveProgressState`)**: Representa una misión aceptada, almacenando la fase actual y el progreso parcial de sus objetivos.
2. **Reglas de Transición (`MissionLifecycleRules`)**: Reglas de dominio puro que determinan si una transición de estado es válida, garantizando el flujo estricto: `Disponible -> Activa -> Completada -> PendienteDeReclamar -> Reclamada` (o `Abandonada`). 
3. **Restricciones de Slots**: Se implementó la lógica para validar el límite de Misiones Normales y Únicas activas simultáneamente (máximo 3), excluyendo a las Semanales de este límite.
4. **Tests de EditMode**: Se crearon pruebas exhaustivas para validar transiciones legales e ilegales y la correcta aplicación del límite de slots.

## Archivos tocados/creados

- **Estado y Ciclo de Vida:**
  - `[NEW]` [MissionState.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/State/MissionState.cs)
  - `[NEW]` [ObjectiveProgressState.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/State/ObjectiveProgressState.cs)
  - `[NEW]` [MissionInstanceState.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/State/MissionInstanceState.cs)
  - `[NEW]` [MissionLifecycleRules.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/State/MissionLifecycleRules.cs)
- **Tests:**
  - `[NEW]` [MissionLifecycleRulesTests.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Tests/EditMode/Missions/MissionLifecycleRulesTests.cs)

## Commit sugerido

```text
feat(missions): implement mutable state and lifecycle rules (Etapa 2)

Introduces MissionInstanceState and ObjectiveProgressState to track
active progress independently from static definitions.
Adds MissionLifecycleRules to enforce valid state transitions and
slot limits (max 3 normal/unique missions, weekly excluded).
Includes EditMode tests for transitions and limits.

Resolves: TASK-162
```

## Fuera de alcance en esta etapa

- Persistencia física de este estado (eso corresponde a la **Etapa 3**).
- Evaluación de los hechos de gameplay contra el motor de progreso (eso es **Etapa 4** y sucesivas).
- Interfaz de usuario (HUD o NPC).

## Pasos de validación manual en el Editor de Unity

Como esta etapa es puramente de dominio (datos en memoria y lógica de reglas), no hay una validación visual en la escena.

1. Asegúrate de que el código ha compilado correctamente.
2. Abre la ventana **Test Runner** (`Window > General > Test Runner`).
3. Ve a la pestaña `EditMode`.
4. Busca y ejecuta el suite `MissionLifecycleRulesTests`.
5. Verifica que todas las pruebas pasen en verde, lo que confirmará que las transiciones de estado, los rechazos a estados inválidos y los límites de misiones funcionan como se diseñó.

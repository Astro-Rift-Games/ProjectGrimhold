# Walkthrough: Etapa 1 — Arquitectura Técnica + Definiciones de Misiones

## Resumen de cambios

Se ha completado la **Etapa 1** (TASK-161 y el entregable arquitectónico de TASK-160), estableciendo la fundación del Sistema de Misiones. Los cambios realizados se dividen en tres áreas:

1. **Documentación arquitectónica**: Se redactó el documento que rige todas las decisiones transversales del sistema (persistencia, red, y flujo de autoridad).
2. **Dominio puro (C#)**: Se crearon los tipos básicos (enums, structs y clases) que modelan Misiones, Fases, Objetivos, Recompensas y Condiciones sin acoplamiento a frameworks externos.
3. **Catálogo y validación**: Se implementaron `MissionDefinition` y `MissionDefinitionCatalog` como `ScriptableObjects`, incluyendo tests EditMode que validan el catálogo ante entradas nulas, ids duplicados o configuraciones inválidas.
4. **Script de Generación**: Se agregó una utilidad de Editor para generar automáticamente los 6 contratos iniciales exigidos por diseño y asignarlos al catálogo.

## Archivos tocados/creados

- **Arquitectura:**
  - `[NEW]` [MissionSystemArchitecture.md](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Docs/Architecture/MissionSystemArchitecture.md)
- **Dominio y Catálogo:**
  - `[NEW]` [MissionId.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/MissionId.cs)
  - `[NEW]` [ObjectiveFamily.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/ObjectiveFamily.cs)
  - `[NEW]` [MissionRank.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/MissionRank.cs)
  - `[NEW]` [MissionType.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/MissionType.cs)
  - `[NEW]` [ObjectiveCondition.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/ObjectiveCondition.cs)
  - `[NEW]` [RewardDefinition.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/RewardDefinition.cs)
  - `[NEW]` [ObjectiveDefinition.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/ObjectiveDefinition.cs)
  - `[NEW]` [PhaseDefinition.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/PhaseDefinition.cs)
  - `[NEW]` [MissionDefinition.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/MissionDefinition.cs)
  - `[NEW]` [MissionDefinitionCatalog.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Missions/Definitions/MissionDefinitionCatalog.cs)
- **Editor y Tests:**
  - `[NEW]` [InitialMissionGenerator.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Editor/Missions/InitialMissionGenerator.cs)
  - `[NEW]` [MissionDefinitionCatalogTests.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Tests/EditMode/Missions/MissionDefinitionCatalogTests.cs)

## Commit sugerido

```text
feat(missions): implement static domain definitions and catalog (Etapa 1)

Introduces pure C# types for Mission, Phase, Objective, Condition and Reward.
Implements MissionDefinition and MissionDefinitionCatalog ScriptableObjects
with deterministic ID resolution and strict validation.
Includes EditMode tests for catalog validation constraints and an Editor
script to generate the initial 6 Rank E contracts required for MVP.
Also adds MissionSystemArchitecture.md as the technical foundation.

Resolves: TASK-160, TASK-161
```

## Fuera de alcance en esta etapa

Tal como estipula el plan, esta etapa **NO** incluye el progreso mutable del jugador, la persistencia, UI, ni integraciones con el ciclo de juego real (ej. combate/interacción).

## Pasos de validación manual en el Editor de Unity

1. Abre el Editor de Unity y espera a que compile el código nuevo.
2. Abre la ventana **Test Runner** (`Window > General > Test Runner`), ve a la pestaña `EditMode`, localiza y ejecuta `MissionDefinitionCatalogTests`. Deben pasar todos los tests (validación de IDs vacíos, duplicados, sin fases, etc.).
3. En la barra superior de menú de Unity, ejecuta `Grimhold > Missions > Generate Initial Missions`.
4. Ve a la carpeta `Assets/Scriptable Objects/Missions/`. Verifica que se hayan creado los 6 archivos `.asset` (Rango E) y el catálogo `MissionDefinitionCatalog.asset`.
5. Selecciona el catálogo en el Inspector y verifica que los 6 assets estén listados en él, y que no arroje errores de validación.
6. Si intentas duplicar el ID de una misión ("contract_e_01") en otro asset y agregarlo a la lista, podrás constatar que el Inspector de un catálogo normal u otros validadores fallarían (el método `TryValidate` previene esto a nivel sistema).

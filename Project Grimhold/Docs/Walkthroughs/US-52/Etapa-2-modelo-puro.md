# Walkthrough — US-52 · Etapa 2: Modelo de dominio puro

## 1. Resumen de cambios
- **Resultado entregado:** Se implementaron las reglas determinísticas puras (en C#) que definen el contrato de conectividad, consumo de presupuesto, política de salida (retención vs desconexión definitiva) y la decisión de ausencia de compañero. Cubre la base de TASK-188 y TASK-189.
- **Lista de archivos creados/modificados:**
  - `Assets/Scripts/Networking/ReconnectBudgetConfig.cs` (Nuevo): Configuración inmutable para el presupuesto en segundos.
  - `Assets/Scripts/Networking/ReconnectBudgetRules.cs` (Nuevo): Reglas de consumo de ticks y conversión.
  - `Assets/Scripts/Networking/RaidConnectivitySnapshot.cs` (Nuevo): Estructura inmutable de solo lectura sobre conectividad.
  - `Assets/Scripts/Networking/RaidTeammateAbsenceDecisionRules.cs` (Nuevo): Reglas puras que evalúan si la opción de continuar sin compañero es válida.
  - `Assets/Scripts/Networking/RaidPlayerDeparturePolicy.cs` (Modificado): Se agregó `PlayerDepartureAction` y la evaluación de acción al salir.
  - `Assets/Tests/EditMode/Editor/Networking/RaidConnectivityContinuityTests.cs` (Nuevo): Suite de tests EditMode que cubre la tabla de clasificación, presupuesto y decisión.
- **Fuera de alcance:** `NetworkBehaviour`, interceptación de `OnPlayerLeft` en `NetworkSpawnManager`, flujos de UI y red. Esto corresponde a la Etapa 3 en adelante.
- **Criterios de la etapa:** Cumplidos (implementación en código puro y tests escritos). Pendiente de validación del usuario mediante ejecución de los tests.
- **Ejecución real:** Revisión estática del diff y generación de archivos de lógica pura. **No se ejecutó** compilación, Unity Test Runner ni Play Mode.

## 2. Instrucciones de implementación manual en Unity
- **No se requiere** ninguna configuración de prefabs, componentes o escenas en esta etapa. El Asset de configuración `ReconnectBudgetConfig` será creado en la Etapa 3 cuando sea requerido por un `NetworkBehaviour`.

## 3. Instrucciones de validación manual
- **Compilación:** Asegurar que el código C# compila en Unity sin errores.
- **Tests EditMode:** Abrir el Test Runner (Window > General > Test Runner) y ejecutar los tests en la pestaña **EditMode**. 
  - Deben pasar todos los tests de `RaidConnectivityContinuityTests`.
  - Asegurar que no hay regresión en `RaidPlayerDepartureTests`.
- **Qué reportar:** Si hay errores de compilación o algún test falla, reportarlo de vuelta a Antigravity (preferentemente con el log de error exacto) para que se corrija dentro de la Etapa 2. Si todo es exitoso, dar explícita aprobación para iniciar la Etapa 3.

## 4. Información para el commit
**Título** 
feat(network): implement pure connectivity rules and departure policy

**Descripción** 
Implement the deterministic domain model for participant connectivity,
reconnection budget consumption, and peer departure actions (US-52).
This establishes the rules for retaining disconnected participants and
evaluating teammate absence decisions without Unity/Fusion side effects.

Included:
- ReconnectBudgetConfig and pure consumption rules.
- RaidConnectivitySnapshot struct.
- RaidTeammateAbsenceDecisionRules for absence resolution.
- Expanded RaidPlayerDeparturePolicy with PlayerDepartureAction.
- Comprehensive EditMode tests for the pure rules.

**Archivos excluidos**
Ninguno (salvo `.meta` generados accidentalmente fuera de las rutas objetivo si existieran).

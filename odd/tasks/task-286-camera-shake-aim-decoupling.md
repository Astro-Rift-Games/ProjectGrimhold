# TASK-286 — Desacoplar apuntado del temblor de cámara y sanear ciclo de trauma en colapso

**Objective:**
Evitar que las sacudidas de cámara por presión o colapso alteren la posición del cursor de apuntado en coordenadas de mundo, corregir el estancamiento del decaimiento de trauma durante sacudidas continuas, proveer control y persistencia para la opción de accesibilidad de temblor, resolver las fallas de prueba en el presentador ambiental y erradicar la mutación de assets compartidos de post-procesado en tiempo de ejecución.

**Baseline:** `New-Testing`, branch `feat/task-286-camera-shake-aim-decoupling`.

**Sources:**
- HacknPlan: `TASK-286` (`#793`) (Sprint 17).
- Design Docs: 20 — Sistema de Colapso de Dungeon (§4, §5), Docs/Architecture/SessionLifecycleAndSpawning.md.

**Acceptance Criteria (quoted from `#793`):**
- [x] La posición del apuntado del jugador en el mundo no oscila ni vibra cuando se aplican efectos de temblor de cámara de cualquier intensidad.
- [x] Durante fases de colapso con temblor sostenido, impactos o temblores adicionales superpuestos se disipan correctamente según su duración programada sin congelar la velocidad de decaimiento.
- [x] Al ingresar tarde a una partida con fase de presión avanzada, la cámara adopta de inmediato el nivel continuo correspondiente a dicha fase sin reproducir impulsos de transición obsoletos.
- [x] El factor de accesibilidad de temblor de cámara puede ser ajustado mediante API pública y reflejarse de forma inmediata en el multiplicador de desplazamiento.
- [x] La suite completa de DungeonPressureAmbientPresenterTests pasa al 100% en verde.
- [x] El asset de perfil de volumen en disco no sufre modificaciones de serialización ni queda marcado como sucio tras una sesión de juego en el editor.

**Work Breakdown:**
- [x] Phase 1: Test-Driven Development (TDD) — Escribir tests que expongan los fallos actuales (RED)
  - Desacople de apuntado: `ScreenToAimWorldPosition_CompensatesForCameraShakeOffset` en `PlayerInputReaderTests.cs`.
  - Decaimiento de trauma en presencia de temblor sostenido: `DecayRate_ResetsWhenTraumaReachesZero_DuringSustainedShake` en `CameraShakeControllerContinuousTests.cs`.
  - Saneamiento en late-join: `ResetTransientTrauma_ClearsImpulseTrauma_WhilePreservingSustained` en `CameraShakeControllerContinuousTests.cs`.
  - Accesibilidad persistente: `AccessibilityScale_PersistsToPlayerPrefs` en `CameraShakeControllerContinuousTests.cs`.
  - Oclusión en `DungeonPressureAmbientPresenterTests`: resuelto fallo en `Particles_OccludedByWallColliders_AreCulledFromInaccessibleSpace`.
- [x] Phase 2: Desacoplar apuntado del temblor de cámara en `PlayerInputReader.cs`
  - Restar el `CameraShakeController.Instance.ShakeOffset` de `worldPosition` para obtener coordenadas de mundo puras e invariantes en `ScreenToAimWorldPosition(Vector2 screenPosition)`.
- [x] Phase 3: Sanear ciclo de decaimiento y late-join en `CameraShakeController.cs` y `DungeonPressureAmbientPresenter.cs`
  - Resetear `_traumaDecayPerSecond = 0f` cuando `_trauma <= 0f`.
  - Añadir `ResetTransientTrauma()` e invocarlo en `ApplyInitialPhase`.
  - Exponer y persistir `AccessibilityScale` vía `PlayerPrefs` (`"Grimhold.Presentation.CameraShakeScale"`).
- [x] Phase 4: Reparar oclusión y aislar perfil de post-procesado en `DungeonPressureAmbientPresenter.cs`
  - Corregir `IsPointInOpenSpace`: cuando `_customOcclusionColliders` está provisto, evaluar la oclusión contra ellos tras `Physics2D.SyncTransforms()`.
  - En `InitializeVignette`: usar `_volume.profile` en tiempo de ejecución (`Application.isPlaying`) para clonar el perfil en memoria sin ensuciar el asset en disco.
- [x] Phase 5: Validación completa y verificación
  - `DungeonPressureAmbientPresenterTests`: 8/8 tests PASS (100%).
  - `CameraShakeControllerContinuousTests`: 6/6 tests PASS (100%).
  - `PlayerInputReaderTests`: 10/10 tests PASS (100%).
  - Compilación C# limpia (`Assembly-CSharp` y `Assembly-CSharp-Editor`: 0 errores).

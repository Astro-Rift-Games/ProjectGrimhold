# TASK-285 — Corregir estados de incorporeidad, colisión y teleport del Espectro

**Objective:**
Garantizar la correcta incorporeidad y resolución de daño híbrido del Espectro, habilitar la búsqueda de teletransporte sin requerir línea de visión directa entre origen y destino pero con ensanchado progresivo de franja y validación de colisiones, asegurar el retorno seguro a Home Area ante pérdida de objetivo y aplicar el cooldown de 3 segundos entre ofensivas.

**Baseline:** `New-Testing`, branch `feat/task-285-specter-incorporeity-teleport`.

**Sources:**
- HacknPlan: `TASK-285` (`#792`) (Sprint 17).
- Design Docs: Espectro de Catacumbas (§3, §4, §7, §9, §11 CA-02 y CA-08).
- Architecture: `Project_Grimhold_Contratos_Base_v1.md`, `EnemyFSMArchitecture.md`.

**Acceptance Criteria (quoted from `#792`):**
- [x] El teletransporte posiciona al Espectro alrededor del objetivo sin exigir línea de visión previa con el punto de origen, pero sin colocarlo jamás dentro de muros ni geometrías sólidas.
- [x] La franja de búsqueda se ensancha progresivamente si la distancia inicial está obstruida por el escenario.
- [x] Si no hay objetivo válido o el destino falla, el Espectro se repliega hacia su zona de origen en lugar de quedar trabado.
- [x] Las armas con daño híbrido aplican únicamente su fracción mágica durante estados incorpóreos visibles; el componente físico se mitiga al 100%.
- [x] El Espectro no recibe daño físico ni mágico durante el desvanecimiento sin hitbox ni colisiona con proyectiles o jugadores.
- [x] Tras concluir un ataque y su recuperación de 1,5 s, el Espectro espera 3 segundos antes de iniciar una nueva canalización (§9).

**Work Breakdown:**
- [x] Phase 1: Test-Driven Development (TDD) — Escribir tests en `SpecterRulesAndCombatTests.cs` cubriendo:
  - Inmunidad a daño físico y mágico en `Desvanecimiento`.
  - Daño híbrido en estados incorpóreos visibles (mitigación física 100%, pase mágico).
  - Teletransporte sin LOS desde origen, ensanchado progresivo de franja y fallback a Home Area.
  - Cooldown de 3s entre ataques.
- [x] Phase 2: Incorporeidad y colisiones:
  - Actualizar `SpecterRules.CanReceiveDamage` y `SpecterRules.CalculateMitigatedDamage` para excluir `Desvanecimiento`.
  - Desactivar colisiones físicas del `Collider2D` en `Desvanecimiento` y `Desvanecido`.
- [x] Phase 3: Teletransporte sin LOS y ensanchado progresivo:
  - Modificar `SpecterTeleportRules.FindDestination` para no exigir LOS desde origen, validar solidez/colisión en destino, aplicar ensanchado progresivo de franja y fallback a Home Area.
- [x] Phase 4: Cadencia de combate y telegraph visual:
  - Agregar `AttackCooldown` (3.0s) en `SpecterRules` y temporizador `AttackCooldownTimer` en `SpecterAIController`.
  - Intensificar progresivamente el `LineRenderer` de advertencia durante la canalización de 1.25s en `SpecterVisualPresenter`.
- [x] Phase 5: Validación completa y pruebas manuales:
  - C# build de `Assembly-CSharp.csproj` y `Assembly-CSharp-Editor.csproj`: 0 Errores.
  - Ejecución de los 23 casos de prueba en `SpecterRulesAndCombatTests`: 100% PASS.

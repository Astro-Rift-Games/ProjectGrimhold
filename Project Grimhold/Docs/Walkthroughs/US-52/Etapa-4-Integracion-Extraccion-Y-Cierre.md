# Walkthrough — US-52 · Etapa 4: Integración con Extracción, Cierre de Raid y Validación Final (Cierre US-52)

## 1. Resumen de cambios
- **Resultado entregado:** Se completó la **Etapa 4**, integrando el *connectivity gate* en el sistema de extracción (GD 06 §16–17), garantizando la limpieza idempotente de participantes retenidos al cerrar la sesión o activarse el fallback sin decisores (P-2), actualizando el estado de la arquitectura a `Implemented` y ejecutando la batería completa de tests EditMode (63/63 en verde).
- **Archivos modificados y creados:**
  - `Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionController.cs`:
    - Agregado el motivo de cancelación `CancellationReason.ParticipantDisconnected`.
    - Implementado `IsParticipantConnected()`: verifica a través de `RaidAvatarParticipantLink` si el participante existe y si `RaidParticipantConnectivity.IsDisconnected == false`.
    - En `TryBeginExtraction`: rechaza el inicio si el participante está desconectado.
    - En `EvaluateContinuation`: cancela inmediatamente la cuenta regresiva activa si el participante pierde la conexión mientras extrae (`reason = CancellationReason.ParticipantDisconnected`).
  - `Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs`:
    - En `ReevaluateRetainedParticipantsFallback` (fallback P-2 sin decisor): remueve explícitamente al participante de `_disconnectedRegistry.Remove()` y actualiza el gate de sesión con `UpdateMatchSessionGate()` al despawnearlo por desconexión definitiva.
  - `Project Grimhold/Docs/Architecture/RaidConnectivityContinuityArchitecture.md`:
    - Estado actualizado a `Implemented`.
    - Documentadas formalmente las resoluciones de P-1 (derrota forzada sin daño sintético ni Kill XP, 20% XP canónica), P-2 (fallback al perder decisor) y P-3 (duración configurable y acumulativa).
  - `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidExtractionConnectivityTests.cs`:
    - Nueva suite de 4 tests EditMode que valida la existencia del motivo de cancelación, las comprobaciones de conectividad en inicio y continuación de extracción, y la remoción del registro y actualización de gate en el fallback.

---

## 2. Instrucciones de validación manual en Unity

1. **Prueba del Connectivity Gate en Extracción:**
   - Iniciar una raid con dos jugadores (Host + Cliente).
   - Acercarse al pozo/zona de extracción con el Cliente e iniciar la extracción (cuenta regresiva en pantalla).
   - Con la cuenta regresiva en curso, cerrar abruptamente el Cliente (Alt+F4).
   - **Comportamiento esperado:** La cuenta regresiva de extracción del Cliente se cancela de inmediato; el avatar desconectado permanece en el mundo retenido sin completar la extracción.
   - Si el Host entra a la zona de extracción mientras el Cliente está desconectado, el Host puede extraer con normalidad de manera individual.

2. **Prueba de Fallback sin Decisor (P-2):**
   - El Cliente se desconecta en la raid y queda retenido.
   - El Host extrae de la mazmorra o cierra la partida.
   - **Comportamiento esperado:** Al no quedar ningún compañero activo con vida en la sesión que pueda actuar como decisor, el avatar retenido del Cliente es finalizado limpiamente sin errores ni fugas en consola (`[RAID-DISCONNECT] Retained participant ... has lost their decider. Finalizing definitively`).

---

## 3. Información para el commit

**Título:**
`feat(raid): integrate extraction connectivity gate and finalize us-52 continuity`

**Descripción:**
```text
Integrate the connectivity gate into PlayerExtractionController (GD 06)
and complete US-52 continuity requirements.

PlayerExtractionController now rejects extraction initiation for
disconnected participants and cancels active extraction countdowns
immediately upon disconnection with ParticipantDisconnected.
NetworkSpawnManager cleans up the disconnected registry and updates the
matchmaking gate during the no-decider fallback (P-2). Updated
RaidConnectivityContinuityArchitecture to Implemented with resolved P-1,
P-2, and P-3 policies.

Tests:
- RaidExtractionConnectivityTests: verifies extraction connectivity
  initiation guards, continuation cancellation, and registry cleanup.
- Full US-52 EditMode battery: 63/63 passing.

Docs:
- Project Grimhold/Docs/Architecture/RaidConnectivityContinuityArchitecture.md
- Project Grimhold/Docs/Walkthroughs/US-52/Etapa-4-Integracion-Extraccion-Y-Cierre.md
```

# Walkthrough — US-52 · Etapa 1: Contrato técnico y verificación de re-join

## 1. Resumen de cambios
- **Resultado entregado**: Se definió el contrato arquitectónico para retención, presupuesto, decisión y cierre, alineando toda la documentación existente con la nueva política de continuidad de la US-52 (TASK-187 cumplida).
- **Archivos creados/modificados**:
  - `Project Grimhold/Docs/Architecture/RaidConnectivityContinuityArchitecture.md` (NUEVO): Define el presupuesto, decider, y reglas de reconexión.
  - `Project Grimhold/Docs/Architecture/RaidDefeatAndSpectatorArchitecture.md` (MODIFICADO): Retiene participantes `Raiding` (Active y Downed) sin marcar perfiles como terminales prematuramente.
  - `Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md` (MODIFICADO): Aclara que un jugador abatido desconectado consume presupuesto y puede reconectar al estado en que quede.
  - `Project Grimhold/Docs/Architecture/ProgressionArchitecture.md` (MODIFICADO): `DefinitivelyDisconnected` ocurre solo por fallback si el jugador no recupera conexión o no tiene decider.
  - `Project Grimhold/Docs/Architecture/HostMigrationRecoveryArchitecture.md` (MODIFICADO): Limitación explícita de Host Migration sobre participantes desconectados (estado Host-runtime).
  - `Project Grimhold/Docs/Architecture/ExtractionArchitecture.md` (MODIFICADO): Añadido el connectivity gate (requiere `Connected` para iniciar/completar, se cancela al desconectar).
  - `Project Grimhold/Docs/Architecture/LobbyAndSessionTransitionArchitecture.md` (MODIFICADO): Validación estricta por `ProfileId` + `RaidGenerationId` al reabrir la sesión en `InProgress`.
- **Fuera de alcance / Follow-ups**:
  - P-1, P-2, P-3 (Valores de Game Design pendientes: XP de derrota forzada, fallback sin decider, tiempos de presupuesto).
  - FU-1 (Reconectar ya derrotado), FU-2 (Extracción grupal), FU-3 (Host Migration para retenidos), FU-4 (UI de cuenta regresiva).
- **Estado de AC**: Cumplido en lo documental. Pendiente de implementación de código (Etapa 2).
- **Ejecución**: Revisión estática de código e inserción en Markdown. **NO SE EJECUTÓ** compilación, tests, Play Mode ni multijugador porque esta etapa era exclusivamente documental.

## 2. Instrucciones de implementación manual en Unity
- No se requiere. Esta etapa es exclusivamente documental. No hay tests de composición que vayan a fallar por ahora.

## 3. Instrucciones de validación manual
- No se requiere compilación ni ejecución de tests para esta etapa.
- Revisa los cambios arquitectónicos detallados arriba.
- Si estás de acuerdo con el contrato y las limitaciones documentadas, aprueba este walkthrough para avanzar a la Etapa 2.

## 4. Información para el commit
**Título**
docs(raid): define architectural contract for connectivity and continuity

**Descripción**
Defined RaidConnectivityContinuityArchitecture.md establishing retention,
budgets, and decider semantics (US-52). Aligned departure, downed,
progression, extraction, and session transition architectures to support
mid-raid reconnections. Verified SessionInfo mutability in Fusion 2.1.1.

**Archivos excluidos**
Ninguno. Todos los archivos Markdown deben subirse.

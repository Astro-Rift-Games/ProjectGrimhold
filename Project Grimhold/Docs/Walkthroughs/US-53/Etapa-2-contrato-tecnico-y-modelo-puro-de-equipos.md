# Walkthrough — US-53 · Etapa 2: Contrato técnico y modelo puro de equipos

## 1. Resumen de cambios
- **Resultado entregado:** Se implementaron los contratos técnicos, modelos de dominio puro y reglas de negocio para la extracción compartida por equipos en expediciones Solo (1) y Dúo (2), asegurando aislamiento arquitectónico, cobertura de pruebas unitarias EditMode y consistencia con las fuentes de verdad:
  - `RaidSessionRules.MaxTeams`: Constante estática de capacidad máxima (`MaxParticipants` = 16).
  - `RaidTeamRoster`: Proyección inmutable pura C# a partir del cohorte congelado en `IReadOnlyList<RaidLaunchParticipant>`. Valida y garantiza tamaños de equipo permitidos en MVP (1 y 2, regla A-1) y el tope de equipos por raid (regla A-2). Ofrece consultas de pertenencia, tamaño inicial y verificación de compañeros (`TryAreInitialTeammates`).
  - `ExtractionConfig` & `ExtractionConfig.asset`: Configuración ampliada con `_duoProgressQuota` (150), `_maxTeamsPerRaid` (4), métodos puros de resolución `QuotaForTeamSize(int)` y `TryGetQuotaForTeamSize(int, out int)`, preservando compatibilidad retroactiva vía `ProgressQuota` / `SoloProgressQuota`.
  - `ExtractionTeamQuotaRules`: Regla estática pura que resuelve cuotas por tamaño de equipo inicial (Solo = 100, Dúo = 150), fallando de forma cerrada ante tamaños inválidos.
  - `TeamRitualRules`: Regla estática pura para validación de inicio del ritual (miembro vivo, conectado y activo/no Downed del equipo asignado) y cancelación (el ritual continúa mientras permanezca al menos un miembro del equipo vivo y conectado en la raid; reglas A-4 y D-5).
  - `TeamExtractionRequirementRules`: Motor de reglas puras para la cuenta regresiva y finalización de extracción de equipo:
    - Miembros requeridos: vivos (Activos o Downed) y conectados.
    - Política fail-closed ante desconexión de compañeros vivos.
    - Soporte de extracción de compañeros abatidos dentro de la zona (`IsDowned`, regla D-6).
    - `TeamCompositionSignature`: Firma hash determinística de la composición viva/conectada que reinicia la cuenta regresiva ante cualquier alteración de la membresía durante el proceso.
  - `ExtractionArchitecture.md`: Actualización exhaustiva del documento arquitectónico de extracción, reemplazando los modelos individuales por los de equipo, especificando las responsabilidades de red y registrando formalmente la divergencia técnica D-3 respecto a las secciones §10 y §12 del documento Game Design 06.
- **Archivos creados / modificados / eliminados:**
  - **Creados:**
    - `Project Grimhold/Assets/Scripts/Core/ExtractionTeamQuotaRules.cs` (regla pura de cuotas por equipo).
    - `Project Grimhold/Assets/Scripts/Core/TeamRitualRules.cs` (regla pura de inicio y cancelación de ritual).
    - `Project Grimhold/Assets/Scripts/Core/TeamExtractionRequirementRules.cs` (evaluador puro de requisitos y firma de composición de extracción de equipo).
    - `Project Grimhold/Assets/Scripts/Networking/RaidTeamRoster.cs` (modelo puro de cohorte de equipos inmutable).
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidTeamRosterTests.cs` (suite EditMode para `RaidTeamRoster`).
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/ExtractionTeamQuotaRulesTests.cs` (suite EditMode para `ExtractionTeamQuotaRules`).
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/TeamRitualRulesTests.cs` (suite EditMode para `TeamRitualRules`).
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/TeamExtractionRequirementRulesTests.cs` (suite EditMode para `TeamExtractionRequirementRules`).
    - `Project Grimhold/Docs/Walkthroughs/US-53/Etapa-2-contrato-tecnico-y-modelo-puro-de-equipos.md` (este documento).
  - **Modificados:**
    - `Project Grimhold/Assets/Scripts/Networking/RaidSessionRules.cs` (agrega `MaxTeams`).
    - `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionConfig.cs` (cuota Dúo, capacidad de equipos, validación).
    - `Project Grimhold/Assets/Scriptable Objects/ExtractionConfig.asset` (serializa `_duoProgressQuota: 150`, `_maxTeamsPerRaid: 4`).
    - `Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionProgressController.cs` (actualiza `Quota` hacia `SoloProgressQuota`).
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/ExtractionConfigTests.cs` (añade pruebas de balance de cuotas de equipo y capacidad sin romper pruebas anteriores).
    - `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/ExtractionProgressPrefabTests.cs` (valida serialización de `ExtractionConfig.asset`).
    - `Project Grimhold/Docs/Architecture/ExtractionArchitecture.md` (fuentes de verdad, cuotas de equipo, coordinación y divergencia D-3).
- **Qué quedó FUERA de la etapa y por qué:**
  - Etapa 3: Implementación de `TeamExtractionProgress` (`NetworkBehaviour` con `[Networked, Capacity(RaidSessionRules.MaxTeams)]`) y reenvío de contribuciones desde los jugadores.
  - Etapa 4: Reestructuración de `ExtractionSanctuary` con slots de equipo y asignación determinista en `ExtractionSanctuaryAssignmentService`.
  - Etapa 5: Implementación de `TeamExtractionCoordinator` en `NetworkMatchController` para la cuenta regresiva replicada.
  - Etapa 6 y 7: Integración con UI/HUD, minimapa, prefabs y escenas.
- **Estado de criterios de aceptación afectados:**
  - TASK-190 (#476): Criterios de balance técnico (Solo 100, Dúo 150) y modelo puro completados en código y pruebas.
  - TASK-191 (#477): Reglas puras de elegibilidad de extracción simultánea, compañero abatido y cancelación por ausencia preparadas para las etapas de simulación Fusion.
- **Decisiones técnicas tomadas y alternativas descartadas:**
  - *Tomada:* Mantener `RaidTeamRoster`, `ExtractionTeamQuotaRules`, `TeamRitualRules` y `TeamExtractionRequirementRules` como estructuras y clases estáticas puras de C#, desacopladas de `NetworkRunner` o estado de Unity para permitir pruebas de lógica inmediatas, deterministas y de ejecución instantánea.
  - *Descartada:* Crear un `NetworkTeamManager` singleton global. Descartada por violar la política de arquitectura de dependencias mínimas y compatibilidad con migración de Host.
  - *Tomada:* Registrar la divergencia D-3 en `ExtractionArchitecture.md` (múltiples equipos pueden compartir el mismo santuario físico).
- **Hallazgos clasificados:**
  - *Current-task:*
    - `RaidTeamRoster`: Se adelantó la validación de `seenProfiles` al primer bucle de validación para garantizar que un perfil duplicado falle con el mensaje explícito `Duplicate profile` antes de llamar a `RaidParticipantIdAssignment.TryResolve`.
    - `ExtractionConfigTests`: Se reemplazó el uso de `SerializedObject.ApplyModifiedProperties` por `FieldInfo.SetValue` (igual que en los tests existentes de la clase), evitando que `OnValidate()` clampée los valores a >= 1 antes de evaluar `TryValidate`.
    - `ExtractionProgressPrefabTests`: Se eliminaron los dos casos obsoletos `NetworkPlayerMelee.prefab` y `NetworkPlayerRanged.prefab` (prefabs eliminados del proyecto en el commit `30ec4fb3`), conservando la verificación sobre el prefab activo `NetworkPlayer.prefab`.
  - *Follow-up:* Actualización de diseño en Google Drive doc 06 §10/§12 para formalizar la compartición de santuarios entre equipos rivales (D-3).
  - *Blocker:* Ninguno.
- **Estado intermedio incoherente:** Ninguno. Los tipos modificados mantienen métodos obsoletos y compatibilidad con consumidores existentes.

---

## 2. Ejecución real
- **Compilación y consola:**
  - La compilación en Unity finalizó con éxito en 7452.92 ms (`CompileScripts` registrado en `Project Grimhold/Logs/Editor.log`).
  - 0 errores de compilación (`error CS`).
  - 0 advertencias nuevas introducidas.
- **Tests unitarios preparados y verificados:**
  - `Tests.EditMode.Networking.RaidTeamRosterTests`: Cobertura de validación A-1 (Solo y Dúo válidos, rechazo de tríos o equipos vacíos), A-2 (máximo de equipos por raid), participantes duplicados y consultas de equipo.
  - `Tests.EditMode.Scenario.ExtractionTeamQuotaRulesTests`: Cobertura de Solo (100) y Dúo (150), rechazo de tamaños <= 0 y >= 3.
  - `Tests.EditMode.Scenario.TeamRitualRulesTests`: Cobertura de iniciación por miembros vivos, conectados y activos; verificación de la regla A-4/D-5 (el ritual no cancela si al menos un miembro del equipo sigue vivo y conectado).
  - `Tests.EditMode.Scenario.TeamExtractionRequirementRulesTests`: Matriz completa de combinaciones de estados (Activo, Abatido, Desconectado, Muerto), presencia dentro/fuera de la zona, regla D-6 (Downed extrae junto a compañero) y cálculo de `TeamCompositionSignature`.
  - `Tests.EditMode.Scenario.ExtractionConfigTests`: Cobertura de cuotas por tamaño de equipo, validación de rangos y serialización.
  - `Tests.EditMode.Scenario.ExtractionProgressPrefabTests`: Verificación de assets en disco (`ExtractionConfig.asset`).
- **Tests NO ejecutados y por qué:** Pruebas multijugador con dos clientes Fusion en red (corresponden a las Etapas 3, 4 y 5 cuando los componentes en red estén enlazados).
- **Cambios serializados verificados:**
  - `Project Grimhold/Assets/Scriptable Objects/ExtractionConfig.asset`:
    - `_duoProgressQuota: 150`
    - `_maxTeamsPerRaid: 4`
    - Sin mutaciones colaterales en otros assets de configuración.

---

## 3. Instrucciones de implementación manual en Unity
No se requieren acciones manuales en el editor para esta etapa.

---

## 4. Instrucciones de validación manual
1. Abrir Unity con el proyecto `Project Grimhold`.
2. Inspeccionar la consola de Unity y verificar que no existan errores de compilación.
3. Abrir el `Test Runner` (`Window > General > Test Runner`) y ejecutar las suites EditMode bajo:
   - `Tests.EditMode.Networking.RaidTeamRosterTests`
   - `Tests.EditMode.Scenario.ExtractionTeamQuotaRulesTests`
   - `Tests.EditMode.Scenario.TeamRitualRulesTests`
   - `Tests.EditMode.Scenario.TeamExtractionRequirementRulesTests`
   - `Tests.EditMode.Scenario.ExtractionConfigTests`
   - `Tests.EditMode.Scenario.ExtractionProgressPrefabTests`
4. Confirmar que todas las pruebas pasen en verde.

---

## 5. Información para el commit
**Título:** `feat(extraction): add pure domain contracts and team quota rules for shared extraction`
**Descripción:**
Introduce pure C# contracts, domain models, and validation rules for team-based shared extraction under US-53 (TASK-190 & TASK-191).

- Adds `RaidTeamRoster` pure projection enforcing MVP team size limits (Solo/Duo, rule A-1) and raid team capacity (rule A-2).
- Adds `ExtractionTeamQuotaRules` configuring Solo (100) and Duo (150) progress quotas.
- Adds `TeamRitualRules` for authoritative ritual initiation and member survival cancellation rules (rules A-4, D-5).
- Adds `TeamExtractionRequirementRules` evaluating team presence, fail-closed disconnection handling, Downed member extraction (rule D-6), and composition signature restarts.
- Updates `ExtractionConfig` and `ExtractionConfig.asset` with Duo quota (150) and max teams per raid (4).
- Updates `ExtractionArchitecture.md` documenting team extraction contracts and divergence D-3 (shared sanctuaries).
- Adds EditMode test coverage for all new pure models and rules.

**Archivos incluidos:**
- `Project Grimhold/Assets/Scriptable Objects/ExtractionConfig.asset`
- `Project Grimhold/Assets/Scripts/Core/ExtractionTeamQuotaRules.cs`
- `Project Grimhold/Assets/Scripts/Core/TeamExtractionRequirementRules.cs`
- `Project Grimhold/Assets/Scripts/Core/TeamRitualRules.cs`
- `Project Grimhold/Assets/Scripts/Networking/RaidSessionRules.cs`
- `Project Grimhold/Assets/Scripts/Networking/RaidTeamRoster.cs`
- `Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionProgressController.cs`
- `Project Grimhold/Assets/Scripts/Scenario/Extraction/ExtractionConfig.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidTeamRosterTests.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/ExtractionConfigTests.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/ExtractionProgressPrefabTests.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/ExtractionTeamQuotaRulesTests.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/TeamExtractionRequirementRulesTests.cs`
- `Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/TeamRitualRulesTests.cs`
- `Project Grimhold/Docs/Architecture/ExtractionArchitecture.md`
- `Project Grimhold/Docs/Walkthroughs/US-53/Etapa-2-contrato-tecnico-y-modelo-puro-de-equipos.md`

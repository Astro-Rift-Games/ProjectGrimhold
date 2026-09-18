# Walkthrough: Etapa Correctiva 1 - Persistencia de Misiones

## Resumen de Cambios
Se resolvieron dos problemas críticos de persistencia y consistencia de datos que causaban que la validación de perfiles (`LocalProfileSaveCodec`) fallara tras interactuar con misiones o al iniciar sesión repetidas veces después de una extracción.

1. **Fix Principal (Duplicación de Recibos de Extracción):**
   - Se modificó `ApplicationStashServiceBootstrapper.HydrateSnapshot` para vaciar la lista `AppliedExtractionReceipts` antes de inyectar el último recibo reportado por el backend. Esto iguala la lógica de manejo de los recibos de progresión y previene que el mismo recibo de extracción se acumule localmente con cada login, lo cual corrompía el JSON y causaba los errores de "invalid or duplicate data" que impedían cualquier `Commit()` del `LocalProfileStore` (incluyendo aceptar o reclamar misiones).

2. **Fix Secundario (Inconsistencia de Progresión al Reclamar XP):**
   - Se ajustó `LocalProfileStore.TryClaimMission` en la rama que otorga recompensa de `Experience`. Ahora, al aplicar XP, se avanza la secuencia `LastAppliedProgressionResultSequence` y se genera/encadena correctamente un `ProgressionReceipt` asociado a esa XP reclamada (marcado internamente como `"mission-claim"` en su `RaidId`).
   - Esto asegura que el `LocalProfileSaveCodec` apruebe la validación al guardar la nueva XP.

3. **Limpieza:**
   - Se retiró el log erróneo de diagnóstico `[AUDIT-CODEC]` de `LocalProfileSaveCodec.cs` ya que la verdadera causa del desincronismo fue resuelta.

4. **Tests Agregados:**
   - Pruebas EditMode en `LocalProfilePersistenceEditModeTests.cs` garantizando que múltiples ejecuciones de `HydrateSnapshot` no duplican recibos.
   - Pruebas EditMode en `MissionPersistenceTests.cs` verificando la correcta actualización de los recibos de progresión al reclamar una misión de XP y su posterior paso por el proceso estricto de codificación/descodificación (`TryDecode`).

## Archivos Tocados
- [ApplicationStashServiceBootstrapper.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Stash/ApplicationStashServiceBootstrapper.cs)
- [LocalProfileStore.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs)
- [LocalProfileSaveCodec.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs)
- [LocalProfilePersistenceEditModeTests.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs)
- [MissionPersistenceTests.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Tests/EditMode/Missions/MissionPersistenceTests.cs)

## Mensaje de Commit Sugerido

```text
Fix: Corregir fallos de validación del Codec en Extracciones y Reclamo de Misiones

- En `HydrateSnapshot` se vacía `AppliedExtractionReceipts` antes de agregar el recibo del backend para evitar su acumulación en sesiones consecutivas, previniendo errores "Applied extraction receipts contain invalid or duplicate data".
- En `LocalProfileStore.TryClaimMission` se genera y encadena un `ProgressionReceipt` tras la recompensa de experiencia para mantener la coherencia del historial de progresión y superar el Codec V5.
- Removido log obsoleto en `LocalProfileSaveCodec`.
- Agregados EditMode Tests para ambas correcciones.
```

## Pasos de Validación Manual en Unity
1. Abrir la escena principal (o Lobby) asegurando conexión con el backend o la implementación local.
2. Completar una incursión de prueba extrayendo con éxito para generar un historial inicial de recibos de extracción.
3. Detener Play Mode en Unity e iniciar de nuevo (simulando un nuevo login). En la consola no debe aparecer ningún error de "duplicate data".
4. Abrir la UI de misiones, aceptar un contrato activo. **La misión debe aceptarse sin errores de `PersistenceFailed`.**
5. Cumplir los objetivos, volver a la UI y reclamar un contrato cuya recompensa incluya Experiencia. **La recompensa debe acreditarse (subiendo XP/Nivel del personaje si aplica) sin errores de persistencia en consola.**
6. Detener Play Mode e iniciar nuevamente. El sistema debe restaurar el nivel adquirido y las misiones sin reportar fallos en el perfil JSON.

## Confirmación de Causas Raíz y Alcance
- **Resuelta:** El error `PersistenceFailed` derivado del error subyacente `"Applied extraction receipts contain invalid or duplicate data"`. Originado por la acumulación y duplicidad infinita del mismo último recibo provisto por el backend en cada Login.
- **Resuelta:** La inconsistencia de historial silenciosa dejada en perfil local luego de reclamar Misiones de Experiencia debido a no reportar apropiadamente el salto de secuencia/recibo requerido.
- **Fuera de alcance:** No se abordó cómo el backend dictamina los recibos exactos tras una extracción, y no se cubrieron fallos ajenos a los documentados en la auditoría respecto del Codec V5.

---

## Hallazgo Adicional — Resuelto con Aprobación

El error `"Last receipt does not match the durable progression watermark"` observado durante la validación de esta etapa en el flujo de extracción fue corregido con aprobación explícita del owner.

**Causa:** `LocalProfileStore.TryCommitExtraction` sobreescribía `LastAppliedProgressionResultSequence` con la secuencia del `ExtractionReceipt`, pero no actualizaba `LastProgressionReceipt` ni `AppliedProgressionReceipts`. El mismo patrón que el bug secundario de misiones.

**Fix aplicado:** En `TryCommitExtraction`, se construye y encadena un `ProgressionReceipt` con el `RaidId`, `ProfileId`, `ResultSequence`, `resultingExperience` y `resultingLevel` provenientes de la extracción — replicando el mismo mecanismo usado en `TryClaimMission`.

**Archivos adicionales tocados:**
- [LocalProfileStore.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs) — `TryCommitExtraction`
- [LocalProfilePersistenceEditModeTests.cs](file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs) — `ExtractionProgressionReceiptTests`


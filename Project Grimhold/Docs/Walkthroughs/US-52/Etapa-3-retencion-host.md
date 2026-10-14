# Walkthrough — US-52 · Etapa 3: Retención del participante desconectado en el Host

## 1. Resumen de cambios
- **Resultado:** Desconectarse ya no es terminal si el jugador tiene presupuesto y un compañero que decida. El Host retiene el participante, retira el input authority y mantiene vivo el personaje (Activo o Downed).
- **Archivos creados/modificados:**
  - `Assets/Scripts/Networking/RaidParticipantConnectivity.cs`: `NetworkBehaviour` para registrar conectividad y consumo de presupuesto en el State Authority.
  - `Assets/Scripts/Networking/DisconnectedParticipantRegistry.cs`: Colaborador puro que administra la lista de objetos desconectados/retenidos.
  - `Assets/Scripts/Networking/NetworkSpawnManager.cs`: Integración de la retención en `OnPlayerLeft` (usando el clasificador de la Etapa 2); refactor de `_retainedDownedParticipants` hacia el nuevo `_disconnectedRegistry`; y re-evaluación del fallback sin decisor.
- **Fuera de la etapa y por qué:** Reconexión y UI están fuera; esta etapa solo prepara la infraestructura del Host para no destruir al participante cuando pierde conexión.
- **Estado de AC:** "Retener participante, avatar, estado, objetos" -> Cumplido. "Plazos no definidos = decisiones pendientes" -> Cumplido (presupuesto delegable).
- **Justificación P-2 (Fallback sin decisor):** El hook elegido fue el final del método `OnPlayerLeft`. La única forma de que un jugador retenido "pierda" a su decisor (compañero) de la sesión activa es que el decisor abandone o se desconecte. Revisar la lista de retenidos *inmediatamente después* del `OnPlayerLeft` es el momento exacto y determinístico donde la condición de retención (tener decisor) debe revalidarse. Si falla, se finaliza el perfil definitivamente (comportamiento legacy).
- **Ejecución:** Revisión estática del diff. No ejecuté Play Mode.

## 2. Instrucciones de implementación manual en Unity
1. **NetworkRaidParticipant Prefab:**
   - Abrí el prefab `Assets/Prefabs/NetworkRaidParticipant.prefab`.
   - Agregale el componente `RaidParticipantConnectivity`.
   - Asegurate de que Unity/Fusion regenere el código de Networked Behaviours si estás usando un weaver antiguo, o que no haya errores de compilación de Fusion.
2. **Reconnect Budget Config:**
   - En tu carpeta de configuración (por ejemplo, `Assets/Settings/` o donde tengas configuraciones similares), creá un nuevo asset de tipo `ReconnectBudgetConfig` (click derecho -> Create -> ... si tenés el menú, o bien crealo programáticamente o instancialo). (NOTA: En la Etapa 2 lo llamamos inmutable pero requiere el menú de creación. Si te falta el `[CreateAssetMenu]`, podés agregarlo).
   - Asignale un valor de prueba (ej. `300` segundos = 5 minutos).
   - *Este asset aún no se inyecta directamente porque la inyección depende de Etapa 4, pero tenelo preparado.*

*(Los tests de composición de prefabs de la participación pueden fallar si validaban el número exacto de componentes, actualizalos si aplica).*

## 3. Instrucciones de validación manual
- **Compilación y tests:** Compilá el proyecto. Si tenías tests que esperaban que un participante Activo siempre muriera al desconectarse, van a fallar (deben actualizarse para validar la retención).
- **Host + Cliente en Play Mode:**
  1. Corré el Host (build) y unite con el Cliente (Editor).
  2. En la Dungeon, **matá o cerrá** el Editor (caída abrupta).
  3. Observá el log del Host y el juego: el personaje del Cliente no debe desaparecer. En la consola del Host debe aparecer el log `[RAID-DISCONNECT] Participant '{profileId}' disconnected. Objects retained...`.
  4. Si tuvieras un tercer cliente y este abandona dejándolo sin decisor, verás el log de *fallback*.

## 4. Información para el commit
**Título:** feat: retain active disconnected participants in host
**Descripción:** 
Implements host-side retention for unexpectedly disconnected participants (US-52). Replaces terminal disconnection with input authority revocation and budget consumption. Re-evaluates retained participants when a peer leaves to ensure deciders remain available (fallback).
**Archivos excluidos:** Ninguno.

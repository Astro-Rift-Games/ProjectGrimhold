# Walkthrough: US-59 (#527) Autenticar la Identidad de Town y la Admisión a Raid

## Resumen Ejecutivo

Se implementó el 100% de la **User Story 59 (#527)** y sus 4 tareas hijas de seguridad:
- **SEC-59.1 (#540):** Contrato formal, emisión y verificación criptográfica de tokens (`TownSessionTicket`, `AuthoritativeRaidAdmissionTicket`) sin exponer secretos del emisor en builds de Unity.
- **SEC-59.2 (#541):** Validación estricta de identidad en Town (`TownSenderIdentityValidator`, `SocialPlayerIdentity`, `TownPartyDirectory`, `TownMerchantNetworkController`, `TownRaidPreparationDirectory`), bloqueando la suplantación de `ProfileId`.
- **SEC-59.3 (#542):** Admisión autoritativa a Raid ligada a `pendingReservation` en backend (`RaidAdmissionRules`, `NetworkSpawnManager`, `AuthoritativeRaidAdmissionTicketCodec`), asegurando que atributos, nivel, XP, habilidades e inventario provengan exclusivamente del snapshot verificado por el backend, descartando valores fabricados por clientes.
- **SEC-59.4 (#543):** Protección de reconexión y Host Migration con prueba de reserva (`ReservationId` continuity check), impidiendo el secuestro de avatares desconectados, doble consumo de reservas o sobrescritura de snapshots restaurados por fresh spawns.

---

## Cambios Realizados

### 1. Backend (Node.js / Express / MongoDB)
- **`backend/src/services/SessionTicketService.js`:**
  - `issueTownTicket(accountId)` & `verifyTownTicket(ticketString)`: Generación y verificación de HMAC-SHA256 para Town (TTL 30 min, scope `Lobby-Town`).
  - `issueRaidAdmissionTicket(accountId, raidCode, reservationId)` & `verifyRaidAdmissionTicket(ticketString, expectedRaidCode)`: Snapshot autoritativo de `characterAttributes`, `level`, `experience`, `preparedAbilities`, `items` y `preparedEquipment` ligado a la reserva activa en base de datos.
- **`backend/src/models/Character.js` & `backend/src/services/InventoryService.js`:**
  - Persistencia de `preparedAbilities` en el esquema de `Character` y `pendingReservation`.
- **`backend/src/routes/character.routes.js`:**
  - `POST /character/me/session-ticket/town` (emisión de ticket de Town)
  - `POST /character/verify-session-ticket` (verificación de ticket de Town)
  - `POST /character/me/raid/admission-ticket` (emisión de ticket de Raid)
  - `POST /character/raid/verify-admission` (verificación de ticket de Raid)

### 2. Contratos y Clientes en Unity (C#)
- **`SessionTicketDataTransferObjects.cs`:** DTOs para comunicación con endpoints de tickets de sesión y snapshots autoritativos.
- **`CharacterClient.cs`:** Métodos asíncronos para solicitar y verificar tickets de Town y admisión a Raid.
- **`TownSessionTicket.cs` & `TownSessionTicketCodec.cs`:** Representación estructurada y codec base64url del ticket de Town.
- **`ITownSessionTicketVerifier.cs` & `TownSessionTicketVerifier.cs`:** Interfaz e implementaciones (`BackendTownSessionTicketVerifier`, `MockTownSessionTicketVerifier`).
- **`TownSenderIdentityValidator.cs`:** Validador estático síncrono para State Authority en RPCs de Town (valida ticket presente, parseable, no expirado, sesión `Lobby-Town` y coincidencia de `ProfileId`).
- **`SocialPlayerIdentity.cs`:** Añadido campo replicado `[Networked] public NetworkString<_512> SessionTicket` publicado durante `Spawned()`.
- **`TownPartyDirectory.cs`, `TownMerchantNetworkController.cs`, `TownRaidPreparationDirectory.cs`:** Actualizados para usar `TownSenderIdentityValidator`, rechazando llamadas de suplantación antes de mutar listas o disolver parties.
- **`AuthoritativeRaidAdmissionTicket.cs` & `AuthoritativeRaidAdmissionTicketCodec.cs`:** Struct inmutable y codec del ticket de admisión con método `TryToRaidAdmissionData` para hidratación canónica de estado.
- **`IRaidAdmissionVerifier.cs` & `RaidAdmissionVerifier.cs`:** Interfaz e implementaciones (`BackendRaidAdmissionVerifier`, `LocalDeterministicRaidAdmissionVerifier`).
- **`RaidAdmissionRules.cs`:** Sobrecarga `IsAdmitted` para `AuthoritativeRaidAdmissionTicket`.
- **`NetworkSpawnManager.cs`:**
  - `TryHandleRemoteAdmissionPayload`: Decodifica y valida `AuthoritativeRaidAdmissionTicket`. Al verificar el ticket, hidrata atributos, nivel, XP, habilidades e inventario exclusivamente del snapshot canónico del backend.
  - `TryValidateEarlyHostMigrationToken`: Añadido soporte para `AuthoritativeRaidAdmissionTicketCodec`.
  - `TryHandleReconnectArrival`: Incorporada validación obligatoria de coincidencia de `ReservationId` con el participante retenido.
- **`RaidReconnectPayload.cs`, `RaidReconnectTicket.cs`, `RaidReconnectPayloadCodec.cs`, `RaidReconnectTicketStore.cs`:** Añadido soporte persistente y en handshake para `ReservationId` como prueba de continuidad de sesión.
- **`SessionConnectionCoordinator.cs`:** Almacenamiento del `ReservationId` activo al generar el `RaidReconnectTicket`.

### 3. Documentación de Arquitectura
- **`RemoteIdentityAndAuthArchitecture.md`:** Documentado el vínculo de identidad autoritativa y verificación de tickets en backend y cliente.
- **`LobbyAndSessionTransitionArchitecture.md`:** Documentada la protección contra suplantación en Town y la admisión autoritativa a Raid.

---

## Resultados de Pruebas

### 1. Tests de Backend (Node.js)
- **Unit Tests (`npm test`):**
  - `tests/session.ticket.test.js`: 12/12 pasados.
  - Total: **110 tests pasados**, 0 fallos.
- **Integration Tests (`npm run test:integration`):**
  - Total: **54 tests pasados**, 0 fallos.
- **Concurrency Tests (`npm run test:concurrency`):**
  - Total: **7 tests pasados**, 0 fallos.

### 2. Tests de Unity EditMode (C# NUnit)
- **`TownIdentitySecurityTests` (8 tests):**
  - `Attacker_WithNoSessionTicket_IsRejected`: PASSED
  - `Attacker_DeclaringVictimProfile_WithOwnTicket_IsRejectedAsSpoofing`: PASSED
  - `Attacker_WithExpiredTicket_IsRejected`: PASSED
  - `Attacker_WithWrongSessionScopeTicket_IsRejected`: PASSED
  - `Attacker_WithMalformedTicket_IsRejected`: PASSED
  - `Attacker_WithForgedSignature_RejectedByVerifier`: PASSED
  - `LegitimatePlayer_WithAuthenticTicket_ResolvesSuccessfully`: PASSED
  - `LegitimatePlayer_WithVerifierValidation_ResolvesSuccessfully`: PASSED
- **`RaidAdmissionSecurityTests` (6 tests):**
  - `Client_AuthoritativeTicket_ValidCohortAndSnapshot_ResolvesCanonicalAdmission`: PASSED
  - `Client_MismatchedRaidCode_IsRejectedByCohortRules`: PASSED
  - `Outsider_NotInManifest_IsRejectedByCohortRules`: PASSED
  - `Verifier_RejectsForgedOrUnregisteredTicket`: PASSED
  - `Verifier_ValidatesRegisteredTicketAndConfirmsAuthoritativeState`: PASSED
  - `Verifier_RejectsTicketWithMismatchedRaidCode`: PASSED
- **`RaidContinuitySecurityTests` (4 tests):**
  - `ReconnectPayload_CodecRoundtrip_PreservesReservationProof`: PASSED
  - `Impostor_WithMismatchedReservationId_DiffersFromLegitimatePayload`: PASSED
  - `ReconnectTicket_StoreRoundtrip_PreservesReservationProof`: PASSED
  - `HostMigration_Token_AuthoritativeAdmissionTicketDecodesCleanly`: PASSED
- **Pruebas de regresión (`RaidReconnectClientFlowTests`, `RaidAdmissionRulesTests`, `RaidAdmissionDataCodecTests`):**
  - `RaidReconnectClientFlowTests`: **11/11 pasados**.
  - `RaidAdmissionRulesTests` & `RaidAdmissionDataCodecTests`: **21/21 pasados**.

---

## Guía de Verificación Manual

### Escenario A: Seguridad de Identidad en Town
1. Iniciar el backend (`npm run dev` en `backend/`).
2. Abrir Unity y entrar en PlayMode en la escena `MainMenu`.
3. Iniciar sesión con un usuario legítimo -> entrar a Town.
4. En Town, unirte a una party o abrir la tienda del mercader.
5. Mediante depuración o un segundo cliente con ProfileId modificado que intente enviar `RPC_RequestLeaveParty` en nombre del primer jugador:
   - El servidor de Town detecta que el `SessionTicket` no coincide con el `ProfileId` declarado.
   - La llamada es rechazada con un warning `[SECURITY]` en la consola de Unity y la party no se disuelve.

### Escenario B: Admisión Autoritativa a Raid
1. En Town, equipar ítems y atributos (ej. Fuerza 5).
2. Crear sala de Raid y reservar loadout.
3. Al iniciar la Raid, el cliente recibe el `AuthoritativeRaidAdmissionTicket` firmado por el backend con los atributos y equipo exactos de la reserva.
4. Al ingresar a la Raid, el Host valida el ticket e hidrata al avatar con los atributos autorizados (Fuerza 5).
5. Cualquier intento de enviar un payload con Fuerza 999 o ítems no reservados es descartado o rechazado por el Host con desconexión del cliente hostil.

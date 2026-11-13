#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEngine;

namespace Grimhold.Tests.Backend
{
    /// <summary>
    /// FIX-38: a failed Town session ticket request must surface as a login error and leave the
    /// same retryable hydration-failure state as the other hydration steps.
    /// </summary>
    [TestFixture]
    public class LoginFlowControllerTownTicketTests
    {
        private const string Username = "alice";
        private const string Token = "token-1";
        private const string CharacterId = "char-1";

        private GameObject _controllerObject;
        private GameObject _authObject;
        private LoginFlowController _controller;
        private ApplicationAuthContext _authContext;
        private FakeLoginFlowBackend _backend;

        [SetUp]
        public void SetUp()
        {
            LocalProfileProvider.ClearRemoteCharacterId();
            _backend = new FakeLoginFlowBackend();
            _authObject = new GameObject("LoginFlowTest_Auth");
            _authContext = _authObject.AddComponent<ApplicationAuthContext>();
            _controllerObject = new GameObject("LoginFlowTest_Controller");
            _controller = _controllerObject.AddComponent<LoginFlowController>();
            // A null configuration keeps ApplicationAuthContext from creating real HTTP verifiers.
            _controller.ConfigureForTesting(_backend, null, _authContext);
        }

        [TearDown]
        public void TearDown()
        {
            LocalProfileProvider.ClearRemoteCharacterId();
            if (_controllerObject != null) Object.DestroyImmediate(_controllerObject);
            if (_authObject != null) Object.DestroyImmediate(_authObject);
        }

        [TestCase(BackendErrorUtility.NetworkError, LoginFlowStatus.NetworkError)]
        [TestCase(BackendErrorUtility.Timeout, LoginFlowStatus.NetworkError)]
        [TestCase("INTERNAL_SERVER_ERROR", LoginFlowStatus.HydrationFailed)]
        public async Task TicketRequestFails_LoginFailsWithRetryableHydrationState(string errorCode, LoginFlowStatus expectedStatus)
        {
            _backend.TicketResult = (false, default, new BackendError { error = errorCode });

            LoginFlowResult result = await _controller.ExecuteLoginAsync(Username, "pw");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(expectedStatus, result.Status);
            Assert.IsNotEmpty(result.ErrorMessage);
            Assert.IsFalse(_authContext.ActiveTownTicket.IsValid);
            Assert.IsTrue(_controller.HasHydrationFailed);
            Assert.AreEqual(Username, _controller.PendingUsername);
            AssertNoPartialIdentity();
        }

        [Test]
        public async Task TicketRequestFails_RetryHydrationSucceedsOnceTicketIsAvailable()
        {
            _backend.TicketResult = (false, default, new BackendError { error = BackendErrorUtility.NetworkError });
            await _controller.ExecuteLoginAsync(Username, "pw");

            _backend.TicketResult = (true, ValidTicket(), default);
            LoginFlowResult retry = await _controller.RetryHydrationAsync();

            Assert.IsTrue(retry.IsSuccess, retry.ErrorMessage);
            Assert.IsFalse(_controller.HasHydrationFailed);
            Assert.AreEqual("ticket-raw", _authContext.ActiveTownTicket.RawTicket);
        }

        [Test]
        public async Task TicketResponseWithoutTicket_LoginFailsWithRetryableHydrationState()
        {
            _backend.TicketResult = (true, new TownSessionTicketResponse(), default);

            LoginFlowResult result = await _controller.ExecuteLoginAsync(Username, "pw");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(LoginFlowStatus.HydrationFailed, result.Status);
            Assert.IsNotEmpty(result.ErrorMessage);
            Assert.IsTrue(_controller.HasHydrationFailed);
            AssertNoPartialIdentity();
        }

        [Test]
        public async Task TicketRequestUnauthorized_FailsAuthAndClearsState()
        {
            _backend.TicketResult = (false, default, new BackendError { error = BackendErrorUtility.Unauthorized });

            LoginFlowResult result = await _controller.ExecuteLoginAsync(Username, "pw");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(LoginFlowStatus.AuthFailed, result.Status);
            Assert.IsNotEmpty(result.ErrorMessage);
            Assert.IsFalse(_controller.HasHydrationFailed);
            Assert.IsNull(_controller.PendingUsername);
            AssertNoPartialIdentity();
        }

        [Test]
        public async Task TicketRequestSucceeds_LoginSucceedsAndSetsTicket()
        {
            _backend.TicketResult = (true, ValidTicket(), default);

            LoginFlowResult result = await _controller.ExecuteLoginAsync(Username, "pw");

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsTrue(_authContext.IsAuthenticated);
            Assert.IsTrue(_authContext.ActiveTownTicket.IsValid);
            Assert.AreEqual("ticket-raw", _authContext.ActiveTownTicket.RawTicket);
            Assert.AreEqual(CharacterId, LocalProfileProvider.GetOrCreateLocalProfile().Value);
            Assert.AreEqual(1, _backend.StashInitializations);
            Assert.IsFalse(_controller.HasHydrationFailed);
            Assert.IsNull(_controller.PendingUsername);
        }

        private void AssertNoPartialIdentity()
        {
            Assert.IsFalse(LocalProfileProvider.GetOrCreateLocalProfile().IsValid, "Remote character id must not be injected.");
            Assert.IsFalse(_authContext.IsAuthenticated, "Auth context must not be initialized.");
            Assert.AreEqual(0, _backend.StashInitializations, "Stash must not be initialized.");
        }

        private static TownSessionTicketResponse ValidTicket() => new TownSessionTicketResponse
        {
            ticket = "ticket-raw",
            characterId = CharacterId,
            characterName = "Hero",
            sessionName = "town-1",
            expiresAt = 1
        };

        private sealed class FakeLoginFlowBackend : ILoginFlowBackend
        {
            public (bool success, TownSessionTicketResponse data, BackendError error) TicketResult;
            public int StashInitializations { get; private set; }

            public Task<(bool success, LoginResult result, BackendError error)> PostLoginAsync(
                BackendConfiguration config, string username, string password)
                => Task.FromResult<(bool, LoginResult, BackendError)>((true, new LoginResult { token = Token }, default));

            public Task<(bool success, CharacterData data, BackendError error)> GetCharacterAsync(
                BackendConfiguration config, string token)
                => Task.FromResult<(bool, CharacterData, BackendError)>(
                    (true, new CharacterData { characterId = CharacterId, name = "Hero" }, default));

            public Task<(bool success, CharacterProfileData data, BackendError error)> GetProfileAsync(
                BackendConfiguration config, string token)
                => Task.FromResult<(bool, CharacterProfileData, BackendError)>(
                    (true, new CharacterProfileData { characterId = CharacterId }, default));

            public Task<(bool success, InventoryData data, BackendError error)> GetInventoryAsync(
                BackendConfiguration config, string token)
                => Task.FromResult<(bool, InventoryData, BackendError)>((true, new InventoryData { revision = 1 }, default));

            public Task<(bool success, ProgressionData data, BackendError error)> GetProgressionAsync(
                BackendConfiguration config, string token)
                => Task.FromResult<(bool, ProgressionData, BackendError)>((true, new ProgressionData { revision = 1 }, default));

            public Task<(bool success, TownSessionTicketResponse data, BackendError error)> GetTownSessionTicketAsync(
                BackendConfiguration config, string token)
                => Task.FromResult(TicketResult);

            public bool InitializeStash(ProfileId characterId, InventoryData inventory, ProgressionData progression)
            {
                StashInitializations++;
                return true;
            }
        }
    }
}
#endif

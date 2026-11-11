#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Grimhold.Backend;
using NUnit.Framework;
using UnityEngine;

namespace Grimhold.Tests.Backend
{
    [TestFixture]
    [Category("SEC60")]
    public class AuthenticationSecurityTests
    {
        [TearDown]
        public void TearDown()
        {
            LocalProfileProvider.ClearRemoteCharacterId();
            if (ApplicationAuthContext.Instance != null)
            {
                Object.DestroyImmediate(ApplicationAuthContext.Instance.gameObject);
            }
        }

        [Test]
        public void BackendErrorUtility_TooManyRequests_IsCorrectConstant()
        {
            Assert.AreEqual("TOO_MANY_REQUESTS", BackendErrorUtility.TooManyRequests);
            Assert.IsFalse(BackendErrorUtility.IsTransportFailure(BackendErrorUtility.TooManyRequests));
        }

        [Test]
        public void BackendErrorUtility_Unauthorized_IsCorrectConstant()
        {
            Assert.AreEqual("UNAUTHORIZED", BackendErrorUtility.Unauthorized);
            Assert.IsFalse(BackendErrorUtility.IsTransportFailure(BackendErrorUtility.Unauthorized));
        }

        [Test]
        public void ApplicationAuthContext_Clear_ResetsAllSessionState()
        {
            var go = new GameObject("TestAuthContext");
            var context = go.AddComponent<ApplicationAuthContext>();

            var charData = new CharacterData
            {
                characterId = "char-sec-1",
                name = "SecurityHero"
            };
            var profileData = new CharacterProfileData
            {
                profile = new CharacterProfileValues()
            };

            context.Initialize("jwt-token-xyz", charData, profileData);

            Assert.IsTrue(context.IsAuthenticated);
            Assert.AreEqual("jwt-token-xyz", context.Token);
            Assert.AreEqual("char-sec-1", context.CharacterId);
            Assert.AreEqual("SecurityHero", context.CharacterName);

            context.Clear();

            Assert.IsFalse(context.IsAuthenticated);
            Assert.IsNull(context.Token);
            Assert.IsNull(context.CharacterId);
            Assert.IsNull(context.CharacterName);
            Assert.AreEqual(default(TownSessionTicket), context.ActiveTownTicket);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void LocalProfileProvider_ClearRemoteCharacterId_ClearsCurrentProfile()
        {
            var testProfileId = new ProfileId("char-sec-2");
            LocalProfileProvider.SetRemoteCharacterId(testProfileId);

            Assert.AreEqual("char-sec-2", LocalProfileProvider.GetOrCreateLocalProfile().Value);

            LocalProfileProvider.ClearRemoteCharacterId();

            Assert.IsTrue(string.IsNullOrEmpty(LocalProfileProvider.GetOrCreateLocalProfile().Value));
        }

        [Test]
        public void LoginFlowResult_FailureWithRateLimit_PreservesMessageAndAuthFailedStatus()
        {
            string rateLimitMsg = "Too many requests. Please wait a moment and try again.";
            var result = LoginFlowResult.Failure(LoginFlowStatus.AuthFailed, rateLimitMsg);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(LoginFlowStatus.AuthFailed, result.Status);
            Assert.AreEqual(rateLimitMsg, result.ErrorMessage);
        }

        [Test]
        public void ApplicationAuthContext_HandleUnauthorizedSession_ClearsAuthAndProfileAndRaisesEvent()
        {
            var go = new GameObject("TestAuthContext401");
            var context = go.AddComponent<ApplicationAuthContext>();
            var backendConfig = ScriptableObject.CreateInstance<BackendConfiguration>();

            var charData = new CharacterData
            {
                characterId = "char-sec-401",
                name = "ExpiredHero"
            };
            var profileData = new CharacterProfileData
            {
                profile = new CharacterProfileValues()
            };

            LocalProfileProvider.SetRemoteCharacterId(new ProfileId("char-sec-401"));
            context.Initialize("jwt-token-401", charData, profileData, backendConfig);

            Assert.IsTrue(context.IsAuthenticated);
            Assert.IsNotNull(context.TownTicketVerifier);
            Assert.IsNotNull(context.RaidAdmissionVerifier);

            bool invalidatedEventFired = false;
            context.SessionInvalidated += () => invalidatedEventFired = true;

            context.HandleUnauthorizedSession();

            Assert.IsTrue(invalidatedEventFired, "SessionInvalidated event must fire when 401 session invalidation occurs.");
            Assert.IsFalse(context.IsAuthenticated);
            Assert.IsNull(context.Token);
            Assert.IsNull(context.CharacterId);
            Assert.IsNull(context.TownTicketVerifier);
            Assert.IsNull(context.RaidAdmissionVerifier);
            Assert.IsTrue(string.IsNullOrEmpty(LocalProfileProvider.GetOrCreateLocalProfile().Value));

            Object.DestroyImmediate(backendConfig);
            Object.DestroyImmediate(go);
        }
    }
}
#endif

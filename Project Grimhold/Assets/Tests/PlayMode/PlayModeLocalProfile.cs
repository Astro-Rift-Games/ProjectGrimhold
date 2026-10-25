#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>
    /// Composes one in-memory local profile behind an <see cref="ApplicationStashContext"/> and binds the
    /// same <see cref="ProfileId"/> to a runner's <see cref="LocalPlayerJoinContext"/>, which is what
    /// <see cref="LocalPlayerHudBinder"/> requires to create Quick Slots. The runtime bootstrapper may keep its
    /// own context alive across play sessions, so existing ones are suspended while this profile exists and
    /// restored on dispose.
    /// </summary>
    internal sealed class PlayModeLocalProfile : IDisposable
    {
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";

        public const string DefaultProfileValue = "5b5b5b5b5b5b5b5b5b5b5b5b5b5b5b5b";

        private readonly GameObject _contextObject;
        private readonly ApplicationStashContext[] _suspendedContexts;

        private PlayModeLocalProfile(
            ProfileId profileId,
            GameObject contextObject,
            ApplicationStashContext[] suspendedContexts)
        {
            ProfileId = profileId;
            _contextObject = contextObject;
            _suspendedContexts = suspendedContexts;
        }

        public ProfileId ProfileId { get; }

        public static PlayModeLocalProfile Create(string profileValue = DefaultProfileValue)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);

            var profileId = new ProfileId(profileValue);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profileId, catalog), Is.True);
            var store = new LocalProfileStore(repository, profileId, catalog);

            ApplicationStashContext[] suspended = Object.FindObjectsByType<ApplicationStashContext>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (ApplicationStashContext existing in suspended)
            {
                existing.gameObject.SetActive(false);
            }

            var contextObject = new GameObject("ApplicationStashContext (PlayMode test profile)");
            var context = contextObject.AddComponent<ApplicationStashContext>();
            var stashService = contextObject.AddComponent<InMemoryPlayerStashService>();
            stashService.Initialize(store);
            var loadoutService = contextObject.AddComponent<InMemoryPlayerLoadoutService>();
            loadoutService.Initialize(store);
            context.Initialize(store, stashService, loadoutService, null, null);

            return new PlayModeLocalProfile(profileId, contextObject, suspended);
        }

        /// <summary>Binds this profile as the local player's join identity; call before the avatar spawns.</summary>
        public void BindTo(LocalPlayerJoinContext joinContext)
        {
            Assert.That(joinContext, Is.Not.Null);
            joinContext.Initialize(new PlayerJoinData(ProfileId));
        }

        public void Dispose()
        {
            if (_contextObject != null)
            {
                Object.DestroyImmediate(_contextObject);
            }

            foreach (ApplicationStashContext existing in _suspendedContexts)
            {
                if (existing != null)
                {
                    existing.gameObject.SetActive(true);
                }
            }
        }
    }
}
#endif

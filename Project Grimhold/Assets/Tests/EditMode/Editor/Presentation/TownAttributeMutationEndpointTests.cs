#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Presentation
{
    public sealed class TownAttributeMutationEndpointTests
    {
        [Test]
        public void ReadyBlocksWithoutCommit_NotReadyAssigns_AndAssignsAgainAfterUnlock()
        {
            var profile = new ProfileId("94949494949494949494949494949494");
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profile, catalog), Is.True);
            var store = new LocalProfileStore(repository, profile, lootCatalog: catalog);
            Assert.That(CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 3, out var initial), Is.True);
            store.ForceCharacterAttributeState(initial);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;
            bool canMutate = false;
            var endpoint = new TownAttributeMutationEndpoint(store, () => canMutate);

            Assert.That(endpoint.TryAssign(CharacterAttribute.Vitality, out _), Is.False);
            Assert.That(store.TryGetCharacterAttributeState(out var blocked), Is.True);
            Assert.That(blocked.Vitality, Is.EqualTo(5));
            Assert.That(blocked.AvailablePoints, Is.EqualTo(3));
            Assert.That(commits, Is.Zero);

            canMutate = true;
            Assert.That(endpoint.TryAssign(CharacterAttribute.Vitality, out _), Is.True);
            canMutate = false;
            Assert.That(endpoint.TryAssign(CharacterAttribute.Vitality, out _), Is.False);
            Assert.That(commits, Is.EqualTo(1));
            canMutate = true;
            Assert.That(endpoint.TryAssign(CharacterAttribute.Vitality, out _), Is.True);
            Assert.That(store.TryGetCharacterAttributeState(out var assigned), Is.True);
            Assert.That(assigned.Vitality, Is.EqualTo(7));
            Assert.That(assigned.AvailablePoints, Is.EqualTo(1));
            Assert.That(commits, Is.EqualTo(2));
        }
    }
}
#endif

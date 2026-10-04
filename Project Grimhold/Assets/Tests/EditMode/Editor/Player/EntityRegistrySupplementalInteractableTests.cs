using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Player
{
    /// <summary>
    /// A player avatar already owns the primary interactable slot (its corpse loot container), so the
    /// revive target registers as a supplemental interactable resolved through one interaction handler.
    /// </summary>
    public sealed class EntityRegistrySupplementalInteractableTests
    {
        private static readonly EntityId Id = new EntityId(77);

        private GameObject _holder;
        private EntityRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("SupplementalInteractableRegistry");
            _registry = _holder.AddComponent<EntityRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
        }

        [Test]
        public void Handler_WithoutSupplementals_ResolvesThePrimaryInteractable()
        {
            var primary = new StubInteractable(Id, canInteract: false);
            _registry.TryRegisterInteractable(Id, primary);

            Assert.That(_registry.TryGetInteractionHandler(Id, out IInteractable handler), Is.True);

            Assert.That(handler, Is.SameAs(primary));
        }

        [Test]
        public void Handler_WithNothingRegistered_Fails()
        {
            Assert.That(_registry.TryGetInteractionHandler(Id, out _), Is.False);
        }

        [Test]
        public void Supplemental_DoesNotReplaceOrChangeThePrimarySlot()
        {
            var primary = new StubInteractable(Id, canInteract: false);
            var supplemental = new StubInteractable(Id, canInteract: true);
            _registry.TryRegisterInteractable(Id, primary);

            Assert.That(_registry.TryRegisterSupplementalInteractable(Id, supplemental), Is.True);

            Assert.That(_registry.TryGetInteractable(Id, out IInteractable resolved), Is.True);
            Assert.That(resolved, Is.SameAs(primary), "Presenters keep resolving the corpse container.");
        }

        [Test]
        public void Handler_ReachesTheSupplementalWhenThePrimaryCannotInteract()
        {
            var primary = new StubInteractable(Id, canInteract: false);
            var supplemental = new StubInteractable(Id, canInteract: true);
            _registry.TryRegisterInteractable(Id, primary);
            _registry.TryRegisterSupplementalInteractable(Id, supplemental);
            _registry.TryGetInteractionHandler(Id, out IInteractable handler);
            var request = new InteractionRequest(new EntityId(5), Id, 1);

            Assert.That(handler.Id, Is.EqualTo(Id));
            Assert.That(handler.CanInteract(request), Is.True);
            InteractionResult result = handler.Interact(request);

            Assert.That(result.Success, Is.True);
            Assert.That(primary.InteractCount, Is.Zero);
            Assert.That(supplemental.InteractCount, Is.EqualTo(1));
        }

        [Test]
        public void Handler_PrefersThePrimaryAndRejectsWhenNoneCanInteract()
        {
            var primary = new StubInteractable(Id, canInteract: true);
            var supplemental = new StubInteractable(Id, canInteract: true);
            _registry.TryRegisterInteractable(Id, primary);
            _registry.TryRegisterSupplementalInteractable(Id, supplemental);
            _registry.TryGetInteractionHandler(Id, out IInteractable handler);
            var request = new InteractionRequest(new EntityId(5), Id, 1);

            handler.Interact(request);

            Assert.That(primary.InteractCount, Is.EqualTo(1));
            Assert.That(supplemental.InteractCount, Is.Zero);

            primary.CanInteractValue = false;
            supplemental.CanInteractValue = false;

            Assert.That(handler.CanInteract(request), Is.False);
            Assert.That(handler.Interact(request).Success, Is.False);
        }

        [Test]
        public void Handler_ResolvesASupplementalWithoutAnyPrimary()
        {
            var supplemental = new StubInteractable(Id, canInteract: true);
            _registry.TryRegisterSupplementalInteractable(Id, supplemental);

            Assert.That(_registry.TryGetInteractionHandler(Id, out IInteractable handler), Is.True);
            Assert.That(handler.CanInteract(new InteractionRequest(new EntityId(5), Id, 1)), Is.True);
            Assert.That(_registry.TryGetInteractable(Id, out _), Is.False);
        }

        [Test]
        public void Supplemental_RejectsNullMismatchedIdAndIsIdempotent()
        {
            var supplemental = new StubInteractable(Id, canInteract: true);

            Assert.That(_registry.TryRegisterSupplementalInteractable(Id, null), Is.False);
            Assert.That(_registry.TryRegisterSupplementalInteractable(new EntityId(0), supplemental), Is.False);
            Assert.That(_registry.TryRegisterSupplementalInteractable(new EntityId(78), supplemental), Is.False);
            Assert.That(_registry.TryRegisterSupplementalInteractable(Id, supplemental), Is.True);
            Assert.That(_registry.TryRegisterSupplementalInteractable(Id, supplemental), Is.True);
        }

        [Test]
        public void Supplemental_CanOnlyBeUnregisteredByItsOwnInstance()
        {
            var primary = new StubInteractable(Id, canInteract: false);
            var supplemental = new StubInteractable(Id, canInteract: true);
            var other = new StubInteractable(Id, canInteract: true);
            _registry.TryRegisterInteractable(Id, primary);
            _registry.TryRegisterSupplementalInteractable(Id, supplemental);

            Assert.That(_registry.TryUnregisterSupplementalInteractable(Id, other), Is.False);
            Assert.That(_registry.TryUnregisterSupplementalInteractable(Id, supplemental), Is.True);

            _registry.TryGetInteractionHandler(Id, out IInteractable handler);
            Assert.That(handler, Is.SameAs(primary));
            Assert.That(_registry.TryUnregisterSupplementalInteractable(Id, supplemental), Is.False);
        }

        [Test]
        public void ClearForRaidClosure_RemovesSupplementals()
        {
            _registry.TryRegisterSupplementalInteractable(Id, new StubInteractable(Id, canInteract: true));

            _registry.ClearForRaidClosure();

            Assert.That(_registry.TryGetInteractionHandler(Id, out _), Is.False);
        }

        private sealed class StubInteractable : IInteractable
        {
            public StubInteractable(EntityId id, bool canInteract)
            {
                Id = id;
                CanInteractValue = canInteract;
            }

            public EntityId Id { get; }
            public bool CanInteractValue { get; set; }
            public int InteractCount { get; private set; }

            public bool CanInteract(in InteractionRequest request) => CanInteractValue;

            public InteractionResult Interact(in InteractionRequest request)
            {
                InteractCount++;
                return InteractionResult.Succeeded();
            }
        }
    }
}

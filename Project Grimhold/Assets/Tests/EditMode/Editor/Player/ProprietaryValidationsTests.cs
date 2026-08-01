using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Editor.Player
{
    [TestFixture]
    public sealed class ProprietaryValidationsTests
    {
        private GameObject _playerObject;
        private PlayerCharacter _character;
        private PlayerExtractionController _extractionController;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("TestPlayer");
            _character = _playerObject.AddComponent<PlayerCharacter>();
            _extractionController = _playerObject.AddComponent<PlayerExtractionController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null)
            {
                Object.DestroyImmediate(_playerObject);
            }
        }

        [Test]
        public void CanReceiveDamage_BeforeFusionObject_ReturnsBaseValue()
        {
            // Object is null before Fusion spawn, so CanReceiveDamage delegates to base.CanReceiveDamage
            Assert.IsTrue(_character.CanReceiveDamage);
        }

        [Test]
        public void LootTransferFailureReason_ContainsPlayerUnavailable()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(LootTransferFailureReason), LootTransferFailureReason.PlayerUnavailable));
        }
    }
}

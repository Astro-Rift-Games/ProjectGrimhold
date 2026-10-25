#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewFrameCacheTests
    {
        private readonly List<Texture2D> _created = new List<Texture2D>();
        private AttackPreviewFrameCache _cache;
        private double _clock;
        private readonly List<float> _times = new List<float>();
        private static AttackPreviewCombination Cell(int facing = 0) => new AttackPreviewCombination(
            AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, facing);
        private Texture2D Render(AttackPreviewCombination cell, float seconds)
        {
            _clock += 5;
            _times.Add(seconds);
            var texture = new Texture2D(2, 2);
            _created.Add(texture);
            return texture;
        }
        [SetUp]
        public void SetUp()
        {
            // NUnit reuses this fixture: destroyed Unity objects remain in managed lists.
            _created.Clear();
            _times.Clear();
            _clock = 0;
            _cache = null;
        }
        [TearDown]
        public void TearDown()
        {
            try { _cache?.Dispose(); }
            finally { foreach (var texture in _created) if (texture != null) Object.DestroyImmediate(texture); }
        }
        [Test]
        public void Tick_StopsAtBudgetAndResumesWithoutDuplicateSamples()
        {
            _cache = new AttackPreviewFrameCache(Render, () => _clock, sampleCount: 4, tailSeconds: 0.1f);
            var cell = Cell();
            _cache.Request(cell, 0.9f);
            _cache.Request(cell, 0.9f);
            Assert.That(_created, Is.Empty, "Request must be lazy.");
            _cache.Tick(8);
            Assert.That(_times.Count, Is.EqualTo(2), "A non-preemptible render may finish beyond budget; no third render may start.");
            Assert.That(_cache.PendingCount, Is.EqualTo(2));
            _cache.Tick(8);
            Assert.That(_times, Is.EqualTo(new[] { 0f, 1f / 3, 2f / 3, 1f }).Within(0.00001f));
            Assert.That(_cache.PendingCount, Is.Zero);
            Assert.That(_cache.Get(cell.Id, 1f), Is.SameAs(_created[3]));
            _cache.Tick(8);
            Assert.That(_created.Count, Is.EqualTo(4));
        }
        [Test]
        public void Lru_EvictsLeastRecentlyReadTextureAndDestroysIt()
        {
            _cache = new AttackPreviewFrameCache(Render, () => _clock, sampleCount: 2, maxTextures: 3);
            var a = Cell(); var b = Cell(1);
            _cache.Request(a, 1);
            _cache.Tick(100);
            var oldest = _cache.Get(a.Id, 0);
            var evicted = _created[1];
            _cache.Request(b, 1);
            _cache.Tick(100);
            Assert.That(_cache.TextureCount, Is.EqualTo(3));
            Assert.That(oldest != null, Is.True);
            Assert.That(evicted == null, Is.True, "Eviction must destroy the owned Unity texture.");
            Assert.That(_cache.Get(a.Id, 1), Is.Null);
        }
        [Test]
        public void Invalidate_DestroysTexturesClearsPendingAndAllowsFreshRequest()
        {
            _cache = new AttackPreviewFrameCache(Render, () => _clock, sampleCount: 4);
            _cache.Request(Cell(), 1);
            _cache.Tick(1);
            Assert.That(_created.Count, Is.EqualTo(1));
            _cache.Invalidate();
            Assert.That(_created[0] == null, Is.True);
            Assert.That(_cache.TextureCount, Is.Zero);
            Assert.That(_cache.PendingCount, Is.Zero);
            Assert.That(_cache.Get(Cell().Id, 0), Is.Null);
            _cache.Request(Cell(), 1);
            _cache.Tick(100);
            Assert.That(_cache.TextureCount, Is.EqualTo(4));
        }
        [Test]
        public void Dispose_DestroysAllOwnedTexturesAndIsIdempotent()
        {
            _cache = new AttackPreviewFrameCache(Render, () => _clock, sampleCount: 2);
            _cache.Request(Cell(), 1);
            _cache.Tick(100);
            _cache.Dispose();
            Assert.That(_created.TrueForAll(t => t == null), Is.True);
            Assert.DoesNotThrow(() => _cache.Dispose());
        }
    }
}
#endif

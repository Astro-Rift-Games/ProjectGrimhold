using System.Collections.Generic;
using NUnit.Framework;
using Spawning;
using UnityEngine;

public class ReinforcementPointRegistryTests
{
    private ReinforcementPointRegistry _registry;
    private GameObject _container;

    [SetUp]
    public void Setup()
    {
        _registry = new ReinforcementPointRegistry();
        _container = new GameObject("TestContainer");
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_container);
    }

    [Test]
    public void Initialize_ValidGroup_ReturnsTrueAndSetsPoints()
    {
        var pt1 = new GameObject("pt1").transform;
        var pt2 = new GameObject("pt2").transform;
        pt1.SetParent(_container.transform);
        pt2.SetParent(_container.transform);

        Transform[] reinforcements = { pt1, pt2 };
        Transform[] enemies = new Transform[0];

        bool result = _registry.Initialize(reinforcements, enemies, out List<string> diagnostics);

        Assert.IsTrue(result);
        Assert.IsEmpty(diagnostics);
        Assert.AreEqual(2, _registry.Points.Length);
        Assert.AreEqual(pt1, _registry.Points[0]);
        Assert.AreEqual(pt2, _registry.Points[1]);
    }

    [Test]
    public void Initialize_NullPoint_ReturnsFalseAndRegistryIsEmpty()
    {
        var pt1 = new GameObject("pt1").transform;
        pt1.SetParent(_container.transform);

        Transform[] reinforcements = { pt1, null };
        Transform[] enemies = new Transform[0];

        bool result = _registry.Initialize(reinforcements, enemies, out List<string> diagnostics);

        Assert.IsFalse(result);
        Assert.IsNotEmpty(diagnostics);
        Assert.IsTrue(diagnostics[0].Contains("null"));
        Assert.AreEqual(0, _registry.Points.Length);
    }

    [Test]
    public void Initialize_DuplicatePoint_ReturnsFalseAndRegistryIsEmpty()
    {
        var pt1 = new GameObject("pt1").transform;
        pt1.SetParent(_container.transform);

        Transform[] reinforcements = { pt1, pt1 };
        Transform[] enemies = new Transform[0];

        bool result = _registry.Initialize(reinforcements, enemies, out List<string> diagnostics);

        Assert.IsFalse(result);
        Assert.IsNotEmpty(diagnostics);
        Assert.IsTrue(diagnostics[0].Contains("Duplicate"));
        Assert.AreEqual(0, _registry.Points.Length);
    }

    [Test]
    public void Initialize_CrossGroupConflictWithEnemies_ReturnsFalseAndRegistryIsEmpty()
    {
        var pt1 = new GameObject("pt1").transform;
        var pt2 = new GameObject("pt2").transform;
        pt1.SetParent(_container.transform);
        pt2.SetParent(_container.transform);

        Transform[] reinforcements = { pt1, pt2 };
        Transform[] enemies = { pt2 }; // pt2 is in both!

        bool result = _registry.Initialize(reinforcements, enemies, out List<string> diagnostics);

        Assert.IsFalse(result);
        Assert.IsNotEmpty(diagnostics);
        Assert.IsTrue(diagnostics[0].Contains("present in the Enemies group"));
        Assert.AreEqual(0, _registry.Points.Length);
    }

    [Test]
    public void ResetForRaidClosure_EmptiesRegistry()
    {
        var pt1 = new GameObject("pt1").transform;
        pt1.SetParent(_container.transform);

        Transform[] reinforcements = { pt1 };
        _registry.Initialize(reinforcements, null, out _);

        Assert.AreEqual(1, _registry.Points.Length);

        _registry.ResetForRaidClosure();

        Assert.AreEqual(0, _registry.Points.Length);
    }
}

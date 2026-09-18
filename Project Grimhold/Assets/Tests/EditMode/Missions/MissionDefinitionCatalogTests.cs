using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

public class MissionDefinitionCatalogTests
{
    private MissionDefinitionCatalog _catalog;
    private MissionDefinition _validMission;

    [SetUp]
    public void SetUp()
    {
        _catalog = ScriptableObject.CreateInstance<MissionDefinitionCatalog>();

        _validMission = ScriptableObject.CreateInstance<MissionDefinition>();
        SetPrivateField(_validMission, "_id", "test_mission_01");
        _validMission.Phases.Add(new PhaseDefinition
        {
            Objectives = new List<ObjectiveDefinition>
            {
                new ObjectiveDefinition
                {
                    Family = ObjectiveFamily.EliminacionPvE,
                    Condition = new ObjectiveCondition { RequiredAmount = 5 }
                }
            }
        });
    }

    [Test]
    public void TryValidate_EmptyCatalog_ReturnsFalse()
    {
        bool result = _catalog.TryValidate(out string error);
        Assert.IsFalse(result);
        Assert.IsNotNull(error);
        Assert.IsTrue(error.Contains("no entries"));
    }

    [Test]
    public void TryValidate_ValidMission_ReturnsTrue()
    {
        SetPrivateField(_catalog, "_definitions", new List<MissionDefinition> { _validMission });
        
        bool result = _catalog.TryValidate(out string error);
        Assert.IsTrue(result, error);
    }

    [Test]
    public void TryValidate_DuplicateId_ReturnsFalse()
    {
        var duplicateMission = ScriptableObject.CreateInstance<MissionDefinition>();
        SetPrivateField(duplicateMission, "_id", "test_mission_01");
        duplicateMission.Phases = _validMission.Phases;

        SetPrivateField(_catalog, "_definitions", new List<MissionDefinition> { _validMission, duplicateMission });
        
        bool result = _catalog.TryValidate(out string error);
        Assert.IsFalse(result);
        Assert.IsNotNull(error);
        Assert.IsTrue(error.Contains("duplicate entry"));
    }

    [Test]
    public void TryValidate_InvalidMission_ReturnsFalse()
    {
        var invalidMission = ScriptableObject.CreateInstance<MissionDefinition>();
        SetPrivateField(invalidMission, "_id", ""); // Empty ID is invalid

        SetPrivateField(_catalog, "_definitions", new List<MissionDefinition> { invalidMission });
        
        bool result = _catalog.TryValidate(out string error);
        Assert.IsFalse(result);
        Assert.IsNotNull(error);
    }

    [Test]
    public void TryGet_ExistingId_ReturnsDefinition()
    {
        SetPrivateField(_catalog, "_definitions", new List<MissionDefinition> { _validMission });
        
        bool result = _catalog.TryGet("test_mission_01", out var resolved);
        Assert.IsTrue(result);
        Assert.AreEqual(_validMission, resolved);
    }

    [Test]
    public void TryGet_MissingId_ReturnsFalse()
    {
        SetPrivateField(_catalog, "_definitions", new List<MissionDefinition> { _validMission });
        
        bool result = _catalog.TryGet("unknown_id", out var resolved);
        Assert.IsFalse(result);
        Assert.IsNull(resolved);
    }

    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(obj, value);
    }
}

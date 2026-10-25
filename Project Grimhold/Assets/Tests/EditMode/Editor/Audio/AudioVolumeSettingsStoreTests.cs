#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEngine;

public sealed class AudioVolumeSettingsStoreTests
{
    private string _prefix;

    [SetUp]
    public void SetUp()
    {
        _prefix = "Grimhold.Tests.Audio." + Guid.NewGuid().ToString("N") + ".";
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(_prefix + "MasterVolume");
        PlayerPrefs.DeleteKey(_prefix + "MusicVolume");
        PlayerPrefs.DeleteKey(_prefix + "SfxVolume");
        PlayerPrefs.Save();
    }

    [Test]
    public void NewStore_WithoutSavedValues_DefaultsToOne()
    {
        var store = new AudioVolumeSettingsStore(_prefix);
        AssertVolumes(store, 1f, 1f, 1f);
    }

    [Test]
    public void Save_NewStoreLoadsEachChannelIndependently()
    {
        var store = new AudioVolumeSettingsStore(_prefix);
        store.SetMasterVolume(0.2f);
        store.SetMusicVolume(0.4f);
        store.SetSfxVolume(0.6f);
        AssertVolumes(new AudioVolumeSettingsStore(_prefix), 0.2f, 0.4f, 0.6f);
    }

    [TestCase(-2f, 0f)]
    [TestCase(2f, 1f)]
    public void Save_ClampsEveryChannel(float input, float expected)
    {
        var store = new AudioVolumeSettingsStore(_prefix);
        store.SetMasterVolume(input);
        store.SetMusicVolume(input);
        store.SetSfxVolume(input);
        AssertVolumes(store, expected, expected, expected);
        AssertVolumes(new AudioVolumeSettingsStore(_prefix), expected, expected, expected);
    }

    [TestCase(-2f, 0f)]
    [TestCase(2f, 1f)]
    public void Load_ClampsExistingPreferences(float input, float expected)
    {
        PlayerPrefs.SetFloat(_prefix + "MasterVolume", input);
        PlayerPrefs.SetFloat(_prefix + "MusicVolume", input);
        PlayerPrefs.SetFloat(_prefix + "SfxVolume", input);
        AssertVolumes(new AudioVolumeSettingsStore(_prefix), expected, expected, expected);
    }

    private static void AssertVolumes(AudioVolumeSettingsStore store, float master, float music, float sfx)
    {
        Assert.That(store.MasterVolume, Is.EqualTo(master));
        Assert.That(store.MusicVolume, Is.EqualTo(music));
        Assert.That(store.SfxVolume, Is.EqualTo(sfx));
    }
}
#endif

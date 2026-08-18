using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Inspector-only entry point for the legacy direct MainMenu raid workflow.
/// Production UI enters the Town through <see cref="SessionConnectionCoordinator"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SessionConnectionCoordinator))]
public sealed class DirectRaidDevelopmentStarter : MonoBehaviour
{
    [SerializeField]
    private string _sessionName = "Development-Raid";

    private SessionConnectionCoordinator _coordinator;

    private void Awake()
    {
        _coordinator = GetComponent<SessionConnectionCoordinator>();
    }

    [ContextMenu("Start Direct Host Raid")]
    private async void StartDirectHostRaid()
    {
        try
        {
            await _coordinator.StartDirectRaidForDevelopmentAsync(
                _sessionName,
                GameMode.Host);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    [ContextMenu("Join Direct Client Raid")]
    private async void JoinDirectClientRaid()
    {
        try
        {
            await _coordinator.StartDirectRaidForDevelopmentAsync(
                _sessionName,
                GameMode.Client);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }
}

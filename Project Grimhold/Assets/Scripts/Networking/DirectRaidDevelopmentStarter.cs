using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// Inspector-only entry point for the legacy direct MainMenu raid workflow.
/// Production UI enters the Town through <see cref="SessionConnectionCoordinator"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SessionConnectionCoordinator))]
[RequireComponent(typeof(DevelopmentProfileBootstrap))]
public sealed class DirectRaidDevelopmentStarter : MonoBehaviour
{
    [SerializeField]
    private string _sessionName = "Development-Raid";

    private SessionConnectionCoordinator _coordinator;
    private DevelopmentProfileBootstrap _developmentProfileBootstrap;

    private void Awake()
    {
        _coordinator = GetComponent<SessionConnectionCoordinator>();
        _developmentProfileBootstrap = GetComponent<DevelopmentProfileBootstrap>();
    }

    [ContextMenu("Start Direct Host Raid")]
    private async void StartDirectHostRaid()
    {
        await StartDirectRaidAsync(GameMode.Host);
    }

    [ContextMenu("Join Direct Client Raid")]
    private async void JoinDirectClientRaid()
    {
        await StartDirectRaidAsync(GameMode.Client);
    }

    private async Task StartDirectRaidAsync(GameMode mode)
    {
        try
        {
            if (!_developmentProfileBootstrap.TryPrepareForDirectRaid())
            {
                Debug.LogError(
                    "[DirectRaidDevelopmentStarter] Direct raid not started: development profile preparation failed.",
                    this);
                return;
            }

            SessionTransitionResult result = await _coordinator.StartDirectRaidForDevelopmentAsync(
                _sessionName,
                mode);
            if (result != SessionTransitionResult.Succeeded)
            {
                Debug.LogWarning(
                    $"[DirectRaidDevelopmentStarter] Direct raid attempt did not succeed: {result}.",
                    this);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }
}

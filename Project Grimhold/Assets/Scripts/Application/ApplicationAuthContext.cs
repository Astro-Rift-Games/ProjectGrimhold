using System;
using UnityEngine;
using Grimhold.Backend;

public class ApplicationAuthContext : MonoBehaviour
{
    public static ApplicationAuthContext Instance { get; private set; }

    public bool IsAuthenticated { get; private set; }
    public string Token { get; private set; }
    public string CharacterId { get; private set; }
    public string CharacterName { get; private set; }
    public CharacterProfileValues Profile { get; private set; }
    public TownSessionTicket ActiveTownTicket { get; private set; }
    public BackendConfiguration BackendConfig { get; private set; }
    public ITownSessionTicketVerifier TownTicketVerifier { get; private set; }
    public IRaidAdmissionVerifier RaidAdmissionVerifier { get; private set; }

    public event Action SessionInvalidated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Initialize(
        string token,
        CharacterData characterData,
        CharacterProfileData profileData,
        BackendConfiguration backendConfig = null)
    {
        Token = token;
        CharacterId = characterData.characterId;
        CharacterName = characterData.name;
        Profile = profileData.profile;
        ActiveTownTicket = default;
        BackendConfig = backendConfig;
        if (backendConfig != null && !string.IsNullOrEmpty(token))
        {
            TownTicketVerifier = new BackendTownSessionTicketVerifier(backendConfig, token);
            RaidAdmissionVerifier = new BackendRaidAdmissionVerifier(backendConfig, token);
        }
        else
        {
            TownTicketVerifier = null;
            RaidAdmissionVerifier = null;
        }
        IsAuthenticated = true;
    }

    public void SetVerifiers(ITownSessionTicketVerifier townTicketVerifier, IRaidAdmissionVerifier raidAdmissionVerifier)
    {
        TownTicketVerifier = townTicketVerifier;
        RaidAdmissionVerifier = raidAdmissionVerifier;
    }

    public void SetTownSessionTicket(in TownSessionTicket ticket)
    {
        ActiveTownTicket = ticket;
        if (ticket.IsValid && TownTicketVerifier != null)
        {
            _ = TownTicketVerifier.VerifyTownTicketAsync(ticket.RawTicket);
        }
    }

    public void UpdateProfile(CharacterProfileValues updatedProfile)
    {
        Profile = updatedProfile;
    }

    public void HandleUnauthorizedSession()
    {
        if (!IsAuthenticated && string.IsNullOrEmpty(Token))
        {
            return;
        }

        Debug.LogWarning($"[{nameof(ApplicationAuthContext)}] Session invalidated due to 401 UNAUTHORIZED. Clearing local auth and stash state.");
        Clear();
        LocalProfileProvider.ClearRemoteCharacterId();
        ApplicationStashServiceBootstrapper.ResetForLogout();
        SessionInvalidated?.Invoke();
    }

    public void Clear()
    {
        Token = null;
        CharacterId = null;
        CharacterName = null;
        Profile = default;
        ActiveTownTicket = default;
        BackendConfig = null;
        TownTicketVerifier = null;
        RaidAdmissionVerifier = null;
        IsAuthenticated = false;
    }
}

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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Initialize(string token, CharacterData characterData, CharacterProfileData profileData)
    {
        Token = token;
        CharacterId = characterData.characterId;
        CharacterName = characterData.name;
        Profile = profileData.profile;
        IsAuthenticated = true;
    }

    public void Clear()
    {
        Token = null;
        CharacterId = null;
        CharacterName = null;
        Profile = default;
        IsAuthenticated = false;
    }
}

using System;

namespace Grimhold.Backend
{
    [Serializable]
    public struct LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public struct LoginResult
    {
        public string token;
        public int expiresIn;
    }

    [Serializable]
    public struct CharacterData
    {
        public string characterId;
        public string name;
    }

    [Serializable]
    public struct CharacterProfileData
    {
        public string characterId;
        public CharacterProfileValues profile;
    }

    [Serializable]
    public struct CharacterProfileValues
    {
        public string lastSeen;
        public string customNote;
    }

    [Serializable]
    public struct PatchProfileRequest
    {
        public string customNote;
    }

    [Serializable]
    public struct CreateCharacterRequest
    {
        public string name;
    }

    [Serializable]
    public struct CreateCharacterResult
    {
        public string characterId;
        public string name;
    }
}

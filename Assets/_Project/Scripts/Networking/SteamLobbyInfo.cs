using Steamworks;

public sealed class SteamLobbyInfo
{
    public CSteamID LobbyId { get; }
    public string LobbyName { get; }
    public string HostName { get; }
    public int CurrentPlayers { get; }
    public int MaxPlayers { get; }
    public string GameVersion { get; }
    public string NetworkVersion { get; }
    public string GameState { get; }

    public bool IsFull => CurrentPlayers >= MaxPlayers;

    public SteamLobbyInfo(
        CSteamID lobbyId,
        string lobbyName,
        string hostName,
        int currentPlayers,
        int maxPlayers,
        string gameVersion,
        string networkVersion,
        string gameState)
    {
        LobbyId = lobbyId;
        LobbyName = lobbyName;
        HostName = hostName;
        CurrentPlayers = currentPlayers;
        MaxPlayers = maxPlayers;
        GameVersion = gameVersion;
        NetworkVersion = networkVersion;
        GameState = gameState;
    }
}
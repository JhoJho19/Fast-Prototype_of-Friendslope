using System;
using System.Collections.Generic;
using kcp2k;
using Mirror;
using Mirror.FizzySteam;
using Steamworks;
using UnityEngine;

public sealed class CoopNetworkSessionController : MonoBehaviour
{
    [SerializeField] private CoopNetworkManager networkManager;
    [SerializeField] private KcpTransport kcpTransport;
    [SerializeField] private FizzySteamworks steamTransport;
    [SerializeField] private SteamNetworkPlatformService steamPlatform;

    public event Action<string> StatusChanged;
    public event Action<string> LobbySearchStatus;

    public ulong CurrentSteamLobbyId =>
        steamPlatform != null ? steamPlatform.CurrentLobbyId : 0;

    public SteamLobbyBrowserState BrowserState =>
        steamPlatform != null ? steamPlatform.BrowserState :
            SteamLobbyBrowserState.Idle;

    public IReadOnlyList<SteamLobbyInfo> PublicLobbies =>
        steamPlatform != null ? steamPlatform.PublicLobbies :
            System.Array.Empty<SteamLobbyInfo>();

    public bool IsSearching => steamPlatform != null && steamPlatform.IsSearching;

    private void Awake()
    {
        ResolveReferences();
        UseKcpTransport();
    }

    private void Start()
    {
        TryStartLanClientFromCommandLine();
    }

    private void OnEnable()
    {
        ResolveReferences();

        CoopNetworkManager.PlayersChanged += HandlePlayersChanged;

        if (steamPlatform == null)
        {
            return;
        }

        steamPlatform.StatusChanged += SetStatus;
        steamPlatform.HostLobbyReady += HandleSteamHostLobbyReady;
        steamPlatform.JoinAddressResolved += StartSteamClient;
        steamPlatform.LobbySearchFailed += OnLobbySearchFailed;
    }

    private void OnDisable()
    {
        CoopNetworkManager.PlayersChanged -= HandlePlayersChanged;

        if (steamPlatform == null)
        {
            return;
        }

        steamPlatform.StatusChanged -= SetStatus;
        steamPlatform.HostLobbyReady -= HandleSteamHostLobbyReady;
        steamPlatform.JoinAddressResolved -= StartSteamClient;
        steamPlatform.LobbySearchFailed -= OnLobbySearchFailed;
    }

    public void StartLanHost()
    {
        if (!CanStartNetwork())
        {
            return;
        }

        UseKcpTransport();
        networkManager.networkAddress = "localhost";
        networkManager.StartHost();
        SetStatus("LAN host started on port 7777.");
    }

    public void StartLanClient(string address)
    {
        if (!CanStartNetwork())
        {
            return;
        }

        UseKcpTransport();
        networkManager.networkAddress = string.IsNullOrWhiteSpace(address)
            ? "localhost"
            : address.Trim();
        networkManager.StartClient();
        SetStatus($"Connecting to {networkManager.networkAddress}:7777...");
    }

    public void StartSteamHost()
    {
        if (!CanStartNetwork())
        {
            return;
        }

        if (steamPlatform == null)
        {
            SetStatus("Steam service is not configured.");
            return;
        }

        steamPlatform.CreateLobby();
    }

    public void JoinSteamLobby(string lobbyId)
    {
        if (!CanStartNetwork())
        {
            return;
        }

        if (steamPlatform == null || !steamPlatform.JoinLobby(lobbyId))
        {
            SetStatus("Could not join Steam lobby.");
        }
    }

    public void JoinSteamLobby(CSteamID lobbyId)
    {
        if (lobbyId == CSteamID.Nil)
        {
            SetStatus("Invalid lobby selected.");
            return;
        }

        if (!CanStartNetwork())
        {
            return;
        }

        steamPlatform?.JoinLobby(lobbyId);
    }

    public void RefreshPublicLobbies()
    {
        steamPlatform?.RefreshLobbies();
    }

    public void StartSteamClient(string steamHostAddress)
    {
        if (!CanStartNetwork())
        {
            return;
        }

        UseSteamTransport();
        networkManager.networkAddress = steamHostAddress;
        networkManager.StartClient();
        SetStatus($"Steam client connecting to {steamHostAddress}...");
    }

    public void StopNetwork()
    {
        if (NetworkServer.active && NetworkClient.active)
        {
            networkManager.StopHost();
        }
        else if (NetworkClient.active)
        {
            networkManager.StopClient();
        }
        else if (NetworkServer.active)
        {
            networkManager.StopServer();
        }

        steamPlatform?.SetLobbyGameState("closed");
        steamPlatform?.LeaveLobby();
        UseKcpTransport();
        SetStatus("Network session stopped.");
    }

    private void HandleSteamHostLobbyReady(string hostAddress)
    {
        if (!CanStartNetwork())
        {
            return;
        }

        UseSteamTransport();
        networkManager.networkAddress = hostAddress;
        networkManager.StartHost();
        steamPlatform?.SetLobbyGameState("waiting");
        SetStatus($"Steam host started. Lobby ID: {CurrentSteamLobbyId}");
    }

    private void OnLobbySearchFailed(string message)
    {
        LobbySearchStatus?.Invoke(message);
    }

    private void HandlePlayersChanged()
    {
        if (!NetworkServer.active || steamPlatform == null)
        {
            return;
        }

int playerCount = 0;

        foreach (NetworkConnectionToClient connection in
                 NetworkServer.connections.Values)
        {
            if (connection != null && connection.identity != null)
            {
                playerCount++;
            }
        }

        steamPlatform.SetLobbyGameState(
            playerCount >= 2 ? "playing" : "waiting");
    }

    private void UseKcpTransport()
    {
        if (networkManager == null || kcpTransport == null)
        {
            return;
        }

        networkManager.transport = kcpTransport;
        Transport.active = kcpTransport;
    }

    private void UseSteamTransport()
    {
        if (networkManager == null || steamTransport == null)
        {
            return;
        }

        networkManager.transport = steamTransport;
        Transport.active = steamTransport;
    }

    private bool CanStartNetwork()
    {
        ResolveReferences();

        if (networkManager == null)
        {
            SetStatus("NetworkManager is not configured.");
            return false;
        }

        if (NetworkClient.active || NetworkServer.active)
        {
            SetStatus("A network session is already active.");
            return false;
        }

        return true;
    }

    private void ResolveReferences()
    {
        networkManager ??= GetComponent<CoopNetworkManager>();
        kcpTransport ??= GetComponent<KcpTransport>();
        steamTransport ??= GetComponent<FizzySteamworks>();
        steamPlatform ??= GetComponent<SteamNetworkPlatformService>();
    }

    private void TryStartLanClientFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(
                    args[index],
                    "-connect_lan",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            StartLanClient(args[index + 1]);
            return;
        }
    }

    private void SetStatus(string status)
    {
        Debug.Log(status);
        StatusChanged?.Invoke(status);
    }
}

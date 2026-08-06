using System;
using kcp2k;
using Mirror;
using Mirror.FizzySteam;
using UnityEngine;

public sealed class CoopNetworkSessionController : MonoBehaviour
{
    [SerializeField] private CoopNetworkManager networkManager;
    [SerializeField] private KcpTransport kcpTransport;
    [SerializeField] private FizzySteamworks steamTransport;
    [SerializeField] private SteamNetworkPlatformService steamPlatform;

    public event Action<string> StatusChanged;

    public ulong CurrentSteamLobbyId =>
        steamPlatform != null ? steamPlatform.CurrentLobbyId : 0;

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

        if (steamPlatform == null)
        {
            return;
        }

        steamPlatform.StatusChanged += SetStatus;
        steamPlatform.HostLobbyReady += HandleSteamHostLobbyReady;
        steamPlatform.JoinAddressResolved += StartSteamClient;
    }

    private void OnDisable()
    {
        if (steamPlatform == null)
        {
            return;
        }

        steamPlatform.StatusChanged -= SetStatus;
        steamPlatform.HostLobbyReady -= HandleSteamHostLobbyReady;
        steamPlatform.JoinAddressResolved -= StartSteamClient;
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
        SetStatus($"Steam host started. Lobby ID: {CurrentSteamLobbyId}");
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

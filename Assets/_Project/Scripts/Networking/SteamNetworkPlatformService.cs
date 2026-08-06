using System;
using Steamworks;
using UnityEngine;

public sealed class SteamNetworkPlatformService : MonoBehaviour
{
    private const string HostAddressKey = "host_address";
    private const string HostUserIdKey = "host_user_id";
    private const string LobbyNameKey = "lobby_name";
    private const string PlatformKey = "platform";

    [Tooltip("Steam App ID used by the lobby service. 480 is Spacewar for development.")]
    [SerializeField] private uint appId = 480;

    [SerializeField, Min(1)] private int maxMembers = 4;
    [SerializeField] private ELobbyType lobbyType =
        ELobbyType.k_ELobbyTypeFriendsOnly;

    private Callback<LobbyCreated_t> lobbyCreatedCallback;
    private Callback<LobbyEnter_t> lobbyEnteredCallback;
    private Callback<GameLobbyJoinRequested_t> joinRequestedCallback;
    private Callback<LobbyInvite_t> lobbyInviteCallback;
    private bool initialized;
    private bool creatingLobby;
    private CSteamID currentLobby;

    public event Action<string> StatusChanged;
    public event Action<string> HostLobbyReady;
    public event Action<string> JoinAddressResolved;

    public bool IsInitialized => initialized;
    public ulong CurrentLobbyId => currentLobby.m_SteamID;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        TryJoinLobbyFromCommandLine();
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        try
        {
            if (!SteamAPI.IsSteamRunning() || !CallbackDispatcher.IsInitialized)
            {
                initialized = false;
                SetStatus($"Steam unavailable. Run Steam with AppID {appId}.");
                return;
            }

            SteamAPI.RunCallbacks();
        }
        catch (InvalidOperationException exception)
        {
            initialized = false;
            Debug.LogWarning($"Steam callbacks stopped: {exception.Message}");
        }
    }

    private void OnDestroy()
    {
        LeaveLobby();

        lobbyCreatedCallback?.Dispose();
        lobbyEnteredCallback?.Dispose();
        joinRequestedCallback?.Dispose();
        lobbyInviteCallback?.Dispose();

        if (initialized)
        {
            SteamAPI.Shutdown();
            initialized = false;
        }
    }

    public bool Initialize()
    {
        if (initialized)
        {
            return true;
        }

        try
        {
            if (!SteamAPI.IsSteamRunning())
            {
                SetStatus($"Steam unavailable. Run Steam with AppID {appId}.");
                initialized = false;
                return false;
            }

            string steamError;
            ESteamAPIInitResult initResult = SteamAPI.InitEx(out steamError);
            initialized =
                initResult == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;

            if (initialized && !CallbackDispatcher.IsInitialized)
            {
                initialized = false;
                steamError = "Steam callback dispatcher is not initialized.";
            }

            if (!initialized)
            {
                SetStatus($"Steam initialization failed: {initResult} {steamError}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Steam initialization failed: {exception.Message}");
            initialized = false;
        }

        if (!initialized)
        {
            SetStatus($"Steam unavailable. Run Steam with AppID {appId}.");
            return false;
        }

        lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(HandleLobbyCreated);
        lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(HandleLobbyEntered);
        joinRequestedCallback =
            Callback<GameLobbyJoinRequested_t>.Create(HandleJoinRequested);
        lobbyInviteCallback = Callback<LobbyInvite_t>.Create(HandleLobbyInvite);
        SetStatus($"Steam ready: {SteamFriends.GetPersonaName()}");
        return true;
    }

    public void CreateLobby()
    {
        if (!Initialize())
        {
            return;
        }

        creatingLobby = true;
        SetStatus("Creating Steam lobby...");
        SteamMatchmaking.CreateLobby(
            lobbyType,
            Mathf.Clamp(maxMembers, 1, 4));
    }

    public bool JoinLobby(string lobbyId)
    {
        if (!Initialize())
        {
            return false;
        }

        if (!ulong.TryParse(lobbyId, out ulong parsedLobbyId) ||
            parsedLobbyId == 0)
        {
            SetStatus("Invalid Steam lobby ID.");
            return false;
        }

        JoinLobby(new CSteamID(parsedLobbyId));
        return true;
    }

    public void LeaveLobby()
    {
        if (!initialized || currentLobby.m_SteamID == 0)
        {
            return;
        }

        SteamMatchmaking.LeaveLobby(currentLobby);
        currentLobby = CSteamID.Nil;
    }

    private void JoinLobby(CSteamID lobbyId)
    {
        creatingLobby = false;
        SetStatus($"Joining Steam lobby {lobbyId.m_SteamID}...");
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    private void HandleLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            creatingLobby = false;
            SetStatus($"Steam lobby creation failed: {callback.m_eResult}");
            return;
        }

        currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
        string hostAddress = SteamUser.GetSteamID().m_SteamID.ToString();

        SteamMatchmaking.SetLobbyData(currentLobby, HostAddressKey, hostAddress);
        SteamMatchmaking.SetLobbyData(currentLobby, HostUserIdKey, hostAddress);
        SteamMatchmaking.SetLobbyData(
            currentLobby,
            LobbyNameKey,
            $"{SteamFriends.GetPersonaName()}'s Friendslope");
        SteamMatchmaking.SetLobbyData(currentLobby, PlatformKey, "steam");
        SteamMatchmaking.SetLobbyJoinable(currentLobby, true);

        creatingLobby = false;
        SetStatus($"Steam lobby created: {currentLobby.m_SteamID}");
        HostLobbyReady?.Invoke(hostAddress);
    }

    private void HandleLobbyEntered(LobbyEnter_t callback)
    {
        if (callback.m_EChatRoomEnterResponse !=
            (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            SetStatus($"Failed to enter Steam lobby: {callback.m_EChatRoomEnterResponse}");
            return;
        }

        currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
        string hostAddress =
            SteamMatchmaking.GetLobbyData(currentLobby, HostAddressKey);
        string localAddress = SteamUser.GetSteamID().m_SteamID.ToString();

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            SetStatus("Steam lobby has no host address.");
            return;
        }

        SetStatus($"Steam lobby: {currentLobby.m_SteamID}");

        if (!creatingLobby &&
            !string.Equals(hostAddress, localAddress, StringComparison.Ordinal))
        {
            JoinAddressResolved?.Invoke(hostAddress);
        }
    }

    private void HandleJoinRequested(GameLobbyJoinRequested_t callback)
    {
        JoinLobby(callback.m_steamIDLobby);
    }

    private void HandleLobbyInvite(LobbyInvite_t callback)
    {
        if (callback.m_ulSteamIDLobby != 0)
        {
            JoinLobby(new CSteamID(callback.m_ulSteamIDLobby));
        }
    }

    private void TryJoinLobbyFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(
                    args[i],
                    "+connect_lobby",
                    StringComparison.OrdinalIgnoreCase))
            {
                JoinLobby(args[i + 1]);
                return;
            }
        }
    }

    private void SetStatus(string status)
    {
        Debug.Log(status);
        StatusChanged?.Invoke(status);
    }
}

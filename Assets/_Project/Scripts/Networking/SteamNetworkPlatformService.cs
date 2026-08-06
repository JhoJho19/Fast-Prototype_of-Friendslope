using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public enum SteamLobbyBrowserState
{
    Idle,
    Loading,
    Loaded,
    Empty,
    Error
}

public sealed class SteamNetworkPlatformService : MonoBehaviour
{
    private const string HostAddressKey = "host_address";
    private const string HostUserIdKey = "host_user_id";
    private const string HostNameKey = "host_name";
    private const string LobbyNameKey = "lobby_name";
    private const string PlatformKey = "platform";
    private const string GameVersionKey = "game_version";
    private const string NetworkVersionKey = "network_version";
    private const string GameStateKey = "game_state";

    private const string PlatformValue = "steam";
    private const string NetworkVersion = "1.0";

    private const string GameStateWaiting = "waiting";
    private const string GameStatePlaying = "playing";
    private const string GameStateClosed = "closed";

    [Tooltip("Steam App ID used by the lobby service. 480 is Spacewar for development.")]
    [SerializeField] private uint appId = 480;

    [Tooltip("Maximum members per hosted lobby. Clamped to 1..4 by Steam matchmaking rules.")]
    [SerializeField, Min(1)] private int maxMembers = 4;

    [Tooltip("Search distance used when listing public lobbies.")]
    [SerializeField] private ELobbyDistanceFilter lobbyDistanceFilter =
        ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide;

    [Tooltip("Maximum number of lobbies requested from Steam at once.")]
    [SerializeField, Min(1)] private int maxLobbyResults = 50;

    private Callback<LobbyCreated_t> lobbyCreatedCallback;
    private Callback<LobbyEnter_t> lobbyEnteredCallback;
    private Callback<GameLobbyJoinRequested_t> joinRequestedCallback;
    private Callback<LobbyInvite_t> lobbyInviteCallback;
    private CallResult<LobbyMatchList_t> lobbyListCallResult;

    private bool initialized;
    private bool creatingLobby;
    private bool isSearching;
    private CSteamID currentLobby;
    private SteamLobbyBrowserState browserState;
    private IReadOnlyList<SteamLobbyInfo> publicLobbies =
        Array.Empty<SteamLobbyInfo>();

    public event Action<string> StatusChanged;
    public event Action<string> HostLobbyReady;
    public event Action<string> JoinAddressResolved;
    public event Action<SteamLobbyBrowserState> LobbyBrowserStateChanged;
    public event Action<IReadOnlyList<SteamLobbyInfo>> LobbyListLoaded;
    public event Action<string> LobbySearchFailed;

    public bool IsInitialized => initialized;
    public bool IsSearching => isSearching;
    public ulong CurrentLobbyId => currentLobby.m_SteamID;
    public SteamLobbyBrowserState BrowserState => browserState;
    public IReadOnlyList<SteamLobbyInfo> PublicLobbies => publicLobbies;

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
        CancelLobbySearch();
        lobbyListCallResult?.Cancel();

        lobbyCreatedCallback?.Dispose();
        lobbyEnteredCallback?.Dispose();
        joinRequestedCallback?.Dispose();
        lobbyInviteCallback?.Dispose();
        lobbyListCallResult?.Dispose();

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
        lobbyListCallResult =
            CallResult<LobbyMatchList_t>.Create(HandleLobbyMatchList);

        SetStatus($"Steam ready: {SteamFriends.GetPersonaName()}");
        SetBrowserState(SteamLobbyBrowserState.Idle);
        return true;
    }

    public void CreateLobby()
    {
        if (!Initialize())
        {
            return;
        }

        creatingLobby = true;
        SetStatus("Creating public Steam lobby...");
        SteamMatchmaking.CreateLobby(
            ELobbyType.k_ELobbyTypePublic,
            Mathf.Clamp(maxMembers, 1, 4));
    }

    public void RefreshLobbies()
    {
        if (!Initialize())
        {
            return;
        }

        if (isSearching)
        {
            SetStatus("Lobby search already in progress.");
            return;
        }

        publicLobbies = Array.Empty<SteamLobbyInfo>();
        isSearching = true;
        SetBrowserState(SteamLobbyBrowserState.Loading);
        SetStatus("Searching for public sessions...");

        RequestPublicLobbies();
    }

    public void RequestPublicLobbies()
    {
        if (!initialized || isSearching == false)
        {
            return;
        }

        SteamMatchmaking.AddRequestLobbyListDistanceFilter(lobbyDistanceFilter);
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            PlatformKey,
            PlatformValue,
            ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            NetworkVersionKey,
            NetworkVersion,
            ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(maxLobbyResults);

        SteamAPICall_t request = SteamMatchmaking.RequestLobbyList();
        lobbyListCallResult?.Set(request);
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

    public void JoinLobby(CSteamID lobbyId)
    {
        if (lobbyId == CSteamID.Nil)
        {
            return;
        }

        creatingLobby = false;
        SetStatus($"Joining Steam lobby {lobbyId.m_SteamID}...");
        SteamMatchmaking.JoinLobby(lobbyId);
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

    public void SetLobbyGameState(string gameState)
    {
        if (!initialized || currentLobby.m_SteamID == 0)
        {
            return;
        }

        SteamMatchmaking.SetLobbyData(currentLobby, GameStateKey, gameState);
    }

    private void CancelLobbySearch()
    {
        if (isSearching)
        {
            isSearching = false;
            SetBrowserState(SteamLobbyBrowserState.Idle);
        }
    }

    private void HandleLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            creatingLobby = false;
            SetStatus($"Lobby creation failed: {callback.m_eResult}");
            return;
        }

        currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
        string hostAddress = SteamUser.GetSteamID().m_SteamID.ToString();
        string hostName = SteamFriends.GetPersonaName();

        SteamMatchmaking.SetLobbyData(currentLobby, HostAddressKey, hostAddress);
        SteamMatchmaking.SetLobbyData(currentLobby, HostUserIdKey, hostAddress);
        SteamMatchmaking.SetLobbyData(currentLobby, HostNameKey, hostName);
        SteamMatchmaking.SetLobbyData(
            currentLobby,
            LobbyNameKey,
            $"{hostName}'s Friendslope");
        SteamMatchmaking.SetLobbyData(currentLobby, PlatformKey, PlatformValue);
        SteamMatchmaking.SetLobbyData(
            currentLobby,
            GameVersionKey,
            Application.version);
        SteamMatchmaking.SetLobbyData(
            currentLobby,
            NetworkVersionKey,
            NetworkVersion);
        SteamMatchmaking.SetLobbyData(
            currentLobby,
            GameStateKey,
            GameStateWaiting);
        SteamMatchmaking.SetLobbyJoinable(currentLobby, true);

        creatingLobby = false;
        SetStatus($"Public lobby created: {currentLobby.m_SteamID}");
        HostLobbyReady?.Invoke(hostAddress);
    }

    private void HandleLobbyMatchList(LobbyMatchList_t result, bool bIOFailure)
    {
        isSearching = false;

        if (bIOFailure)
        {
            SetBrowserState(SteamLobbyBrowserState.Error);
            string message =
                "Lobby search failed. Please check your Steam connection and try again.";
            SetStatus(message);
            LobbySearchFailed?.Invoke(message);
            return;
        }

        List<SteamLobbyInfo> found = new List<SteamLobbyInfo>();
        HashSet<ulong> seenIds = new HashSet<ulong>();
        bool attemptedRead = false;

        try
        {
            for (uint index = 0; index < result.m_nLobbiesMatching; index++)
            {
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex((int)index);
                SteamLobbyInfo info = TryReadLobbyInfo(lobbyId, seenIds);
                if (info != null)
                {
                    attemptedRead = true;
                    found.Add(info);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to read lobby list: {exception.Message}");
            SetBrowserState(SteamLobbyBrowserState.Error);
            string message =
                "Failed to read lobby list. Please try again.";
            SetStatus(message);
            LobbySearchFailed?.Invoke(message);
            return;
        }

        publicLobbies = found;
        LobbyListLoaded?.Invoke(found);

        if (!attemptedRead || found.Count == 0)
        {
            SetBrowserState(SteamLobbyBrowserState.Empty);
            SetStatus("No open public sessions found.");
        }
        else
        {
            SetBrowserState(SteamLobbyBrowserState.Loaded);
            SetStatus($"Found {found.Count} session(s).");
        }
    }

    private SteamLobbyInfo TryReadLobbyInfo(
        CSteamID lobbyId,
        HashSet<ulong> seenIds)
    {
        if (lobbyId == CSteamID.Nil)
        {
            return null;
        }

        if (!seenIds.Add(lobbyId.m_SteamID))
        {
            return null;
        }

        if (!string.Equals(
                SteamMatchmaking.GetLobbyData(lobbyId, PlatformKey),
                PlatformValue,
                StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.Equals(
                SteamMatchmaking.GetLobbyData(lobbyId, NetworkVersionKey),
                NetworkVersion,
                StringComparison.Ordinal))
        {
            return null;
        }

        string gameState = SteamMatchmaking.GetLobbyData(lobbyId, GameStateKey);
        if (!string.Equals(
                gameState,
                GameStateWaiting,
                StringComparison.Ordinal))
        {
            return null;
        }

        string hostAddress = SteamMatchmaking.GetLobbyData(lobbyId, HostAddressKey);
        string hostName = SteamMatchmaking.GetLobbyData(lobbyId, HostNameKey);
        string lobbyName = SteamMatchmaking.GetLobbyData(lobbyId, LobbyNameKey);

        if (string.IsNullOrWhiteSpace(hostAddress) ||
            string.IsNullOrWhiteSpace(hostName) ||
            string.IsNullOrWhiteSpace(lobbyName))
        {
            return null;
        }

        int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
        int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

        if (maxPlayers <= 0 || currentPlayers >= maxPlayers)
        {
            return null;
        }

        return new SteamLobbyInfo(
            lobbyId,
            lobbyName,
            hostName,
            currentPlayers,
            maxPlayers,
            SteamMatchmaking.GetLobbyData(lobbyId, GameVersionKey),
            SteamMatchmaking.GetLobbyData(lobbyId, NetworkVersionKey),
            gameState);
    }

    private void HandleLobbyEntered(LobbyEnter_t callback)
    {
        if (callback.m_EChatRoomEnterResponse !=
            (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            string message =
                $"Failed to enter lobby: {callback.m_EChatRoomEnterResponse}";
            SetStatus(message);
            LobbySearchFailed?.Invoke(message);
            return;
        }

        currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
        string hostAddress =
            SteamMatchmaking.GetLobbyData(currentLobby, HostAddressKey);
        string localAddress = SteamUser.GetSteamID().m_SteamID.ToString();

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            SetStatus("Lobby has no host address.");
            return;
        }

        SetStatus($"Joined lobby: {currentLobby.m_SteamID}");

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

    private void SetBrowserState(SteamLobbyBrowserState state)
    {
        browserState = state;
        LobbyBrowserStateChanged?.Invoke(state);
    }

    private void SetStatus(string status)
    {
        Debug.Log(status);
        StatusChanged?.Invoke(status);
    }
}
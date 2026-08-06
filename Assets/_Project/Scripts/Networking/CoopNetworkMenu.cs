using Mirror;
using UnityEngine;

public sealed class CoopNetworkMenu : MonoBehaviour
{
    [SerializeField] private CoopNetworkSessionController session;

    private string steamLobbyId = string.Empty;
    private string status = "Ready.";
    private string lastBrowserError = string.Empty;
    private bool isConnecting;
    private Vector2 lobbyScrollPosition;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;
    private bool stylesConfigured;

    private void Awake()
    {
        session ??= GetComponent<CoopNetworkSessionController>();
    }

    private void OnEnable()
    {
        if (session != null)
        {
            session.StatusChanged += SetStatus;
            session.LobbySearchStatus += SetBrowserError;
        }
    }

    private void OnDisable()
    {
        if (session != null)
        {
            session.StatusChanged -= SetStatus;
            session.LobbySearchStatus -= SetBrowserError;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        bool inSession = NetworkClient.active || NetworkServer.active;
        Cursor.visible = !inSession;
        Cursor.lockState = inSession
            ? CursorLockMode.Locked
            : CursorLockMode.None;
    }

    private void OnGUI()
    {
        if (session == null)
        {
            return;
        }

        ConfigureStyles();

        if (NetworkClient.active || NetworkServer.active)
        {
            DrawInGameLobbyLabel();
            return;
        }

        DrawSessionMenu();
    }

    private void DrawInGameLobbyLabel()
    {
        GUILayout.BeginArea(
            new Rect(Screen.width - 360f, 24f, 340f, 60f),
            GUI.skin.window);

        GUILayout.Label($"Lobby: {session.CurrentSteamLobbyId}", labelStyle);

        GUILayout.EndArea();
    }

    private void DrawSessionMenu()
    {
        Rect windowRect = new Rect(
            Mathf.Max(20f, (Screen.width - 600f) * 0.5f),
            Mathf.Max(20f, (Screen.height - 640f) * 0.5f),
            600f,
            640f);

        GUILayout.BeginArea(windowRect, GUI.skin.window);

        GUILayout.Label("Friendslope Co-op", titleStyle);
        GUILayout.Space(4f);
        GUILayout.Label(status, labelStyle);
        GUILayout.Space(8f);

        isConnecting = false;

        GUILayout.Label("Steam sessions", labelStyle);

        if (GUILayout.Button("Create Session", buttonStyle, GUILayout.Height(56f)))
        {
            session.StartSteamHost();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Find Sessions", buttonStyle, GUILayout.Height(56f)))
        {
            lastBrowserError = string.Empty;
            session.RefreshPublicLobbies();
        }

        if (session.IsSearching)
        {
            GUILayout.Label("Searching...", labelStyle);
        }
        GUILayout.EndHorizontal();

        DrawPublicLobbies();

        GUILayout.Space(12f);
        GUILayout.Label("Manual Steam lobby ID (debug)", labelStyle);
        steamLobbyId = GUILayout.TextField(steamLobbyId);

        if (GUILayout.Button("Join Steam Lobby", buttonStyle, GUILayout.Height(56f)))
        {
            session.JoinSteamLobby(steamLobbyId);
        }

        GUILayout.EndArea();
    }

    private void DrawPublicLobbies()
    {
        SteamLobbyBrowserState browserState = session.BrowserState;

        switch (browserState)
        {
            case SteamLobbyBrowserState.Idle:
                GUILayout.Label("No search yet. Press \"Find Sessions\".", labelStyle);
                return;

            case SteamLobbyBrowserState.Loading:
                GUILayout.Label("Loading public sessions...", labelStyle);
                return;

            case SteamLobbyBrowserState.Empty:
                GUILayout.Label("No open sessions found.", labelStyle);
                return;

            case SteamLobbyBrowserState.Error:
                GUILayout.Label(
                    string.IsNullOrWhiteSpace(lastBrowserError)
                        ? "Lobby search failed."
                        : lastBrowserError,
                    labelStyle);
                return;
        }

        lobbyScrollPosition = GUILayout.BeginScrollView(
            lobbyScrollPosition,
            GUILayout.Height(200f));

        foreach (SteamLobbyInfo info in session.PublicLobbies)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(info.LobbyName, labelStyle);
            GUILayout.Label(
                $"Host: {info.HostName}   {info.CurrentPlayers}/{info.MaxPlayers}" +
                $"   {info.GameState}",
                labelStyle);

            if (GUILayout.Button("Connect", buttonStyle, GUILayout.Height(44f)))
            {
                isConnecting = true;
                lastBrowserError = string.Empty;
                session.JoinSteamLobby(info.LobbyId);
            }

            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();
    }

    private void ConfigureStyles()
    {
        if (stylesConfigured)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };

        stylesConfigured = true;
    }

    private void SetBrowserError(string message)
    {
        lastBrowserError = message;
    }

    private void SetStatus(string message)
    {
        status = message;
    }
}
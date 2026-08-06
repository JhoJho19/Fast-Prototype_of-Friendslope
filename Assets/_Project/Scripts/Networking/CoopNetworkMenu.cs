using Mirror;
using UnityEngine;

public sealed class CoopNetworkMenu : MonoBehaviour
{
    [SerializeField] private CoopNetworkSessionController session;

    private string lanAddress = "localhost";
    private string steamLobbyId = string.Empty;
    private string status = "Ready.";

    private void Awake()
    {
        session ??= GetComponent<CoopNetworkSessionController>();
    }

    private void OnEnable()
    {
        if (session != null)
        {
            session.StatusChanged += SetStatus;
        }
    }

    private void OnDisable()
    {
        if (session != null)
        {
            session.StatusChanged -= SetStatus;
        }
    }

    private void OnGUI()
    {
        if (session == null)
        {
            return;
        }

        GUILayout.BeginArea(
            new Rect(20f, 20f, 430f, 340f),
            GUI.skin.window);

        GUILayout.Label("Friendslope Co-op");
        GUILayout.Label(status);

        if (NetworkClient.active || NetworkServer.active)
        {
            GUILayout.Label($"Lobby: {session.CurrentSteamLobbyId}");

            if (GUILayout.Button("Stop Session", GUILayout.Height(32f)))
            {
                session.StopNetwork();
            }

            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("LAN address");
        lanAddress = GUILayout.TextField(lanAddress);

        if (GUILayout.Button("Create LAN Session", GUILayout.Height(32f)))
        {
            session.StartLanHost();
        }

        if (GUILayout.Button("Join LAN Session", GUILayout.Height(32f)))
        {
            session.StartLanClient(lanAddress);
        }

        GUILayout.Space(12f);
        GUILayout.Label("Steam lobby ID");
        steamLobbyId = GUILayout.TextField(steamLobbyId);

        if (GUILayout.Button("Create Steam Lobby", GUILayout.Height(32f)))
        {
            session.StartSteamHost();
        }

        if (GUILayout.Button("Join Steam Lobby", GUILayout.Height(32f)))
        {
            session.JoinSteamLobby(steamLobbyId);
        }

        GUILayout.EndArea();
    }

    private void SetStatus(string message)
    {
        status = message;
    }
}

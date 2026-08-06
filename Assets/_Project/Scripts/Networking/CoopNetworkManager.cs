using System;
using System.Collections;
using Mirror;
using UnityEngine;

public sealed class CoopNetworkManager : NetworkManager
{
    public static event Action PlayersChanged;

    [Tooltip("Delay before restarting the session after every connected player has died.")]
    [SerializeField, Min(0f)]
    private float sessionRestartDelay = 1.5f;

    private Coroutine sessionRestartCoroutine;

    public override void Awake()
    {
        base.Awake();
    }

    public override void OnClientConnect()
    {
        if (NetworkServer.active)
        {
            return;
        }

        base.OnClientConnect();
    }

    public override void OnStartClient()
    {
        if (NetworkServer.active)
        {
            EnsureClientReadyAndPlayer();
        }
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        if (NetworkServer.active)
        {
            EnsureClientReadyAndPlayer();
        }
    }

    private void EnsureClientReadyAndPlayer()
    {
        if (!NetworkClient.isConnected || NetworkClient.connection == null)
        {
            return;
        }

        if (NetworkServer.active)
        {
            NetworkConnectionToClient localConnection =
                NetworkServer.localConnection;

            if (localConnection == null)
            {
                return;
            }

            if (!NetworkClient.ready)
            {
                NetworkClient.Ready();
            }

            if (!localConnection.isReady)
            {
                NetworkServer.SetClientReady(localConnection);
            }

            if (localConnection.identity == null)
            {
                OnServerAddPlayer(localConnection);
            }

            return;
        }

        if (!NetworkClient.connection.isAuthenticated)
        {
            return;
        }

        if (!NetworkClient.ready)
        {
            NetworkClient.Ready();
        }

        if (autoCreatePlayer && NetworkClient.localPlayer == null)
        {
            NetworkClient.AddPlayer();
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient connection)
    {
        Transform startPosition = GetStartPosition();
        GameObject player = startPosition == null
            ? Instantiate(playerPrefab)
            : Instantiate(playerPrefab, startPosition.position, startPosition.rotation);

        NetworkServer.AddPlayerForConnection(connection, player);

        player.GetComponent<CoopNetworkPlayer>()
            ?.SetLocalOwnershipState(connection == NetworkServer.localConnection);

        PlayersChanged?.Invoke();
    }

    public override void OnServerDisconnect(NetworkConnectionToClient connection)
    {
        if (connection.identity != null &&
            connection.identity.TryGetComponent(
                out CoopNetworkPlayer networkPlayer))
        {
            networkPlayer.ServerReleaseCarriedAnimal();
        }

        base.OnServerDisconnect(connection);
        PlayersChanged?.Invoke();
        EvaluateSessionState();
    }

    public void EvaluateSessionState()
    {
        if (!NetworkServer.active || sessionRestartCoroutine != null)
        {
            return;
        }

        int playerCount = 0;
        bool areAllPlayersDead = true;

        foreach (NetworkConnectionToClient connection in
                 NetworkServer.connections.Values)
        {
            if (connection == null || connection.identity == null)
            {
                continue;
            }

            playerCount++;

            if (!connection.identity.TryGetComponent(
                    out CoopNetworkHealth health) ||
                !health.IsDead)
            {
                areAllPlayersDead = false;
                break;
            }
        }

        if (playerCount > 0 && areAllPlayersDead)
        {
            sessionRestartCoroutine =
                StartCoroutine(RestartSessionRoutine());
        }
    }

    private IEnumerator RestartSessionRoutine()
    {
        yield return new WaitForSeconds(sessionRestartDelay);

        if (AreAllPlayersDead())
        {
            ResetSessionState();
        }

        sessionRestartCoroutine = null;
    }

    private bool AreAllPlayersDead()
    {
        int playerCount = 0;

        foreach (NetworkConnectionToClient connection in
                 NetworkServer.connections.Values)
        {
            if (connection == null || connection.identity == null)
            {
                continue;
            }

            playerCount++;

            if (!connection.identity.TryGetComponent(
                    out CoopNetworkHealth health) ||
                !health.IsDead)
            {
                return false;
            }
        }

        return playerCount > 0;
    }

    private void ResetSessionState()
    {
        foreach (NetworkConnectionToClient connection in
                 NetworkServer.connections.Values)
        {
            connection?.identity?.GetComponent<CoopNetworkPlayer>()
                ?.ServerReleaseCarriedAnimal();
        }

        foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
        {
            identity.GetComponent<CoopNetworkAnimal>()
                ?.ServerResetState();
            identity.GetComponent<CoopNetworkOldMan>()
                ?.ServerResetState();
            identity.GetComponent<CoopNetworkDoor>()
                ?.ServerResetState();
        }

        foreach (NetworkConnectionToClient connection in
                 NetworkServer.connections.Values)
        {
            connection?.identity?.GetComponent<CoopNetworkHealth>()
                ?.ServerResetState();
        }
    }
}

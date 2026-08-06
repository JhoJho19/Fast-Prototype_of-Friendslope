using System.Collections;
using Mirror;
using UnityEngine;

public sealed class CoopNetworkManager : NetworkManager
{
    [Tooltip("Delay before restarting the session after every connected player has died.")]
    [SerializeField, Min(0f)]
    private float sessionRestartDelay = 1.5f;

    private Coroutine sessionRestartCoroutine;

    public override void OnServerAddPlayer(NetworkConnectionToClient connection)
    {
        Transform startPosition = GetStartPosition();
        GameObject player = startPosition == null
            ? Instantiate(playerPrefab)
            : Instantiate(playerPrefab, startPosition.position, startPosition.rotation);

        NetworkServer.AddPlayerForConnection(connection, player);
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

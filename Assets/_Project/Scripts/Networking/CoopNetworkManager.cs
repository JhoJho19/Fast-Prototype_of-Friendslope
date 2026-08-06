using Mirror;
using UnityEngine;

public sealed class CoopNetworkManager : NetworkManager
{
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
    }
}

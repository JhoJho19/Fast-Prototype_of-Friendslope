using Mirror;
using UnityEngine;

public sealed class CoopNetworkHealth : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnDeadStateChanged))]
    private bool isDead;

    public void RequestDeath()
    {
        if (!NetworkClient.active)
        {
            GetComponent<PlayerHealth>()?.BeginNetworkDeath();
            return;
        }

        if (isServer)
        {
            ServerApplyDeath();
            return;
        }

        CmdRequestDeath();
    }

    [Command]
    private void CmdRequestDeath()
    {
        ServerApplyDeath();
    }

    private void ServerApplyDeath()
    {
        if (!isServer || isDead)
        {
            return;
        }

        isDead = true;
        ApplyDeathToLocalPlayer();
    }

    private void OnDeadStateChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            ApplyDeathToLocalPlayer();
        }
    }

    private void ApplyDeathToLocalPlayer()
    {
        if (isServer || isLocalPlayer)
        {
            GetComponent<PlayerHealth>()?.BeginNetworkDeath();
        }
    }
}

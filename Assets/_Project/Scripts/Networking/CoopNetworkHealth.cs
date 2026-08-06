using Mirror;
using UnityEngine;

public sealed class CoopNetworkHealth : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnDeadStateChanged))]
    private bool isDead;

    public bool IsDead => isDead;

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
        (NetworkManager.singleton as CoopNetworkManager)
            ?.EvaluateSessionState();
    }

    private void OnDeadStateChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            GetComponent<PlayerHealth>()?.SpawnRagdoll();
            ApplyDeathToLocalPlayer();
            return;
        }

        GetComponent<PlayerHealth>()?.DestroyRagdoll();
        if (isServer || isLocalPlayer)
        {
            GetComponent<PlayerHealth>()?.ResetForSession();
        }
    }

    public void ServerResetState()
    {
        if (!isServer)
        {
            return;
        }

        isDead = false;
        GetComponent<PlayerHealth>()?.ResetForSession();
    }

    private void ApplyDeathToLocalPlayer()
    {
        if (isServer || isLocalPlayer)
        {
            GetComponent<PlayerHealth>()?.BeginNetworkDeath();
        }
    }
}

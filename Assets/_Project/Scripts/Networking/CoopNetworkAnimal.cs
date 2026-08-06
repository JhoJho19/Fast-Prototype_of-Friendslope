using Mirror;
using UnityEngine;

public sealed class CoopNetworkAnimal : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnCarriedChanged))]
    private bool isCarried;

    [SyncVar(hook = nameof(OnCarrierChanged))]
    private uint carrierNetId;

    [SyncVar(hook = nameof(OnFrozenChanged))]
    private bool isFrozen;

    private CatchableAnimal animal;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private NetworkTransformBase networkTransform;

    public bool IsFrozen => isFrozen;

    private void Awake()
    {
        animal = GetComponent<CatchableAnimal>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        networkTransform = GetComponent<NetworkTransformBase>();
    }

    public override void OnStartClient()
    {
        ApplyNetworkState();

        if (isFrozen)
        {
            animal?.Freeze();
        }
        else
        {
            animal?.Unfreeze();
        }
    }

    [Server]
    public bool ServerFreeze()
    {
        if (animal == null || isCarried || isFrozen)
        {
            return false;
        }

        isFrozen = true;
        animal.Freeze();
        return true;
    }

    public bool ServerTryCarry(CoopNetworkPlayer player)
    {
        if (!isServer ||
            player == null ||
            animal == null ||
            isCarried)
        {
            return false;
        }

        Transform carryPoint = player.GetCarryPoint(animal.Kind);

        if (carryPoint == null ||
            !animal.BeginCarry(carryPoint))
        {
            return false;
        }

        isCarried = true;
        carrierNetId = player.netId;
        animal.SetCarryParent(carryPoint);
        ApplyNetworkState();
        return true;
    }

    public void ServerRelease(Vector3 playerPosition, Vector3 playerForward)
    {
        if (!isServer || !isCarried || animal == null)
        {
            return;
        }

        animal.Release(playerPosition, playerForward);
        isCarried = false;
        carrierNetId = 0;
        ApplyNetworkState();
    }

    public void ServerResetState()
    {
        if (!isServer || animal == null)
        {
            return;
        }

        isFrozen = false;
        animal.Unfreeze();

        if (isCarried)
        {
            animal.Release(startPosition, startRotation * Vector3.forward);
        }
        else
        {
            animal.RestoreOriginalParent();
        }

        transform.SetPositionAndRotation(startPosition, startRotation);

        networkTransform?.ServerTeleport(startPosition, startRotation);

        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.Warp(startPosition);
            agent.nextPosition = transform.position;
        }

        GetComponent<AnimalMovement>()?.ResetNavMeshBinding();
        GetComponent<AnimalStateMachine>()?.ResetForSession();

        isCarried = false;
        carrierNetId = 0;
        animal.ApplyNetworkVisualState(false);
        ApplyNetworkState();
    }

    private void OnCarriedChanged(bool oldValue, bool newValue)
    {
        ApplyNetworkState();
    }

    private void OnCarrierChanged(uint oldValue, uint newValue)
    {
        ApplyNetworkState();
    }

    private void OnFrozenChanged(bool oldValue, bool newValue)
    {
        if (animal == null)
        {
            return;
        }

        if (newValue)
        {
            animal.Freeze();
        }
        else
        {
            animal.Unfreeze();
        }
    }

    private void ApplyNetworkState()
    {
        if (animal == null)
        {
            return;
        }

        if (!isCarried)
        {
            animal.RestoreOriginalParent();
            animal.ApplyNetworkVisualState(false);
            return;
        }

        CoopNetworkPlayer carrier = ResolveCarrier();

        if (carrier != null)
        {
            Transform carryPoint = carrier.GetCarryPoint(animal.Kind);

            if (carryPoint != null)
            {
                animal.SetCarryParent(carryPoint);
            }
        }

        animal.ApplyNetworkVisualState(true);
    }

    private CoopNetworkPlayer ResolveCarrier()
    {
        if (carrierNetId == 0)
        {
            return null;
        }

        if (isServer &&
            NetworkServer.spawned.TryGetValue(
                carrierNetId,
                out NetworkIdentity serverIdentity))
        {
            return serverIdentity.GetComponent<CoopNetworkPlayer>();
        }

        if (NetworkClient.spawned.TryGetValue(
                carrierNetId,
                out NetworkIdentity clientIdentity))
        {
            return clientIdentity.GetComponent<CoopNetworkPlayer>();
        }

        return null;
    }
}

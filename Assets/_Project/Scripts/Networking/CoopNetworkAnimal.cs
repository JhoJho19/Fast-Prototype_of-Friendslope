using Mirror;
using UnityEngine;

public sealed class CoopNetworkAnimal : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnCarriedChanged))]
    private bool isCarried;

    [SyncVar(hook = nameof(OnCarrierChanged))]
    private uint carrierNetId;

    private CatchableAnimal animal;
    private Transform originalParent;
    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        animal = GetComponent<CatchableAnimal>();
        originalParent = transform.parent;
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public override void OnStartClient()
    {
        ApplyNetworkState();
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
        transform.SetParent(carryPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        ApplyNetworkState();
        return true;
    }

    public void ServerRelease(Vector3 playerPosition, Vector3 playerForward)
    {
        if (!isServer || !isCarried || animal == null)
        {
            return;
        }

        transform.SetParent(originalParent, true);
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

        if (isCarried)
        {
            transform.SetParent(originalParent, true);
            animal.Release(startPosition, startRotation * Vector3.forward);
        }
        else
        {
            transform.SetParent(originalParent, true);
        }

        transform.SetPositionAndRotation(startPosition, startRotation);

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

    private void ApplyNetworkState()
    {
        if (animal == null)
        {
            return;
        }

        if (!isCarried)
        {
            transform.SetParent(originalParent, true);
            animal.ApplyNetworkVisualState(false);
            return;
        }

        CoopNetworkPlayer carrier = ResolveCarrier();

        if (carrier != null)
        {
            Transform carryPoint = carrier.GetCarryPoint(animal.Kind);

            if (carryPoint != null)
            {
                transform.SetParent(carryPoint, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
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

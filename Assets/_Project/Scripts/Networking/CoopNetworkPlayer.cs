using Mirror;
using UnityEngine;

public sealed class CoopNetworkPlayer : NetworkBehaviour
{
    private const float CatchDistance = 5f;

    private Transform movementCapsule;
    private Vector3 capsuleLocalPosition;
    private Quaternion capsuleLocalRotation;
    private NetworkTransformBase movementNetworkTransform;
    private uint carriedAnimalNetId;

    private void Awake()
    {
        movementCapsule = FindChildByName("PlayerCapsule");
        movementNetworkTransform = GetComponent<NetworkTransformBase>();

        if (movementCapsule != null)
        {
            capsuleLocalPosition = movementCapsule.localPosition;
            capsuleLocalRotation = movementCapsule.localRotation;
        }

        ConfigureOwnerOnlyComponents(false);
    }

    public override void OnStartClient()
    {
        ConfigureOwnerOnlyComponents(isLocalPlayer);
    }

    public override void OnStartLocalPlayer()
    {
        ConfigureOwnerOnlyComponents(true);
    }

    public override void OnStartAuthority()
    {
        ConfigureOwnerOnlyComponents(isLocalPlayer);
    }

    public override void OnStopAuthority()
    {
        ConfigureOwnerOnlyComponents(false);
    }

    public void SetLocalOwnershipState(bool isOwner)
    {
        ConfigureOwnerOnlyComponents(isOwner);
    }

    public void ResetToPosition(Vector3 capsulePosition, Quaternion capsuleRotation)
    {
        if (movementCapsule == null)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.Euler(
            0f,
            capsuleRotation.eulerAngles.y,
            0f);

        Vector3 rootPosition =
            capsulePosition - targetRotation * capsuleLocalPosition;

        transform.SetPositionAndRotation(rootPosition, targetRotation);
        movementCapsule.localPosition = capsuleLocalPosition;
        movementCapsule.localRotation = capsuleLocalRotation;

        if (isServer && movementNetworkTransform != null)
        {
            movementNetworkTransform.ServerTeleport(
                rootPosition,
                targetRotation);
        }
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer || movementCapsule == null)
        {
            return;
        }

        Vector3 worldPosition = movementCapsule.position;
        Quaternion worldRotation = movementCapsule.rotation;

        Quaternion targetRotation = Quaternion.Euler(
            0f,
            worldRotation.eulerAngles.y,
            0f);

        transform.position =
            worldPosition - targetRotation * capsuleLocalPosition;
        transform.rotation = targetRotation;

        movementCapsule.localPosition = capsuleLocalPosition;
        movementCapsule.localRotation = capsuleLocalRotation;
    }

    public Transform GetCarryPoint(CatchableAnimalKind animalKind)
    {
        string pointName = animalKind switch
        {
            CatchableAnimalKind.Cat => "CatPoint",
            CatchableAnimalKind.Parrot => "ParrotPoint",
            _ => "DogPoint"
        };

        return FindChildByName(pointName);
    }

    public void RequestCatch(CatchableAnimal animal)
    {
        if (animal == null || !NetworkClient.active)
        {
            return;
        }

        NetworkIdentity animalIdentity =
            animal.GetComponent<NetworkIdentity>();

        if (animalIdentity == null)
        {
            return;
        }

        if (isServer)
        {
            ServerTryCatch(animalIdentity.netId);
            return;
        }

        CmdRequestCatch(animalIdentity.netId);
    }

    public void RequestReleaseAnimal()
    {
        if (!NetworkClient.active)
        {
            return;
        }

        if (isServer)
        {
            ServerReleaseCarriedAnimal();
            return;
        }

        CmdReleaseAnimal();
    }

    public Vector3 GetReleasePosition()
    {
        return movementCapsule != null
            ? movementCapsule.position
            : transform.position;
    }

    public Vector3 GetReleaseForward()
    {
        return movementCapsule != null
            ? movementCapsule.forward
            : transform.forward;
    }

    [Command]
    private void CmdRequestCatch(uint animalNetId)
    {
        ServerTryCatch(animalNetId);
    }

    [Command]
    private void CmdReleaseAnimal()
    {
        ServerReleaseCarriedAnimal();
    }

    [TargetRpc]
    private void TargetAnimalCarried(
        NetworkConnectionToClient target,
        uint animalNetId)
    {
        if (!NetworkClient.spawned.TryGetValue(
                animalNetId,
                out NetworkIdentity animalIdentity))
        {
            return;
        }

        CatchableAnimal animal =
            animalIdentity.GetComponent<CatchableAnimal>();

        GetComponentInChildren<PlayerAnimalCatchInteractor>(true)
            ?.ApplyNetworkCarry(animal);
    }

    [TargetRpc]
    private void TargetAnimalReleased(NetworkConnectionToClient target)
    {
        GetComponentInChildren<PlayerAnimalCatchInteractor>(true)
            ?.ApplyNetworkRelease();
    }

    public void ServerReleaseCarriedAnimal()
    {
        if (!isServer || carriedAnimalNetId == 0)
        {
            return;
        }

        if (NetworkServer.spawned.TryGetValue(
                carriedAnimalNetId,
                out NetworkIdentity animalIdentity))
        {
            CoopNetworkAnimal animal =
                animalIdentity.GetComponent<CoopNetworkAnimal>();

            animal?.ServerRelease(
                GetReleasePosition(),
                GetReleaseForward());
        }

        carriedAnimalNetId = 0;

        if (connectionToClient != null)
        {
            TargetAnimalReleased(connectionToClient);
        }
    }

    private void ServerTryCatch(uint animalNetId)
    {
        if (!isServer || carriedAnimalNetId != 0)
        {
            return;
        }

        if (!NetworkServer.spawned.TryGetValue(
                animalNetId,
                out NetworkIdentity animalIdentity))
        {
            return;
        }

        CoopNetworkAnimal animal =
            animalIdentity.GetComponent<CoopNetworkAnimal>();

        if (animal == null ||
            Vector3.Distance(transform.position, animal.transform.position) >
            CatchDistance ||
            !animal.ServerTryCarry(this))
        {
            return;
        }

        carriedAnimalNetId = animalNetId;

        if (connectionToClient != null)
        {
            TargetAnimalCarried(connectionToClient, animalNetId);
        }
    }

    private void ConfigureOwnerOnlyComponents(bool isOwner)
    {
        // Disable the whole camera objects so remote Cinemachine brains and virtual cameras cannot compete.
        SetOwnerOnlyGameObject("MainCamera", isOwner);
        SetOwnerOnlyGameObject("PlayerFollowCamera", isOwner);

        foreach (MonoBehaviour behaviour in
                 GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;

            if (typeName == "PlayerInput" ||
                typeName == "StarterAssetsInputs" ||
                typeName == "FirstPersonController" ||
                typeName == "PlayerInteractor" ||
                typeName == "PlayerDoorInteractor" ||
                typeName == "PlayerAnimalCatchInteractor" ||
                typeName == "PlayerInteractionHintPresenter")
            {
                behaviour.enabled = isOwner;
            }
        }

        foreach (Camera camera in GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = isOwner;
        }

        foreach (AudioListener listener in
                 GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = isOwner;
        }

        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
        {
            canvas.enabled = isOwner;
        }
    }

    private void SetOwnerOnlyGameObject(string objectName, bool isOwner)
    {
        Transform child = FindChildByName(objectName);

        if (child != null && child.gameObject.activeSelf != isOwner)
        {
            child.gameObject.SetActive(isOwner);
        }
    }

    private Transform FindChildByName(string childName)
    {
        foreach (Transform candidate in
                 GetComponentsInChildren<Transform>(true))
        {
            if (candidate != null && candidate.name == childName)
            {
                return candidate;
            }
        }

        return null;
    }
}

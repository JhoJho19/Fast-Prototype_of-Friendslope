using Mirror;
using UnityEngine;

public sealed class CoopNetworkDoor : NetworkBehaviour
{
    [SerializeField, Min(0.1f)]
    private float interactionDistance = 5f;

    [SyncVar(hook = nameof(OnOpenStateChanged))]
    private bool isOpen;

    private DoorBeh door;

    private void Awake()
    {
        door = GetComponent<DoorBeh>();
    }

    public override void OnStartServer()
    {
        isOpen = door != null && door.IsOpen;
    }

    public override void OnStartClient()
    {
        ApplyOpenState();
    }

    public void RequestToggle()
    {
        if (!NetworkClient.active)
        {
            door?.ToggleDoor();
            return;
        }

        if (isServer)
        {
            ServerToggle(NetworkServer.localConnection);
            return;
        }

        CmdToggle();
    }

    public void ServerResetState()
    {
        if (!isServer)
        {
            return;
        }

        isOpen = false;
        ApplyOpenState();
    }

    [Command(requiresAuthority = false)]
    private void CmdToggle(NetworkConnectionToClient sender = null)
    {
        ServerToggle(sender);
    }

    private void ServerToggle(NetworkConnectionToClient sender)
    {
        if (!isServer ||
            sender == null ||
            sender.identity == null ||
            Vector3.Distance(
                sender.identity.transform.position,
                transform.position) > interactionDistance)
        {
            return;
        }

        isOpen = !isOpen;
        ApplyOpenState();
    }

    private void OnOpenStateChanged(bool oldValue, bool newValue)
    {
        ApplyOpenState();
    }

    private void ApplyOpenState()
    {
        if (door == null)
        {
            return;
        }

        if (isOpen)
        {
            door.OpenDoor();
        }
        else
        {
            door.CloseDoor();
        }
    }
}

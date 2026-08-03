using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class DoorInteractionZone : MonoBehaviour
{
    [SerializeField] private DoorBeh door;

    private void Reset()
    {
        FindDoor();
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        FindDoor();
        ConfigureTrigger();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (door == null || other == null)
        {
            return;
        }

        IDoorUser doorUser = FindDoorUser(other);

        if (doorUser == null)
        {
            return;
        }

        doorUser.HandleDoor(door);
    }

    private void FindDoor()
    {
        if (door == null)
        {
            door = GetComponentInParent<DoorBeh>();
        }
    }

    private void ConfigureTrigger()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();

        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private static IDoorUser FindDoorUser(Component component)
    {
        IDoorUser doorUser =
            component.GetComponent(typeof(IDoorUser)) as IDoorUser;

        if (doorUser != null)
        {
            return doorUser;
        }

        doorUser =
            component.GetComponentInParent(typeof(IDoorUser)) as IDoorUser;

        if (doorUser != null)
        {
            return doorUser;
        }

        return component.GetComponentInParent<OldManDoorUser>();
    }
}

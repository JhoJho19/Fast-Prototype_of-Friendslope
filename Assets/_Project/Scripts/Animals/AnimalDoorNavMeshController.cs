using Unity.AI.Navigation;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnimalDoorNavMeshController : MonoBehaviour
{
    [SerializeField] private DoorBeh door;
    [SerializeField] private NavMeshLink dogDoorLink;
    [SerializeField] private NavMeshLink catDoorLink;
    [SerializeField] private NavMeshLink parrotDoorLink;

    private void Reset()
    {
        FindLinks();
    }

    private void Awake()
    {
        FindLinks();
        RefreshLinks();
    }

    private void OnEnable()
    {
        if (door == null)
        {
            Debug.LogWarning(
                $"{name} needs a DoorBeh reference to control animal NavMesh links.",
                this);
            SetLinks(false);
            return;
        }

        door.OpeningStarted += DisableLinks;
        door.Opened += EnableLinks;
        door.ClosingStarted += DisableLinks;
        door.Closed += DisableLinks;
        RefreshLinks();
    }

    private void OnDisable()
    {
        if (door != null)
        {
            door.OpeningStarted -= DisableLinks;
            door.Opened -= EnableLinks;
            door.ClosingStarted -= DisableLinks;
            door.Closed -= DisableLinks;
        }

        SetLinks(false);
    }

    private void RefreshLinks()
    {
        SetLinks(door != null && door.IsOpen && !door.IsMoving);
    }

    private void EnableLinks()
    {
        SetLinks(true);
    }

    private void DisableLinks()
    {
        SetLinks(false);
    }

    private void SetLinks(bool active)
    {
        if (dogDoorLink != null)
        {
            dogDoorLink.activated = active;
        }

        if (catDoorLink != null)
        {
            catDoorLink.activated = active;
        }

        if (parrotDoorLink != null)
        {
            parrotDoorLink.activated = active;
        }
    }

    private void FindLinks()
    {
        foreach (NavMeshLink link in GetComponentsInChildren<NavMeshLink>(true))
        {
            if (link.name.StartsWith("DogDoorLink"))
            {
                dogDoorLink = link;
            }
            else if (link.name.StartsWith("CatDoorLink"))
            {
                catDoorLink = link;
            }
            else if (link.name.StartsWith("ParrotDoorLink"))
            {
                parrotDoorLink = link;
            }
        }
    }
}

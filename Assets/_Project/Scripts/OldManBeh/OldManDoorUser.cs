using UnityEngine;
using UnityEngine.AI;

public sealed class OldManDoorUser : MonoBehaviour, IDoorUser
{
    [SerializeField] private NavMeshAgent agent;

    private DoorBeh pendingDoor;
    private bool wasMovingBeforeInteraction;
    private bool stoppedByThisComponent;

    private void Awake()
    {
        FindAgent();
    }

    private void Reset()
    {
        FindAgent();
    }

    private void OnValidate()
    {
        FindAgent();
    }

    public void HandleDoor(DoorBeh door)
    {
        if (door == null || agent == null || pendingDoor != null)
        {
            return;
        }

        if (door.IsOpen)
        {
            return;
        }

        pendingDoor = door;
        wasMovingBeforeInteraction = IsAgentMoving();
        stoppedByThisComponent =
            wasMovingBeforeInteraction && !agent.isStopped;

        if (stoppedByThisComponent)
        {
            agent.isStopped = true;
        }

        pendingDoor.Opened += OnDoorOpened;
        pendingDoor.OpenDoor();
    }

    private void OnDoorOpened()
    {
        bool shouldResumeAgent = CanResumeAgent();

        StopWaitingForDoor();

        if (shouldResumeAgent)
        {
            agent.isStopped = false;
        }
    }

    private bool IsAgentMoving()
    {
        return agent.isActiveAndEnabled &&
               agent.isOnNavMesh &&
               !agent.isStopped &&
               agent.hasPath &&
               !agent.pathPending &&
               agent.remainingDistance > agent.stoppingDistance;
    }

    private bool CanResumeAgent()
    {
        return wasMovingBeforeInteraction &&
               stoppedByThisComponent &&
               isActiveAndEnabled &&
               gameObject.activeInHierarchy &&
               agent != null &&
               agent.isActiveAndEnabled &&
               agent.isOnNavMesh &&
               agent.hasPath &&
               !agent.pathPending &&
               agent.isStopped;
    }

    private void FindAgent()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    private void OnDisable()
    {
        StopWaitingForDoor();
    }

    private void OnDestroy()
    {
        StopWaitingForDoor();
    }

    private void StopWaitingForDoor()
    {
        if (pendingDoor != null)
        {
            pendingDoor.Opened -= OnDoorOpened;
        }

        pendingDoor = null;
        wasMovingBeforeInteraction = false;
        stoppedByThisComponent = false;
    }
}

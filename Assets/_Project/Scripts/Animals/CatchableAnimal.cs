using UnityEngine;
using UnityEngine.AI;

public enum CatchableAnimalKind
{
    Dog,
    Cat,
    Parrot
}

[DisallowMultipleComponent]
public sealed class CatchableAnimal : MonoBehaviour
{
    [SerializeField] private CatchableAnimalKind animalKind;
    [SerializeField] private AnimalStateMachine stateMachine;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Collider[] catchColliders;
    [SerializeField, Min(0.1f)] private float releaseDistanceFromPlayer = 1.5f;
    [SerializeField, Min(0.1f)] private float releaseNavMeshSampleDistance = 2f;

    private Transform originalParent;
    private bool isCarried;

    public CatchableAnimalKind Kind => animalKind;
    public bool IsCarried => isCarried;

    public bool CanBeCaught =>
        !isCarried &&
        isActiveAndEnabled &&
        gameObject.activeInHierarchy &&
        stateMachine != null;

    private void Reset()
    {
        FindReferences();
        GuessAnimalKind();
        CollectCatchColliders();
    }

    private void OnValidate()
    {
        FindReferences();
        GuessAnimalKind();
        CollectCatchColliders();
    }

    private void Awake()
    {
        FindReferences();
        GuessAnimalKind();

        if (catchColliders == null ||
            catchColliders.Length == 0)
        {
            CollectCatchColliders();
        }

        originalParent = transform.parent;
    }

    public bool BeginCarry(Transform carryPoint)
    {
        if (!CanBeCaught || carryPoint == null)
        {
            return false;
        }

        originalParent = transform.parent;
        stateMachine.SetState(AnimalState.Carried);

        SetCatchCollidersEnabled(false);

        if (agent != null &&
            agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        transform.SetParent(carryPoint, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        isCarried = true;
        return true;
    }

    public void Release(Vector3 playerPosition, Vector3 playerForward)
    {
        if (!isCarried)
        {
            return;
        }

        Transform restoreParent = originalParent;
        transform.SetParent(restoreParent, true);

        Vector3 releasePosition =
            ResolveReleasePosition(playerPosition, playerForward);

        transform.position = releasePosition;

        if (agent != null &&
            !agent.enabled)
        {
            agent.enabled = true;
        }

        if (agent != null &&
            agent.isActiveAndEnabled)
        {
            agent.Warp(releasePosition);
            agent.nextPosition = releasePosition;
        }

        SetCatchCollidersEnabled(true);

        isCarried = false;

        if (stateMachine == null)
        {
            return;
        }

        FleeFrom(playerPosition);
    }

    public void FleeFrom(Vector3 sourcePosition)
    {
        if (isCarried ||
            stateMachine == null)
        {
            return;
        }

        stateMachine.SetFleeSource(sourcePosition);

        if (stateMachine.CurrentState != AnimalState.Flee)
        {
            stateMachine.SetState(AnimalState.Flee);
        }
    }

    private void FindReferences()
    {
        if (stateMachine == null)
        {
            stateMachine = GetComponent<AnimalStateMachine>();
        }

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    private void GuessAnimalKind()
    {
        string lowerName = name.ToLowerInvariant();

        if (lowerName.Contains("cat"))
        {
            animalKind = CatchableAnimalKind.Cat;
            return;
        }

        if (lowerName.Contains("parrot") ||
            lowerName.Contains("parot"))
        {
            animalKind = CatchableAnimalKind.Parrot;
            return;
        }

        if (lowerName.Contains("dog"))
        {
            animalKind = CatchableAnimalKind.Dog;
        }
    }

    private void CollectCatchColliders()
    {
        catchColliders = GetComponentsInChildren<Collider>(true);
    }

    private Vector3 ResolveReleasePosition(
        Vector3 playerPosition,
        Vector3 playerForward)
    {
        Vector3 flattenedForward = Vector3.ProjectOnPlane(playerForward, Vector3.up);

        if (flattenedForward.sqrMagnitude <= 0.0001f)
        {
            flattenedForward = Vector3.forward;
        }

        flattenedForward.Normalize();

        Vector3 desiredPosition =
            playerPosition + flattenedForward * releaseDistanceFromPlayer;

        if (TryResolveGroundPosition(
                desiredPosition,
                out Vector3 groundedPosition))
        {
            desiredPosition = groundedPosition;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent != null ? agent.agentTypeID : 0,
            areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas
        };

        if (NavMesh.SamplePosition(
                desiredPosition,
                out NavMeshHit desiredHit,
                releaseNavMeshSampleDistance,
                filter))
        {
            return desiredHit.position;
        }

        if (NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit fallbackHit,
                releaseNavMeshSampleDistance,
                filter))
        {
            return fallbackHit.position;
        }

        return desiredPosition;
    }

    private bool TryResolveGroundPosition(
        Vector3 desiredPosition,
        out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * 2f;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                6f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            groundedPosition = hit.point;
            return true;
        }

        groundedPosition = desiredPosition;
        return false;
    }

    private void SetCatchCollidersEnabled(bool enabled)
    {
        if (catchColliders == null)
        {
            return;
        }

        foreach (Collider catchCollider in catchColliders)
        {
            if (catchCollider != null)
            {
                catchCollider.enabled = enabled;
            }
        }
    }
}

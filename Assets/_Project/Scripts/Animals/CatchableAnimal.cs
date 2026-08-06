using System.Collections.Generic;
using Mirror;
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
    [SerializeField] private Renderer[] animalRenderers;
    [SerializeField, Min(0.1f)] private float releaseDistanceFromPlayer = 1.5f;
    [SerializeField, Min(0.1f)] private float releaseNavMeshSampleDistance = 2f;

    private Transform originalParent;
    private Vector3 originalLocalScale;
    private Vector3 originalWorldScale;
    private bool isCarried;
    private bool isFrozen;
    private readonly List<FrozenBehaviourState> frozenBehaviours =
        new List<FrozenBehaviourState>();

    private struct FrozenBehaviourState
    {
        public Behaviour Behaviour;
        public bool WasEnabled;
    }

    public CatchableAnimalKind Kind => animalKind;
    public bool IsCarried => isCarried;
    public bool IsFrozen => isFrozen;

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
        CollectAnimalRenderers();
    }

    private void OnValidate()
    {
        FindReferences();
        GuessAnimalKind();
        CollectCatchColliders();
        CollectAnimalRenderers();
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

        if (animalRenderers == null ||
            animalRenderers.Length == 0)
        {
            CollectAnimalRenderers();
        }

        originalParent = transform.parent;
        originalLocalScale = transform.localScale;
        originalWorldScale = transform.lossyScale;
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

        if (stateMachine != null)
        {
            stateMachine.Movement.ResetNavMeshBinding();
        }

        SetCarryParent(carryPoint);
        SetAnimalRenderersEnabled(false);

        isCarried = true;
        return true;
    }

    public void SetCarryParent(Transform carryPoint)
    {
        if (carryPoint == null)
        {
            return;
        }

        transform.SetParent(carryPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        SetWorldScale(originalWorldScale);
    }

    public void RestoreOriginalParent()
    {
        transform.SetParent(originalParent, true);
        transform.localScale = originalLocalScale;
    }

    public void Release(Vector3 playerPosition, Vector3 playerForward)
    {
        if (!isCarried)
        {
            return;
        }

        RestoreOriginalParent();

        Vector3 releasePosition =
            ResolveReleasePosition(playerPosition, playerForward);

        transform.position = releasePosition;

        if (agent != null &&
            !agent.enabled)
        {
            agent.enabled = true;
        }

        if (stateMachine != null)
        {
            stateMachine.Movement.ResetNavMeshBinding();
        }

        if (stateMachine != null &&
            stateMachine.Movement.TrySnapToNavMesh(
                releasePosition,
                releaseNavMeshSampleDistance))
        {
            releasePosition = transform.position;
        }
        else if (agent != null &&
                 agent.isActiveAndEnabled)
        {
            agent.Warp(releasePosition);
        }

        if (agent != null &&
            agent.isActiveAndEnabled)
        {
            agent.nextPosition = transform.position;
        }

        SetCatchCollidersEnabled(true);
        SetAnimalRenderersEnabled(true);

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

    public void ApplyNetworkVisualState(bool carried)
    {
        SetCatchCollidersEnabled(!carried);
        SetAnimalRenderersEnabled(!carried);
    }

    public void Freeze()
    {
        if (isFrozen || isCarried)
        {
            return;
        }

        isFrozen = true;

        frozenBehaviours.Clear();

        Behaviour[] behaviours =
            GetComponentsInChildren<Behaviour>(true);

        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == null ||
                behaviour == this ||
                behaviour is NetworkBehaviour)
            {
                continue;
            }

            frozenBehaviours.Add(new FrozenBehaviourState
            {
                Behaviour = behaviour,
                WasEnabled = behaviour.enabled
            });
        }

        if (agent != null &&
            agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        if (stateMachine != null)
        {
            stateMachine.Movement.ResetNavMeshBinding();
        }

        foreach (FrozenBehaviourState frozenBehaviour in frozenBehaviours)
        {
            if (frozenBehaviour.Behaviour != null)
            {
                frozenBehaviour.Behaviour.enabled = false;
            }
        }

        enabled = false;
    }

    public void Unfreeze()
    {
        if (!isFrozen)
        {
            return;
        }

        isFrozen = false;
        enabled = true;

        foreach (FrozenBehaviourState frozenBehaviour in frozenBehaviours)
        {
            if (frozenBehaviour.Behaviour != null)
            {
                frozenBehaviour.Behaviour.enabled = frozenBehaviour.WasEnabled;
            }
        }

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.ResetPath();
            agent.nextPosition = transform.position;
        }

        frozenBehaviours.Clear();
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

    private void CollectAnimalRenderers()
    {
        animalRenderers = GetComponentsInChildren<Renderer>(true);
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

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent != null ? agent.agentTypeID : 0,
            areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas
        };

        float sampleDistance = releaseNavMeshSampleDistance;

        if (TryResolveGroundPosition(
                desiredPosition,
                out Vector3 groundedPosition))
        {
            return groundedPosition;
        }

        if (NavMesh.SamplePosition(
                desiredPosition,
                out NavMeshHit desiredHit,
                sampleDistance,
                filter))
        {
            return desiredHit.position;
        }

        if (NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit fallbackHit,
                sampleDistance,
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
        Vector3 rayOrigin = desiredPosition + Vector3.up * 0.5f;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                releaseNavMeshSampleDistance + 1f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) &&
            hit.normal.y > 0.5f)
        {
            groundedPosition = hit.point;
            return true;
        }

        groundedPosition = desiredPosition;
        return false;
    }

    private void SetWorldScale(Vector3 worldScale)
    {
        Vector3 parentScale = transform.parent != null
            ? transform.parent.lossyScale
            : Vector3.one;

        transform.localScale = new Vector3(
            GetScaleComponent(worldScale.x, parentScale.x),
            GetScaleComponent(worldScale.y, parentScale.y),
            GetScaleComponent(worldScale.z, parentScale.z));
    }

    private static float GetScaleComponent(float worldScale, float parentScale)
    {
        return Mathf.Abs(parentScale) > Mathf.Epsilon
            ? worldScale / parentScale
            : worldScale;
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

    private void SetAnimalRenderersEnabled(bool enabled)
    {
        if (animalRenderers == null)
        {
            return;
        }

        foreach (Renderer animalRenderer in animalRenderers)
        {
            if (animalRenderer != null)
            {
                animalRenderer.enabled = enabled;
            }
        }
    }
}

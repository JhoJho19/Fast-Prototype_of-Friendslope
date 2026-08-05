using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class AnimalMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float arrivalDistanceTolerance = 0.05f;
    [SerializeField, Min(0.01f)] private float navMeshSnapDistance = 0.25f;
    [SerializeField, Min(0f)] private float movementSpeedThreshold = 0.01f;

    private NavMeshAgent agent;
    private NavMeshPath calculatedPath;
    private Vector3 currentDestination;
    private float defaultSpeed;
    private float speedMultiplier = 1f;
    private bool hasRequestedRoute;
    private bool hasTriedInitialSnap;
    private bool warnedAboutMissingNavMesh;

    public bool IsMoving =>
        IsAgentOnNavMesh &&
        !agent.isStopped &&
        agent.hasPath &&
        !agent.pathPending &&
        CurrentSpeed > movementSpeedThreshold;

    public bool HasReachedDestination
    {
        get
        {
            if (!IsAgentOnNavMesh || !hasRequestedRoute || agent.pathPending)
            {
                return false;
            }

            float arrivalDistance = agent.stoppingDistance + arrivalDistanceTolerance;

            if (agent.hasPath)
            {
                return agent.remainingDistance <= arrivalDistance;
            }

            return Vector3.Distance(transform.position, currentDestination) <= arrivalDistance;
        }
    }

    public bool HasPath => IsAgentOnNavMesh && agent.hasPath;

    public NavMeshPathStatus PathStatus =>
        IsAgentOnNavMesh ? agent.pathStatus : NavMeshPathStatus.PathInvalid;

    public float CurrentSpeed
    {
        get
        {
            if (agent == null)
            {
                return 0f;
            }

            return Mathf.Max(agent.velocity.magnitude, agent.desiredVelocity.magnitude);
        }
    }

    private bool IsAgentOnNavMesh =>
        agent != null &&
        agent.isActiveAndEnabled &&
        agent.isOnNavMesh;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        calculatedPath = new NavMeshPath();
        defaultSpeed = agent.speed;
        agent.updatePosition = true;
        agent.updateRotation = true;
        ApplySpeedMultiplier();
    }

    private void OnEnable()
    {
        hasTriedInitialSnap = false;
        ApplySpeedMultiplier();
    }

    private void OnDisable()
    {
        Stop();
    }

    public bool TryMoveTo(Vector3 destination)
    {
        if (!EnsureOnNavMesh())
        {
            return false;
        }

        if (!agent.CalculatePath(destination, calculatedPath) ||
            calculatedPath.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        if (!agent.SetPath(calculatedPath))
        {
            return false;
        }

        currentDestination = destination;
        hasRequestedRoute = true;
        agent.isStopped = false;
        return true;
    }

    public bool CanReach(Vector3 destination)
    {
        if (!EnsureOnNavMesh())
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        return agent.CalculatePath(destination, path) &&
               path.status == NavMeshPathStatus.PathComplete;
    }

    public void Pause()
    {
        if (IsAgentOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    public void Resume()
    {
        if (IsAgentOnNavMesh && agent.hasPath)
        {
            agent.isStopped = false;
        }
    }

    public void Stop()
    {
        hasRequestedRoute = false;

        if (!IsAgentOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
        agent.nextPosition = transform.position;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.01f, multiplier);
        ApplySpeedMultiplier();
    }

    public void ResetNavMeshBinding()
    {
        hasRequestedRoute = false;
        hasTriedInitialSnap = false;
        warnedAboutMissingNavMesh = false;
    }

    public bool TrySnapToNavMesh(
        Vector3 targetPosition,
        float searchDistance)
    {
        ResetNavMeshBinding();

        if (agent == null ||
            !agent.isActiveAndEnabled)
        {
            return false;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        float sampleDistance =
            Mathf.Max(
                navMeshSnapDistance,
                searchDistance,
                agent.baseOffset + navMeshSnapDistance);

        if (!NavMesh.SamplePosition(
                GetNavMeshSampleOrigin(targetPosition),
                out NavMeshHit navMeshHit,
                sampleDistance,
                filter))
        {
            return false;
        }

        return agent.Warp(navMeshHit.position) &&
               agent.isOnNavMesh;
    }

    private bool EnsureOnNavMesh()
    {
        if (IsAgentOnNavMesh)
        {
            return true;
        }

        if (agent == null || !agent.isActiveAndEnabled)
        {
            WarnAboutMissingNavMesh();
            return false;
        }

        hasTriedInitialSnap = true;

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        if (NavMesh.SamplePosition(
                GetNavMeshSampleOrigin(transform.position),
                out NavMeshHit navMeshHit,
                Mathf.Max(
                    navMeshSnapDistance,
                    agent.baseOffset + navMeshSnapDistance),
                filter) &&
            agent.Warp(navMeshHit.position))
        {
            return agent.isOnNavMesh;
        }

        WarnAboutMissingNavMesh();
        return false;
    }

    private void WarnAboutMissingNavMesh()
    {
        if (warnedAboutMissingNavMesh)
        {
            return;
        }

        warnedAboutMissingNavMesh = true;
        Debug.LogWarning(
            $"{name} cannot start animal navigation because no matching NavMesh was found nearby.",
            this);
    }

    private void ApplySpeedMultiplier()
    {
        if (agent == null)
        {
            return;
        }

        agent.speed = defaultSpeed * speedMultiplier;
    }

    private Vector3 GetNavMeshSampleOrigin(Vector3 targetPosition)
    {
        if (agent == null)
        {
            return targetPosition;
        }

        float verticalOffset =
            Mathf.Max(agent.baseOffset, agent.height * 0.5f);

        return targetPosition + Vector3.up * verticalOffset;
    }
}

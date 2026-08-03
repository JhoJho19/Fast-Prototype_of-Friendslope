using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class OldManStateMachine : MonoBehaviour
{
    public enum OldManState
    {
        Patrol,
        Chasing,
        Aiming,
        Shooting
    }

    private const string SpeedParameter = "Speed";
    private const string PatrolTrigger = "Patrol";
    private const string AimTrigger = "Aim";
    private const string ShootTrigger = "Shoot";
    private const float DoorCheckInterval = 0.1f;
    private const float DoorCheckDistance = 2f;
    private const float DoorOpenCooldown = 1f;
    private const float PatrolPointWaitDuration = 2f;
    private const float ArrivalDistanceTolerance = 0.05f;
    private const float NavMeshSnapDistance = 0.75f;

    [SerializeField] private Transform[] patrolPoints;

    private readonly Dictionary<DoorBeh, float> doorOpenTimes = new();

    private NavMeshAgent agent;
    private Animator animator;
    private OldManState currentState;
    private int currentPatrolPointIndex = -1;
    private float nextDoorCheckTime;
    private float patrolWaitEndTime;
    private bool isWaitingAtPatrolPoint;
    private bool missingPatrolPointsWarningShown;

    public OldManState CurrentState => currentState;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        agent.updatePosition = false;
        agent.updateRotation = false;
        animator.applyRootMotion = true;
    }

    private void Start()
    {
        SetState(OldManState.Patrol);
    }

    private void Update()
    {
        SynchronizeAgentWithRootMotion();

        if (currentState == OldManState.Patrol)
        {
            UpdatePatrol();
        }

        UpdateAnimationState();
    }

    private void OnAnimatorMove()
    {
        Vector3 rootMotionPosition = animator.rootPosition;

        if (agent.isOnNavMesh &&
            NavMesh.SamplePosition(
                rootMotionPosition,
                out NavMeshHit navMeshHit,
                NavMeshSnapDistance,
                agent.areaMask))
        {
            rootMotionPosition.y = navMeshHit.position.y;
            agent.nextPosition = rootMotionPosition;
        }

        transform.position = rootMotionPosition;
    }

    public void SetState(OldManState newState)
    {
        currentState = newState;

        switch (currentState)
        {
            case OldManState.Patrol:
                isWaitingAtPatrolPoint = false;
                agent.isStopped = false;
                animator.ResetTrigger(AimTrigger);
                animator.ResetTrigger(ShootTrigger);
                animator.SetTrigger(PatrolTrigger);
                SelectNextPatrolPoint();
                break;

            case OldManState.Chasing:
                // Placeholder for target pursuit.
                StopAgent();
                break;

            case OldManState.Aiming:
                // Placeholder for target selection and aiming logic.
                StopAgent();
                animator.SetTrigger(AimTrigger);
                break;

            case OldManState.Shooting:
                // Placeholder for weapon and damage logic.
                StopAgent();
                animator.SetTrigger(ShootTrigger);
                break;
        }
    }

    private void UpdatePatrol()
    {
        if (isWaitingAtPatrolPoint)
        {
            if (Time.time >= patrolWaitEndTime)
            {
                isWaitingAtPatrolPoint = false;
                SelectNextPatrolPoint();
            }

            return;
        }

        if (agent.pathPending)
        {
            return;
        }

        if (HasReachedPatrolDestination())
        {
            StartPatrolPointWait();
            return;
        }

        if (Time.time < nextDoorCheckTime)
        {
            return;
        }

        nextDoorCheckTime = Time.time + DoorCheckInterval;
        TryOpenDoorOnCurrentPath();
    }

    private bool HasReachedPatrolDestination()
    {
        return agent.hasPath &&
               agent.remainingDistance <= agent.stoppingDistance + ArrivalDistanceTolerance;
    }

    private void StartPatrolPointWait()
    {
        isWaitingAtPatrolPoint = true;
        patrolWaitEndTime = Time.time + PatrolPointWaitDuration;
        agent.isStopped = true;
        agent.ResetPath();
        agent.nextPosition = transform.position;
    }

    private void SelectNextPatrolPoint()
    {
        int validPatrolPointCount = CountValidPatrolPoints();
        if (validPatrolPointCount == 0)
        {
            WarnAboutMissingPatrolPoints();
            StopAgent();
            return;
        }

        int nextPatrolPointIndex;
        do
        {
            nextPatrolPointIndex = UnityEngine.Random.Range(0, patrolPoints.Length);
        }
        while (patrolPoints[nextPatrolPointIndex] == null ||
               validPatrolPointCount > 1 && nextPatrolPointIndex == currentPatrolPointIndex);

        currentPatrolPointIndex = nextPatrolPointIndex;
        agent.isStopped = false;
        agent.SetDestination(patrolPoints[currentPatrolPointIndex].position);
    }

    private int CountValidPatrolPoints()
    {
        if (patrolPoints == null)
        {
            return 0;
        }

        int validPatrolPointCount = 0;
        foreach (Transform patrolPoint in patrolPoints)
        {
            if (patrolPoint != null)
            {
                validPatrolPointCount++;
            }
        }

        return validPatrolPointCount;
    }

    private void TryOpenDoorOnCurrentPath()
    {
        if (!agent.hasPath)
        {
            return;
        }

        Vector3 pathDirection = agent.steeringTarget - agent.nextPosition;
        pathDirection.y = 0f;

        if (pathDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 checkOrigin = agent.nextPosition + Vector3.up * (agent.height * 0.5f);
        if (!Physics.SphereCast(
                checkOrigin,
                agent.radius,
                pathDirection.normalized,
                out RaycastHit hit,
                DoorCheckDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        DoorBeh door = hit.collider.GetComponentInParent<DoorBeh>();
        if (door == null || door.gameObject.name.IndexOf("Door", StringComparison.Ordinal) < 0)
        {
            return;
        }

        if (doorOpenTimes.TryGetValue(door, out float lastOpenTime) &&
            Time.time - lastOpenTime < DoorOpenCooldown)
        {
            return;
        }

        doorOpenTimes[door] = Time.time;
        door.OpenDoor();
    }

    private void UpdateAnimationState()
    {
        bool isMovingAlongPath = currentState == OldManState.Patrol &&
                                  !isWaitingAtPatrolPoint &&
                                  !agent.isStopped &&
                                  agent.hasPath &&
                                  !agent.pathPending;

        animator.SetFloat(SpeedParameter, isMovingAlongPath ? 1f : 0f);
    }

    private void SynchronizeAgentWithRootMotion()
    {
        if (!agent.isOnNavMesh)
        {
            return;
        }

        if (agent.isStopped || !agent.hasPath || agent.pathPending)
        {
            return;
        }

        Vector3 pathDirection = agent.steeringTarget - transform.position;
        pathDirection.y = 0f;
        if (pathDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(pathDirection.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            agent.angularSpeed * Time.deltaTime);
    }

    private void StopAgent()
    {
        isWaitingAtPatrolPoint = false;
        agent.isStopped = true;
        agent.ResetPath();
        agent.nextPosition = transform.position;
    }

    private void WarnAboutMissingPatrolPoints()
    {
        if (missingPatrolPointsWarningShown)
        {
            return;
        }

        Debug.LogWarning("OldMan requires at least one patrol point.", this);
        missingPatrolPointsWarningShown = true;
    }
}

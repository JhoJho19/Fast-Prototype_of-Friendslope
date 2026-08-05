using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class OldManMovement : MonoBehaviour
{
    [SerializeField] private float arrivalDistanceTolerance = 0.05f;
    [SerializeField] private float navMeshSnapDistance = 0.5f;

    private NavMeshAgent agent;
    private Animator animator;

    public bool IsMoving =>
        IsAgentOnNavMesh &&
        !agent.isStopped &&
        agent.hasPath &&
        !agent.pathPending;

    public bool HasReachedDestination =>
        IsAgentOnNavMesh &&
        agent.hasPath &&
        !agent.pathPending &&
        agent.remainingDistance <=
        agent.stoppingDistance + arrivalDistanceTolerance;

    public bool HasPath => IsAgentOnNavMesh && agent.hasPath;

    private bool IsAgentOnNavMesh =>
        agent != null &&
        agent.isActiveAndEnabled &&
        agent.isOnNavMesh;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        agent.updatePosition = false;
        agent.updateRotation = false;
        animator.applyRootMotion = true;
    }

    private void Update()
    {
        SynchronizeAgentWithRootMotion();
    }

    private void OnAnimatorMove()
    {
        Vector3 rootMotionPosition = animator.rootPosition;

        if (agent == null ||
            !agent.isActiveAndEnabled ||
            agent.isStopped ||
            !agent.hasPath ||
            agent.pathPending)
        {
            return;
        }

        if (NavMesh.SamplePosition(
                rootMotionPosition,
                out NavMeshHit navMeshHit,
                navMeshSnapDistance,
                agent.areaMask))
        {
            transform.position = navMeshHit.position;
            agent.nextPosition = navMeshHit.position;
            return;
        }

        transform.position = rootMotionPosition;
        agent.nextPosition = rootMotionPosition;
    }

    public void MoveTo(Vector3 destination)
    {
        if (!IsAgentOnNavMesh)
        {
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    public void Pause()
    {
        if (!IsAgentOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
    }

    public void Resume()
    {
        if (!IsAgentOnNavMesh || !agent.hasPath)
        {
            return;
        }

        agent.isStopped = false;
    }

    public void Stop()
    {
        if (!IsAgentOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
        agent.nextPosition = transform.position;
    }

    private void SynchronizeAgentWithRootMotion()
    {
        if (!IsMoving)
        {
            return;
        }

        Vector3 pathDirection = agent.steeringTarget - transform.position;

        pathDirection.y = 0f;

        if (pathDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(pathDirection.normalized);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            agent.angularSpeed * Time.deltaTime);
    }
}

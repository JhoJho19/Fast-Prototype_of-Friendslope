using Mirror;
using UnityEngine;
using UnityEngine.AI;

public sealed class CoopNetworkOldMan : NetworkBehaviour
{
    private Vector3 startPosition;
    private Quaternion startRotation;
    private NavMeshAgent agent;
    private OldManStateMachine stateMachine;
    private OldManPatrol patrol;
    private OldManMovement movement;
    private OldManCombat combat;
    private OldManAnimation animationController;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        agent = GetComponent<NavMeshAgent>();
        stateMachine = GetComponent<OldManStateMachine>();
        patrol = GetComponent<OldManPatrol>();
        movement = GetComponent<OldManMovement>();
        combat = GetComponent<OldManCombat>();
        animationController = GetComponent<OldManAnimation>();
    }

    public void ServerResetState()
    {
        if (!isServer)
        {
            return;
        }

        combat?.Abort();
        movement?.Stop();

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.ResetPath();
            agent.Warp(startPosition);
        }

        transform.SetPositionAndRotation(startPosition, startRotation);

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.nextPosition = transform.position;
        }

        patrol?.ResetForSession();
        stateMachine?.SetState(OldManStateMachine.OldManState.Patrol);
        animationController?.PlayPatrol();
    }
}

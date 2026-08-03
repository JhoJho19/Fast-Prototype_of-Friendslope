using UnityEngine;

[RequireComponent(typeof(OldManMovement))]
[RequireComponent(typeof(OldManPatrol))]
[RequireComponent(typeof(OldManAnimation))]
public class OldManStateMachine : MonoBehaviour
{
    public enum OldManState
    {
        Patrol,
        Chasing,
        Aiming,
        Shooting
    }

    private OldManMovement movement;
    private OldManPatrol patrol;
    private OldManAnimation animationController;
    private OldManState currentState;
    private bool isInitialized;

    public OldManState CurrentState => currentState;

    private void Awake()
    {
        movement = GetComponent<OldManMovement>();
        patrol = GetComponent<OldManPatrol>();
        animationController = GetComponent<OldManAnimation>();
    }

    private void Start()
    {
        SetState(OldManState.Patrol);
    }

    private void Update()
    {
        if (currentState == OldManState.Patrol)
        {
            patrol.Tick();
        }

        bool isMovingAlongPatrolRoute =
            currentState == OldManState.Patrol &&
            !patrol.IsWaiting &&
            movement.IsMoving;

        animationController.SetMoving(isMovingAlongPatrolRoute);
    }

    public void SetState(OldManState newState)
    {
        if (isInitialized)
        {
            ExitState(currentState);
        }

        currentState = newState;
        isInitialized = true;

        EnterState(currentState);
    }

    private void EnterState(OldManState state)
    {
        switch (state)
        {
            case OldManState.Patrol:
                animationController.PlayPatrol();
                patrol.Enter();
                break;

            case OldManState.Chasing:
                // Placeholder for target pursuit.
                movement.Stop();
                break;

            case OldManState.Aiming:
                // Placeholder for target selection and aiming logic.
                movement.Stop();
                animationController.PlayAim();
                break;

            case OldManState.Shooting:
                // Placeholder for weapon and damage logic.
                movement.Stop();
                animationController.PlayShoot();
                break;
        }
    }

    private void ExitState(OldManState state)
    {
        if (state == OldManState.Patrol)
        {
            patrol.Exit();
        }
    }
}

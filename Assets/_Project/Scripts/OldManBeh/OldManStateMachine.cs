using UnityEngine;

[RequireComponent(typeof(OldManMovement))]
[RequireComponent(typeof(OldManPatrol))]
[RequireComponent(typeof(OldManAnimation))]
[RequireComponent(typeof(OldManCombat))]
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
    private OldManCombat combat;
    private OldManState currentState;
    private bool isInitialized;

    public OldManState CurrentState => currentState;

    private void Awake()
    {
        movement = GetComponent<OldManMovement>();
        patrol = GetComponent<OldManPatrol>();
        animationController = GetComponent<OldManAnimation>();
        combat = GetComponent<OldManCombat>();
    }

    private void Start()
    {
        SetState(OldManState.Patrol);
    }

    private void Update()
    {
        switch (currentState)
        {
            case OldManState.Patrol:
                patrol.Tick();
                break;

            case OldManState.Aiming:
                if (!combat.UpdateAiming(Time.deltaTime))
                {
                    SetState(OldManState.Shooting);
                }
                break;

            case OldManState.Shooting:
                if (!combat.UpdateShooting(Time.deltaTime))
                {
                    SetState(OldManState.Patrol);
                }
                break;
        }

        bool isMovingAlongPatrolRoute =
            currentState == OldManState.Patrol &&
            !patrol.IsWaiting &&
            movement.IsMoving;

        animationController.SetMoving(isMovingAlongPatrolRoute);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState != OldManState.Patrol)
        {
            return;
        }

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();

        if (health == null)
        {
            return;
        }

        combat.BeginAim(health, health.transform, other);
        SetState(OldManState.Aiming);
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
                combat.Abort();
                animationController.PlayPatrol();
                patrol.Enter();
                break;

            case OldManState.Chasing:
                movement.Stop();
                break;

            case OldManState.Aiming:
                break;

            case OldManState.Shooting:
                combat.Shoot();
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
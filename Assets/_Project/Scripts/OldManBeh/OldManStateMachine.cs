using UnityEngine;

[RequireComponent(typeof(OldManMovement))]
[RequireComponent(typeof(OldManPatrol))]
[RequireComponent(typeof(OldManAnimation))]
[RequireComponent(typeof(OldManCombat))]
public class OldManStateMachine : MonoBehaviour
{
    [SerializeField] private float playerDetectionRadius = 3f;
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
    private PlayerHealth playerHealth;
    private Collider playerCollider;
    private Collider stairsTrigger;

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
        FindPlayer();
        FindStairsTrigger();
        SetState(OldManState.Patrol);
    }

    private void Update()
    {
        if (IsOnStairs() &&
            (currentState == OldManState.Aiming ||
             currentState == OldManState.Shooting))
        {
            SetState(OldManState.Patrol);
        }

        switch (currentState)
        {
            case OldManState.Patrol:
                patrol.Tick();
                TryDetectPlayer();
                break;

            case OldManState.Aiming:
                OldManCombat.AimResult aimResult = combat.UpdateAiming(Time.deltaTime);
                if (aimResult == OldManCombat.AimResult.Fire)
                {
                    SetState(OldManState.Shooting);
                }
                else if (aimResult == OldManCombat.AimResult.GiveUp)
                {
                    SetState(OldManState.Patrol);
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

    private void FindPlayer()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerHealth == null)
        {
            return;
        }

        CharacterController character =
            playerHealth.GetComponentInChildren<CharacterController>();

        playerCollider = character != null
            ? character
            : playerHealth.GetComponentInChildren<Collider>();
    }

    private void FindStairsTrigger()
    {
        Collider[] colliders =
            FindObjectsByType<Collider>(FindObjectsSortMode.None);

        foreach (Collider collider in colliders)
        {
            if (collider.isTrigger &&
                collider.gameObject.name.IndexOf(
                    "Stairs",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                stairsTrigger = collider;
                return;
            }
        }
    }

    private void TryDetectPlayer()
    {
        if (IsOnStairs())
        {
            return;
        }

        if (playerHealth == null || playerCollider == null)
        {
            FindPlayer();
        }

        if (playerHealth == null || playerCollider == null)
        {
            return;
        }

        Vector3 delta = playerCollider.transform.position - transform.position;

        delta.y = 0f;

        if (delta.sqrMagnitude >
            playerDetectionRadius * playerDetectionRadius)
        {
            return;
        }

        combat.BeginAim(playerHealth, playerHealth.transform, playerCollider);
        SetState(OldManState.Aiming);
    }

    private bool IsOnStairs()
    {
        return stairsTrigger != null &&
               stairsTrigger.bounds.Contains(transform.position);
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
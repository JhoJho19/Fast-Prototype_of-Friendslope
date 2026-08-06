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
    private PlayerHealth playerHealth;
    private Collider playerCollider;
    private PlayerHealth[] playerHealthCandidates;
    private Collider stairsTrigger;
    private Collider hearingTrigger;
    private Collider watchingTrigger;

    public OldManState CurrentState => currentState;

    private void Awake()
    {
        movement = GetComponent<OldManMovement>();
        patrol = GetComponent<OldManPatrol>();
        animationController = GetComponent<OldManAnimation>();
        combat = GetComponent<OldManCombat>();
        FindSensors();
    }

    private void Start()
    {
        RefreshPlayerCandidates();
        FindStairsTrigger();
        SetState(OldManState.Patrol);
    }

    private void OnEnable()
    {
        CoopNetworkManager.PlayersChanged += RefreshPlayerCandidates;
        RefreshPlayerCandidates();
    }

    private void OnDisable()
    {
        CoopNetworkManager.PlayersChanged -= RefreshPlayerCandidates;
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

    private void RefreshPlayerCandidates()
    {
        playerHealthCandidates =
            FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
    }

    private static Collider FindPlayerCollider(PlayerHealth health)
    {
        if (health == null)
        {
            return null;
        }

        CharacterController character =
            health.GetComponentInChildren<CharacterController>();

        return character != null
            ? character
            : health.GetComponentInChildren<Collider>();
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

    private void FindSensors()
    {
        Transform hearingTransform = transform.Find("Old man hearing");

        if (hearingTransform != null)
        {
            hearingTrigger = hearingTransform.GetComponent<Collider>();
        }

        Transform watchingTransform = transform.Find("Old man watrching");

        if (watchingTransform != null)
        {
            watchingTrigger = watchingTransform.GetComponent<Collider>();
        }
    }

    private void TryDetectPlayer()
    {
        if (IsOnStairs())
        {
            return;
        }

        playerHealth = null;
        playerCollider = null;

        if (playerHealthCandidates == null)
        {
            RefreshPlayerCandidates();
        }

        float nearestSqrDistance = float.MaxValue;

        foreach (PlayerHealth candidate in playerHealthCandidates)
        {
            if (candidate == null)
            {
                continue;
            }

            CoopNetworkHealth networkHealth =
                candidate.GetComponent<CoopNetworkHealth>();

            if (networkHealth != null && networkHealth.IsDead)
            {
                continue;
            }

            Collider candidateCollider = FindPlayerCollider(candidate);

            if (candidateCollider == null ||
                !IsPlayerInsideSensor(hearingTrigger, candidateCollider) &&
                !IsPlayerInsideSensor(watchingTrigger, candidateCollider))
            {
                continue;
            }

            float sqrDistance =
                (candidate.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                playerHealth = candidate;
                playerCollider = candidateCollider;
            }
        }

        if (playerHealth == null || playerCollider == null)
        {
            return;
        }

        combat.BeginAim(playerHealth, playerHealth.transform, playerCollider);
        SetState(OldManState.Aiming);
    }

    private bool IsPlayerInsideSensor(
        Collider sensor,
        Collider targetCollider)
    {
        return sensor != null &&
               sensor.enabled &&
               targetCollider != null &&
               targetCollider.enabled &&
               sensor.bounds.Intersects(targetCollider.bounds);
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

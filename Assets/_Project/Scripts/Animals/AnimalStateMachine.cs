using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AnimalMovement))]
[RequireComponent(typeof(AnimalPatrol))]
[RequireComponent(typeof(AnimalAnimation))]
public sealed class AnimalStateMachine : MonoBehaviour
{
    [SerializeField, Min(0f)] private float idleDuration = 2f;
    [SerializeField, Min(0f)] private float fleeChaseRange = 8f;
    [SerializeField, Min(0f)] private float fleeChaseTimeout = 2.5f;
    [SerializeField, Min(1f)] private float fleeSpeedMultiplier = 2f;

    private AnimalMovement movement;
    private AnimalPatrol patrol;
    private AnimalAnimation animationController;
    private IAnimalState currentStateHandler;
    private bool isInitialized;
    private bool hasFleeSource;
    private Vector3 fleeSource;
    private float lastFleeSourceTime;

    public AnimalState CurrentState { get; private set; }
    internal float IdleDuration => idleDuration;
    internal float FleeChaseRange => fleeChaseRange;
    internal float FleeChaseTimeout => fleeChaseTimeout;
    internal float LastFleeSourceTime => lastFleeSourceTime;
    internal float FleeSpeedMultiplier =>
        fleeSpeedMultiplier > 0f ? fleeSpeedMultiplier : 2f;
    internal AnimalMovement Movement => movement;
    internal AnimalPatrol Patrol => patrol;
    internal AnimalAnimation AnimationController => animationController;

    private void Awake()
    {
        movement = GetComponent<AnimalMovement>();
        patrol = GetComponent<AnimalPatrol>();
        animationController = GetComponent<AnimalAnimation>();
    }

    private void Start()
    {
        SetState(AnimalState.Patrol);
    }

    private void Update()
    {
        currentStateHandler?.Tick();

        bool usesLocomotion =
            CurrentState == AnimalState.Patrol ||
            CurrentState == AnimalState.Flee;

        animationController.SetMoving(
            usesLocomotion && movement.IsMoving,
            movement.CurrentSpeed);
    }

    private void OnDisable()
    {
        movement?.Stop();
        animationController?.SetMoving(false, 0f);
    }

    public void SetState(AnimalState newState)
    {
        if (isInitialized && CurrentState == newState)
        {
            return;
        }

        if (isInitialized)
        {
            currentStateHandler.Exit();
        }

        CurrentState = newState;
        currentStateHandler = CreateState(newState);
        isInitialized = true;
        currentStateHandler.Enter();
    }

    public void SetFleeSource(Vector3 source)
    {
        fleeSource = source;
        hasFleeSource = true;
        lastFleeSourceTime = Time.time;
    }

    internal bool TryGetFleeSource(out Vector3 source)
    {
        source = fleeSource;
        return hasFleeSource;
    }

    private IAnimalState CreateState(AnimalState state)
    {
        switch (state)
        {
            case AnimalState.Patrol:
                return new AnimalPatrolState(this);
            case AnimalState.Idle:
                return new AnimalIdleState(this);
            case AnimalState.Flee:
                return new AnimalFleeState(this);
            case AnimalState.Carried:
                return new AnimalCarriedState(this);
            default:
                return new AnimalIdleState(this);
        }
    }
}

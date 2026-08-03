using UnityEngine;
using UnityEngine.AI;

internal sealed class AnimalPatrolState : IAnimalState
{
    private readonly AnimalStateMachine machine;

    public AnimalPatrolState(AnimalStateMachine machine)
    {
        this.machine = machine;
    }

    public void Enter()
    {
        machine.AnimationController.PlayPatrol();

        if (!machine.Patrol.TryStartPatrolToRandomPoint())
        {
            machine.SetState(AnimalState.Idle);
        }
    }

    public void Tick()
    {
        if (machine.Movement.PathStatus != NavMeshPathStatus.PathComplete)
        {
            machine.Movement.Stop();
            machine.SetState(AnimalState.Idle);
            return;
        }

        if (machine.Movement.HasReachedDestination)
        {
            machine.SetState(AnimalState.Idle);
        }
    }

    public void Exit()
    {
    }
}

internal sealed class AnimalIdleState : IAnimalState
{
    private readonly AnimalStateMachine machine;
    private float idleEndTime;

    public AnimalIdleState(AnimalStateMachine machine)
    {
        this.machine = machine;
    }

    public void Enter()
    {
        machine.Movement.Stop();
        machine.AnimationController.PlayIdle();
        idleEndTime = Time.time + machine.IdleDuration;
    }

    public void Tick()
    {
        if (Time.time >= idleEndTime)
        {
            machine.SetState(AnimalState.Patrol);
        }
    }

    public void Exit()
    {
    }
}

internal sealed class AnimalFleeState : IAnimalState
{
    private readonly AnimalStateMachine machine;

    public AnimalFleeState(AnimalStateMachine machine)
    {
        this.machine = machine;
    }

    public void Enter()
    {
        // TODO: add threat source, escape direction, shelter selection and flee speed.
        machine.Movement.Stop();
        machine.AnimationController.PlayFlee(true);
    }

    public void Tick()
    {
    }

    public void Exit()
    {
        machine.AnimationController.PlayFlee(false);
    }
}

internal sealed class AnimalParalyzedState : IAnimalState
{
    private readonly AnimalStateMachine machine;

    public AnimalParalyzedState(AnimalStateMachine machine)
    {
        this.machine = machine;
    }

    public void Enter()
    {
        machine.Movement.SetParalyzed(true);
        machine.AnimationController.PlayParalyzed(true);
    }

    public void Tick()
    {
    }

    public void Exit()
    {
        machine.Movement.SetParalyzed(false);
        machine.AnimationController.PlayParalyzed(false);
    }
}

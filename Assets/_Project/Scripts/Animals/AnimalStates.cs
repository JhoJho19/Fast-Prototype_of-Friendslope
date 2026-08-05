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
    private Transform fleeTargetPoint;

    public AnimalFleeState(AnimalStateMachine machine)
    {
        this.machine = machine;
    }

    public void Enter()
    {
        machine.Movement.SetSpeedMultiplier(
            machine.FleeSpeedMultiplier);
        machine.AnimationController.PlayFlee(true);

        fleeTargetPoint = machine.Patrol.FindNearestPatrolPoint();

        if (fleeTargetPoint != null &&
            machine.Movement.TryMoveTo(fleeTargetPoint.position))
        {
            return;
        }

        if (!TrySelectRandomTarget())
        {
            machine.Movement.Stop();
        }
    }

    public void Tick()
    {
        if (machine.Movement.PathStatus != NavMeshPathStatus.PathComplete)
        {
            TryContinueFlee();
            return;
        }

        if (!machine.Movement.HasReachedDestination)
        {
            return;
        }

        if (IsPlayerStillChasing())
        {
            TryContinueFlee();
            return;
        }

        machine.SetState(AnimalState.Idle);
    }

    public void Exit()
    {
        machine.Movement.SetSpeedMultiplier(1f);
        machine.AnimationController.PlayFlee(false);
        fleeTargetPoint = null;
    }

    private void TryContinueFlee()
    {
        if (TrySelectRandomTarget())
        {
            return;
        }

        machine.Movement.Stop();
        machine.SetState(AnimalState.Idle);
    }

    private bool TrySelectRandomTarget()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Transform target =
                machine.Patrol.FindRandomPatrolPoint(fleeTargetPoint);

            if (target == null)
            {
                return false;
            }

            if (machine.Movement.TryMoveTo(target.position))
            {
                fleeTargetPoint = target;
                return true;
            }
        }

        return false;
    }

    private bool IsPlayerStillChasing()
    {
        if (!machine.TryGetFleeSource(out Vector3 source) ||
            Time.time - machine.LastFleeSourceTime > machine.FleeChaseTimeout)
        {
            return false;
        }

        Vector3 sourcePosition =
            Vector3.ProjectOnPlane(source, Vector3.up);
        Vector3 selfPosition =
            Vector3.ProjectOnPlane(machine.transform.position, Vector3.up);

        return Vector3.Distance(sourcePosition, selfPosition) <=
               machine.FleeChaseRange;
    }
}

internal sealed class AnimalCarriedState : IAnimalState
{
    private readonly AnimalStateMachine machine;

    public AnimalCarriedState(AnimalStateMachine machine)
    {
        this.machine = machine;
    }

    public void Enter()
    {
        machine.Movement.Stop();
        machine.Movement.SetSpeedMultiplier(1f);
        machine.AnimationController.PlayCarried();
    }

    public void Tick()
    {
    }

    public void Exit()
    {
    }
}

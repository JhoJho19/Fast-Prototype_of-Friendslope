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
    private static readonly float[] DistanceMultipliers =
    {
        1f,
        0.75f,
        0.5f
    };

    private readonly AnimalStateMachine machine;

    public AnimalFleeState(AnimalStateMachine machine)
    {
        this.machine = machine;
    }

    public void Enter()
    {
        machine.Movement.SetSpeedMultiplier(
            machine.FleeSpeedMultiplier);
        machine.AnimationController.PlayFlee(true);

        if (!TryStartFlee())
        {
            machine.Movement.Stop();
        }
    }

    public void Tick()
    {
        if (machine.Movement.PathStatus != NavMeshPathStatus.PathComplete)
        {
            TryStartFlee();
            return;
        }

        if (machine.Movement.HasReachedDestination)
        {
            TryStartFlee();
        }
    }

    public void Exit()
    {
        machine.Movement.SetSpeedMultiplier(1f);
        machine.AnimationController.PlayFlee(false);
    }

    private bool TryStartFlee()
    {
        Vector3 sourcePosition =
            machine.TryGetFleeSource(out Vector3 fleeSource)
                ? fleeSource
                : machine.transform.position - machine.transform.forward;

        Vector3 awayDirection =
            machine.transform.position - sourcePosition;

        awayDirection = Vector3.ProjectOnPlane(awayDirection, Vector3.up);

        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection =
                Vector3.ProjectOnPlane(
                    -machine.transform.forward,
                    Vector3.up);
        }

        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = Vector3.forward;
        }

        awayDirection.Normalize();

        for (int distanceIndex = 0;
             distanceIndex < DistanceMultipliers.Length;
             distanceIndex++)
        {
            float fleeDistance =
                machine.FleeDistance * DistanceMultipliers[distanceIndex];

            if (TryMoveInDirection(awayDirection, fleeDistance))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryMoveInDirection(Vector3 awayDirection, float fleeDistance)
    {
        int candidateCount = Mathf.Max(1, machine.FleeCandidateCount);
        float halfSpread = machine.FleeSpreadAngle * 0.5f;

        if (TryMoveToCandidate(awayDirection, fleeDistance))
        {
            return true;
        }

        for (int i = 0; i < candidateCount; i++)
        {
            float t =
                candidateCount == 1
                    ? 0.5f
                    : (i + 1f) / (candidateCount + 1f);

            float angle = Mathf.Lerp(-halfSpread, halfSpread, t);

            if (Mathf.Approximately(angle, 0f))
            {
                continue;
            }

            Vector3 candidateDirection =
                Quaternion.AngleAxis(angle, Vector3.up) * awayDirection;

            if (TryMoveToCandidate(candidateDirection, fleeDistance))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryMoveToCandidate(Vector3 direction, float fleeDistance)
    {
        Vector3 destination =
            machine.transform.position + direction * fleeDistance;

        return machine.Movement.TryMoveTo(destination);
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

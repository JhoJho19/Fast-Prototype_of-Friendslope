using UnityEngine;

[RequireComponent(typeof(OldManMovement))]
public class OldManPatrol : MonoBehaviour
{
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private Transform _startPatrolPoint;
    [SerializeField] private float patrolPointWaitDuration = 2f;

    private OldManMovement movement;
    private int currentPatrolPointIndex = -1;
    private float patrolWaitEndTime;
    private bool isWaitingAtPatrolPoint;
    private bool missingPatrolPointsWarningShown;
    private bool _needsStartPatrol = true;

    public bool IsWaiting => isWaitingAtPatrolPoint;

    private void Awake()
    {
        movement = GetComponent<OldManMovement>();
    }

    public void Enter()
    {
        isWaitingAtPatrolPoint = false;

        if (_needsStartPatrol && _startPatrolPoint != null)
        {
            _needsStartPatrol = false;
            movement.MoveTo(_startPatrolPoint.position);
            return;
        }

        SelectNextPatrolPoint();
    }

    public void Tick()
    {
        if (isWaitingAtPatrolPoint)
        {
            if (Time.time >= patrolWaitEndTime)
            {
                isWaitingAtPatrolPoint = false;
                SelectNextPatrolPoint();
            }

            return;
        }

        if (movement.HasReachedDestination)
        {
            StartPatrolPointWait();
        }
    }

    public void Exit()
    {
        isWaitingAtPatrolPoint = false;
    }

    private void StartPatrolPointWait()
    {
        isWaitingAtPatrolPoint = true;
        patrolWaitEndTime = Time.time + patrolPointWaitDuration;
        movement.Stop();
    }

    private void SelectNextPatrolPoint()
    {
        int validPatrolPointCount = CountValidPatrolPoints();

        if (validPatrolPointCount == 0)
        {
            WarnAboutMissingPatrolPoints();
            movement.Stop();
            return;
        }

        int nextPatrolPointIndex;

        do
        {
            nextPatrolPointIndex = Random.Range(0, patrolPoints.Length);
        }
        while (patrolPoints[nextPatrolPointIndex] == null ||
               validPatrolPointCount > 1 &&
               nextPatrolPointIndex == currentPatrolPointIndex);

        currentPatrolPointIndex = nextPatrolPointIndex;
        movement.MoveTo(patrolPoints[currentPatrolPointIndex].position);
    }

    private int CountValidPatrolPoints()
    {
        if (patrolPoints == null)
        {
            return 0;
        }

        int validPatrolPointCount = 0;

        foreach (Transform patrolPoint in patrolPoints)
        {
            if (patrolPoint != null)
            {
                validPatrolPointCount++;
            }
        }

        return validPatrolPointCount;
    }

    private void WarnAboutMissingPatrolPoints()
    {
        if (missingPatrolPointsWarningShown)
        {
            return;
        }

        Debug.LogWarning(
            "OldMan requires at least one patrol point.",
            this);

        missingPatrolPointsWarningShown = true;
    }
}

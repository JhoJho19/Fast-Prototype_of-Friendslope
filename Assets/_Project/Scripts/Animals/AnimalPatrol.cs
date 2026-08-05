using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AnimalMovement))]
public sealed class AnimalPatrol : MonoBehaviour
{
    [SerializeField] private Transform[] patrolPoints;

    private readonly List<Transform> candidates = new List<Transform>();
    private AnimalMovement movement;
    private Transform lastSelectedPoint;
    private bool missingPointsWarningShown;
    private bool unreachablePointsWarningShown;

    private void Awake()
    {
        movement = GetComponent<AnimalMovement>();
    }

    public bool TryStartPatrolToRandomPoint()
    {
        CollectValidPoints();

        if (candidates.Count == 0)
        {
            WarnOnce(
                ref missingPointsWarningShown,
                "requires at least one assigned patrol point.");
            movement.Stop();
            return false;
        }

        ShuffleCandidates();
        MovePreviousPointToEnd();

        foreach (Transform point in candidates)
        {
            if (!movement.TryMoveTo(point.position))
            {
                continue;
            }

            lastSelectedPoint = point;
            return true;
        }

        WarnOnce(
            ref unreachablePointsWarningShown,
            "has no reachable patrol point at the moment.");
        movement.Stop();
        return false;
    }

    public Transform FindNearestPatrolPoint()
    {
        CollectValidPoints();

        if (candidates.Count == 0)
        {
            WarnOnce(
                ref missingPointsWarningShown,
                "requires at least one assigned patrol point.");
            return null;
        }

        Transform nearest = null;
        float nearestSqrDistance = float.MaxValue;
        Vector3 selfPosition = transform.position;

        foreach (Transform point in candidates)
        {
            float sqrDistance =
                (point.position - selfPosition).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = point;
            }
        }

        return nearest;
    }

    public Transform FindRandomPatrolPoint(Transform excludePoint)
    {
        CollectValidPoints();

        if (candidates.Count == 0)
        {
            WarnOnce(
                ref missingPointsWarningShown,
                "requires at least one assigned patrol point.");
            return null;
        }

        Transform selected = null;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            Transform candidate =
                candidates[Random.Range(0, candidates.Count)];

            if (candidate != excludePoint)
            {
                selected = candidate;
                break;
            }
        }

        if (selected == null)
        {
            selected = candidates[Random.Range(0, candidates.Count)];
        }

        return selected;
    }

    private void CollectValidPoints()
    {
        candidates.Clear();

        if (patrolPoints == null)
        {
            return;
        }

        foreach (Transform point in patrolPoints)
        {
            if (point != null)
            {
                candidates.Add(point);
            }
        }
    }

    private void ShuffleCandidates()
    {
        for (int index = candidates.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Range(0, index + 1);
            (candidates[index], candidates[swapIndex]) =
                (candidates[swapIndex], candidates[index]);
        }
    }

    private void MovePreviousPointToEnd()
    {
        if (lastSelectedPoint == null || candidates.Count < 2)
        {
            return;
        }

        int lastPointIndex = candidates.IndexOf(lastSelectedPoint);
        if (lastPointIndex < 0)
        {
            return;
        }

        Transform previousPoint = candidates[lastPointIndex];
        candidates.RemoveAt(lastPointIndex);
        candidates.Add(previousPoint);
    }

    private void WarnOnce(ref bool wasShown, string message)
    {
        if (wasShown)
        {
            return;
        }

        wasShown = true;
        Debug.LogWarning($"{name} {message}", this);
    }
}

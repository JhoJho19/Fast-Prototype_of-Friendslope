using DG.Tweening;
using UnityEngine;

public sealed class DoorBeh : MonoBehaviour, IInteractable
{
    private const float ClosedAngle = 0f;
    private const float OpenAngle = 90f;

    [SerializeField] private bool openInNegativeDirection;
    [SerializeField, Min(0f)] private float rotationDuration = 0.5f;
    [SerializeField, Min(0f)] private float angleTolerance = 1f;

    private Tween rotationTween;

    private float TargetOpenAngle =>
        openInNegativeDirection ? -OpenAngle : OpenAngle;

    public void CloseDoor()
    {
        if (IsAtAngle(ClosedAngle))
            return;

        RotateDoor(ClosedAngle);
    }

    public void OpenDoor()
    {
        if (IsAtAngle(TargetOpenAngle))
            return;

        RotateDoor(TargetOpenAngle);
    }

    public void ToggleDoor()
    {
        if (IsDoorOpen())
        {
            CloseDoor();
        }
        else
        {
            OpenDoor();
        }
    }

    private void RotateDoor(float targetAngle)
    {
        rotationTween?.Kill();

        Vector3 targetRotation = transform.localEulerAngles;
        targetRotation.y = targetAngle;

        rotationTween = transform
            .DOLocalRotate(targetRotation, rotationDuration, RotateMode.Fast)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => rotationTween = null);
    }

    private bool IsDoorOpen()
    {
        float currentAngle = GetCurrentYAngle();

        float distanceToClosed = Mathf.Abs(
            Mathf.DeltaAngle(currentAngle, ClosedAngle));

        float distanceToOpen = Mathf.Abs(
            Mathf.DeltaAngle(currentAngle, TargetOpenAngle));

        return distanceToOpen < distanceToClosed;
    }

    private bool IsAtAngle(float targetAngle)
    {
        float difference = Mathf.Abs(
            Mathf.DeltaAngle(GetCurrentYAngle(), targetAngle));

        return difference <= angleTolerance;
    }

    private float GetCurrentYAngle()
    {
        return transform.localEulerAngles.y;
    }

    private void OnDestroy()
    {
        rotationTween?.Kill();
    }

    public void Interact()
    {
        ToggleDoor();
    }
}

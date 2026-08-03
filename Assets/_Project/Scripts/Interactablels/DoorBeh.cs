using System;
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
    private DoorMotion currentMotion;
    private bool isOpen;

    private float TargetOpenAngle =>
        openInNegativeDirection ? -OpenAngle : OpenAngle;

    public bool IsOpen => isOpen;
    public bool IsMoving =>
        rotationTween != null && rotationTween.IsActive();

    public event Action Opened;

    private enum DoorMotion
    {
        None,
        Opening,
        Closing
    }

    private void Awake()
    {
        isOpen = IsAtAngle(TargetOpenAngle);
    }

    public void CloseDoor()
    {
        if (IsMoving && currentMotion == DoorMotion.Closing)
        {
            return;
        }

        if (!IsMoving && !isOpen && IsAtAngle(ClosedAngle))
        {
            return;
        }

        RotateDoor(ClosedAngle, DoorMotion.Closing);
    }

    public void OpenDoor()
    {
        if (IsMoving && currentMotion == DoorMotion.Opening)
        {
            return;
        }

        if (!IsMoving && isOpen)
        {
            return;
        }

        RotateDoor(TargetOpenAngle, DoorMotion.Opening);
    }

    public void ToggleDoor()
    {
        if (currentMotion == DoorMotion.Opening ||
            !IsMoving && isOpen)
        {
            CloseDoor();
        }
        else
        {
            OpenDoor();
        }
    }

    private void RotateDoor(float targetAngle, DoorMotion motion)
    {
        CancelRotationTween();
        currentMotion = motion;

        Vector3 targetRotation = transform.localEulerAngles;
        targetRotation.y = targetAngle;

        if (rotationDuration <= 0f)
        {
            transform.localEulerAngles = targetRotation;
            CompleteRotation(motion);
            return;
        }

        rotationTween = transform
            .DOLocalRotate(targetRotation, rotationDuration, RotateMode.Fast)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => CompleteRotation(motion));
    }

    private void CompleteRotation(DoorMotion completedMotion)
    {
        if (currentMotion != completedMotion)
        {
            return;
        }

        rotationTween = null;
        currentMotion = DoorMotion.None;

        if (completedMotion == DoorMotion.Opening)
        {
            isOpen = true;
            Opened?.Invoke();
            return;
        }

        isOpen = false;
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

    private void CancelRotationTween()
    {
        if (rotationTween == null)
        {
            return;
        }

        rotationTween.Kill();
        rotationTween = null;
    }

    private void OnDestroy()
    {
        CancelRotationTween();
    }

    public void Interact()
    {
        ToggleDoor();
    }
}

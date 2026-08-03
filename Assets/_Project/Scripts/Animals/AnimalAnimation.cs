using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnimalAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string movingParameter = "Moving";
    [SerializeField] private string locomotionParameter = "Vert";
    [SerializeField] private string idleParameter = "Idle";
    [SerializeField] private string fleeParameter = "Flee";
    [SerializeField] private string paralyzedParameter = "Paralyzed";
    [SerializeField, Min(0f)] private float movementSpeedThreshold = 0.01f;
    [SerializeField] private float idleLocomotionValue;
    [SerializeField] private float movingLocomotionValue = 1f;

    private readonly Dictionary<string, AnimatorControllerParameterType> parameterTypes =
        new Dictionary<string, AnimatorControllerParameterType>();

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>(true);
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        CacheParameters();

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    public void PlayPatrol()
    {
        SetOptionalState(idleParameter, false);
        SetOptionalState(fleeParameter, false);
        SetOptionalState(paralyzedParameter, false);
    }

    public void PlayIdle()
    {
        SetMoving(false, 0f);
        SetOptionalState(idleParameter, true);
    }

    public void PlayFlee(bool active)
    {
        SetMoving(false, 0f);
        SetOptionalState(fleeParameter, active);
    }

    public void PlayParalyzed(bool active)
    {
        SetMoving(false, 0f);
        SetOptionalState(paralyzedParameter, active);
    }

    public void SetMoving(bool isMoving, float currentSpeed)
    {
        if (animator == null)
        {
            return;
        }

        bool shouldMove = isMoving && currentSpeed > movementSpeedThreshold;
        SetOptionalFloat(speedParameter, shouldMove ? currentSpeed : 0f);
        SetOptionalState(movingParameter, shouldMove);
        SetOptionalFloat(
            locomotionParameter,
            shouldMove ? movingLocomotionValue : idleLocomotionValue);
    }

    private void CacheParameters()
    {
        parameterTypes.Clear();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            parameterTypes[parameter.name] = parameter.type;
        }
    }

    private void SetOptionalFloat(string parameterName, float value)
    {
        if (string.IsNullOrWhiteSpace(parameterName) ||
            !parameterTypes.TryGetValue(
                parameterName,
                out AnimatorControllerParameterType parameterType) ||
            parameterType != AnimatorControllerParameterType.Float)
        {
            return;
        }

        animator.SetFloat(parameterName, value);
    }

    private void SetOptionalState(string parameterName, bool active)
    {
        if (string.IsNullOrWhiteSpace(parameterName) ||
            !parameterTypes.TryGetValue(
                parameterName,
                out AnimatorControllerParameterType parameterType))
        {
            return;
        }

        if (parameterType == AnimatorControllerParameterType.Bool)
        {
            animator.SetBool(parameterName, active);
        }
        else if (parameterType == AnimatorControllerParameterType.Trigger)
        {
            if (active)
            {
                animator.SetTrigger(parameterName);
            }
            else
            {
                animator.ResetTrigger(parameterName);
            }
        }
    }
}

using Mirror;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class OldManAnimation : MonoBehaviour
{
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int PatrolTrigger = Animator.StringToHash("Patrol");
    private static readonly int AimTrigger = Animator.StringToHash("Aim");
    private static readonly int ShootTrigger = Animator.StringToHash("Shoot");

    private Animator animator;
    private NetworkAnimator networkAnimator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
    }

    public void PlayPatrol()
    {
        ResetTrigger(AimTrigger);
        ResetTrigger(ShootTrigger);
        SetTrigger(PatrolTrigger);
    }

    public void PlayAim()
    {
        ResetTrigger(PatrolTrigger);
        ResetTrigger(ShootTrigger);
        SetTrigger(AimTrigger);
    }

    public void PlayShoot()
    {
        ResetTrigger(PatrolTrigger);
        ResetTrigger(AimTrigger);
        SetTrigger(ShootTrigger);
    }

    public void SetMoving(bool isMoving)
    {
        animator.SetFloat(SpeedParameter, isMoving ? 1f : 0f);
    }

    private void SetTrigger(int triggerHash)
    {
        if (networkAnimator != null && networkAnimator.isServer)
        {
            networkAnimator.SetTrigger(triggerHash);
            return;
        }

        animator.SetTrigger(triggerHash);
    }

    private void ResetTrigger(int triggerHash)
    {
        if (networkAnimator != null && networkAnimator.isServer)
        {
            networkAnimator.ResetTrigger(triggerHash);
            return;
        }

        animator.ResetTrigger(triggerHash);
    }
}

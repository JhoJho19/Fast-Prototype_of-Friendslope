using UnityEngine;

[RequireComponent(typeof(Animator))]
public class OldManAnimation : MonoBehaviour
{
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int PatrolTrigger = Animator.StringToHash("Patrol");
    private static readonly int AimTrigger = Animator.StringToHash("Aim");
    private static readonly int ShootTrigger = Animator.StringToHash("Shoot");

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void PlayPatrol()
    {
        animator.ResetTrigger(AimTrigger);
        animator.ResetTrigger(ShootTrigger);
        animator.SetTrigger(PatrolTrigger);
    }

    public void PlayAim()
    {
        animator.SetTrigger(AimTrigger);
    }

    public void PlayShoot()
    {
        animator.SetTrigger(ShootTrigger);
    }

    public void SetMoving(bool isMoving)
    {
        animator.SetFloat(SpeedParameter, isMoving ? 1f : 0f);
    }
}

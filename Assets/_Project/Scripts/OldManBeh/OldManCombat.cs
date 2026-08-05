using UnityEngine;

[RequireComponent(typeof(OldManMovement))]
[RequireComponent(typeof(OldManAnimation))]
public class OldManCombat : MonoBehaviour
{
    [SerializeField] private float aimDuration = 2f;
    [SerializeField] private float minAimTime = 1.2f;
    [SerializeField] private float postShootPause = 1.2f;
    [SerializeField] private float rayOriginHeight = 1.4f;
    [SerializeField] private float angularSpeed = 240f;
    [SerializeField] private float facingTolerance = 15f;

    private OldManMovement movement;
    private OldManAnimation animationController;
    private OldManShotgunSensor shotgunSensor;
    private Transform playerRoot;
    private PlayerHealth playerHealth;
    private Collider playerCollider;
    private float aimTimer;
    private float postShootTimer;

    public string LastDebug => lastDebug;

    private string lastDebug;

    private void Awake()
    {
        movement = GetComponent<OldManMovement>();
        animationController = GetComponent<OldManAnimation>();
        shotgunSensor = GetComponentInChildren<OldManShotgunSensor>();
    }

    public void BeginAim(PlayerHealth health, Transform root, Collider targetCollider)
    {
        playerHealth = health;
        playerRoot = root;
        playerCollider = targetCollider;
        aimTimer = aimDuration;
        movement.Stop();
        animationController.PlayAim();
        lastDebug = "BeginAim";
    }

    public bool UpdateAiming(float deltaTime)
    {
        if (playerRoot == null)
        {
            return false;
        }

        RotateTowardsPlayer();

        aimTimer -= deltaTime;

        float aimElapsed = aimDuration - aimTimer;
        bool minTimeElapsed = aimElapsed >= minAimTime;
        bool onTarget = IsPlayerInSights();

        if (onTarget && minTimeElapsed)
        {
            lastDebug = "AimFinished:OnTarget";
            return false;
        }

        if (aimTimer <= 0f)
        {
            lastDebug = "AimFinished:MaxTime";
            return false;
        }

        return true;
    }

    public void Shoot()
    {
        movement.Stop();
        FacePlayer();
        animationController.PlayShoot();
        FireAtPlayer();
        postShootTimer = postShootPause;
    }

    public bool UpdateShooting(float deltaTime)
    {
        RotateTowardsPlayer();
        postShootTimer -= deltaTime;

        return postShootTimer > 0f;
    }

    public void Abort()
    {
        playerHealth = null;
        playerRoot = null;
        playerCollider = null;
    }

    private bool HasTarget(out PlayerHealth health, out Transform root)
    {
        health = playerHealth;
        root = playerRoot;
        return playerHealth != null && playerRoot != null;
    }

    private bool IsPlayerInSights()
    {
        if (playerRoot == null || playerCollider == null)
        {
            return false;
        }

        if (shotgunSensor != null && shotgunSensor.IsPlayerInSights)
        {
            return true;
        }

        return IsFacingPlayer();
    }

    private bool IsFacingPlayer()
    {
        Vector3 toPlayer = playerRoot.position - transform.position;

        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);

        return angle <= facingTolerance;
    }

    private void RotateTowardsPlayer()
    {
        Vector3 direction = playerRoot.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            angularSpeed * Time.deltaTime);
    }

    private void FacePlayer()
    {
        if (playerRoot == null)
        {
            return;
        }

        Vector3 direction = playerRoot.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        transform.rotation =
            Quaternion.LookRotation(direction.normalized);
    }

    private void FireAtPlayer()
    {
        if (playerHealth == null || playerRoot == null)
        {
            lastDebug = "Fire:noTarget";
            return;
        }

        Vector3 origin = GetRayOrigin();
        Vector3 target = GetPlayerAimPoint();
        Vector3 toTarget = target - origin;
        float distance = toTarget.magnitude;

        if (distance <= Mathf.Epsilon)
        {
            lastDebug = "Fire:zeroDistance";
            return;
        }

        Vector3 direction = toTarget.normalized;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        Transform blockingObject = null;

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (playerRoot != null &&
                hit.transform.IsChildOf(playerRoot))
            {
                continue;
            }

            blockingObject = hit.transform;
            break;
        }

        if (blockingObject != null)
        {
            lastDebug = $"Fire:blockedBy {blockingObject.name} at {blockingObject.position}";
            return;
        }

        lastDebug = "Fire:hitPlayer";
        playerHealth.Die();
    }

    private Vector3 GetRayOrigin()
    {
        return transform.position + Vector3.up * rayOriginHeight;
    }

    private Vector3 GetPlayerAimPoint()
    {
        if (playerCollider != null)
        {
            return playerCollider.bounds.center;
        }

        return playerRoot.position + Vector3.up * rayOriginHeight;
    }
}
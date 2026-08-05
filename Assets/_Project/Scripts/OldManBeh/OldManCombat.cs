using UnityEngine;

public class OldManCombat : MonoBehaviour
{
    [SerializeField] private float minAimTime = 1.2f;
    [SerializeField] private float aimTimeout = 3f;
    [SerializeField] private float postShootPause = 1.2f;
    [SerializeField] private float rayOriginHeight = 1.4f;
    [SerializeField] private float angularSpeed = 240f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private ParticleSystem shootVFX;

    private OldManMovement movement;
    private OldManAnimation animationController;
    private OldManShotgunSensor shotgunSensor;
    private Transform playerRoot;
    private PlayerHealth playerHealth;
    private Collider playerCollider;
    private float aimElapsed;
    private float postShootTimer;

    public enum AimResult
    {
        StillAiming,
        Fire,
        GiveUp
    }

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
        aimElapsed = 0f;
        movement.Stop();
        animationController.PlayAim();
        lastDebug = "BeginAim";
    }

    public AimResult UpdateAiming(float deltaTime)
    {
        if (playerRoot == null)
        {
            return AimResult.GiveUp;
        }

        RotateTowardsPlayer();

        aimElapsed += deltaTime;

        bool onTarget = IsPlayerInSights();

        if (onTarget && aimElapsed >= minAimTime)
        {
            lastDebug = "AimFinished:OnTarget";
            return AimResult.Fire;
        }

        if (aimElapsed >= aimTimeout)
        {
            lastDebug = "AimFinished:Timeout";
            return AimResult.GiveUp;
        }

        return AimResult.StillAiming;
    }

    public void Shoot()
    {
        movement.Stop();
        FacePlayer();
        animationController.PlayShoot();
        postShootTimer = postShootPause;
    }

    public void OldmanFireringEvent()
    {
        FireAtPlayer(); 
        audioSource.PlayOneShot(shootClip);
        shootVFX.Play();
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
        return shotgunSensor != null && shotgunSensor.IsPlayerInSights;
    }

    private void RotateTowardsPlayer()
    {
        Vector3 direction = GetPlayerPosition() - transform.position;

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

        Vector3 direction = GetPlayerPosition() - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        transform.rotation =
            Quaternion.LookRotation(direction.normalized);
    }

    private Vector3 GetPlayerPosition()
    {
        if (playerCollider != null)
        {
            return playerCollider.transform.position;
        }

        if (playerRoot != null)
        {
            return playerRoot.position;
        }

        return transform.position;
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
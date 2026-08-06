using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float respawnDelay = 0.6f;
    [SerializeField] private GameObject textYouDied;
    [SerializeField] private GameObject _model;
    [SerializeField] private GameObject _ragdollPrefab;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Rigidbody body;
    private PlayerInput playerInput;
    private CharacterController characterController;
    private FirstPersonController firstPersonController;
    private bool isDead;
    private Coroutine respawnCoroutine;
    private GameObject _spawnedRagdoll;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        playerInput = GetComponentInChildren<PlayerInput>();
        characterController = GetComponentInChildren<CharacterController>();
        firstPersonController = GetComponentInChildren<FirstPersonController>();
    }

    private void Start()
    {
        Transform teleportTarget = characterController != null
            ? characterController.transform
            : transform;
        startPosition = teleportTarget.position;
        startRotation = teleportTarget.rotation;
    }

    public void Die()
    {
        CoopNetworkHealth networkHealth =
            GetComponent<CoopNetworkHealth>();

        if (networkHealth != null && NetworkClient.active)
        {
            networkHealth.RequestDeath();
            return;
        }

        BeginNetworkDeath();

        if (respawnCoroutine == null)
        {
            respawnCoroutine = StartCoroutine(RespawnRoutine());
        }
    }

    public void BeginNetworkDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        if (textYouDied != null)
        {
            textYouDied.SetActive(true);
        }

        DisableControls();
        SpawnRagdoll();
    }

    public void SpawnRagdoll()
    {
        if (_model == null || _ragdollPrefab == null)
        {
            return;
        }

        _model.SetActive(false);

        if (_spawnedRagdoll == null)
        {
            Transform modelTransform = _model.transform;
            _spawnedRagdoll = Instantiate(
                _ragdollPrefab,
                modelTransform.position,
                modelTransform.rotation);
            CopyBoneTransforms(_model.transform, _spawnedRagdoll.transform);
        }
    }

    private static void CopyBoneTransforms(Transform source, Transform target)
    {
        target.SetPositionAndRotation(source.position, source.rotation);

        for (int i = 0; i < source.childCount; i++)
        {
            Transform sourceChild = source.GetChild(i);
            Transform targetChild = target.Find(sourceChild.name);

            if (targetChild != null)
            {
                CopyBoneTransforms(sourceChild, targetChild);
            }
        }
    }

    public void DestroyRagdoll()
    {
        if (_spawnedRagdoll != null)
        {
            Destroy(_spawnedRagdoll);
            _spawnedRagdoll = null;
        }

        if (_model != null)
        {
            _model.SetActive(true);

            Animator animator = _model.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Update(0f);
            }
        }
    }

    public void ResetForSession()
    {
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }

        DestroyRagdoll();

        Transform teleportTarget = characterController != null
            ? characterController.transform
            : transform;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        CoopNetworkPlayer networkPlayer =
            GetComponent<CoopNetworkPlayer>();

        if (networkPlayer != null)
        {
            networkPlayer.ResetToPosition(startPosition, startRotation);
        }
        else
        {
            teleportTarget.SetPositionAndRotation(startPosition, startRotation);
        }

        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        bool isLocalOwner = !NetworkClient.active ||
            GetComponent<CoopNetworkPlayer>()?.isLocalPlayer == true;

        if (firstPersonController != null)
        {
            firstPersonController.enabled = isLocalOwner;
        }

        if (playerInput != null)
        {
            if (isLocalOwner)
            {
                playerInput.ActivateInput();
            }
            else
            {
                playerInput.DeactivateInput();
            }
        }

        isDead = false;

        if (textYouDied != null)
        {
            textYouDied.SetActive(false);
        }

        if (_model != null)
        {
            _model.SetActive(true);
        }
    }

    private IEnumerator RespawnRoutine()
    {
        Debug.Log("[PlayerHealth] RespawnRoutine start");
        if (playerInput != null)
        {
            playerInput.DeactivateInput();
        }

        if (firstPersonController != null)
        {
            firstPersonController.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        yield return new WaitForSeconds(respawnDelay);

        Debug.Log("[PlayerHealth] after wait, teleporting to " + startPosition);
        ResetForSession();
    }

    private void DisableControls()
    {
        if (playerInput != null)
        {
            playerInput.DeactivateInput();
        }

        if (firstPersonController != null)
        {
            firstPersonController.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }
    }
}

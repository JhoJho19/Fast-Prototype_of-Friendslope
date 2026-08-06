using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float respawnDelay = 0.6f;
    [SerializeField] private GameObject textYouDied;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Rigidbody body;
    private PlayerInput playerInput;
    private CharacterController characterController;
    private FirstPersonController firstPersonController;
    private bool isDead;

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

        StartCoroutine(RespawnRoutine());
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

        Transform teleportTarget = characterController != null
            ? characterController.transform
            : transform;
        teleportTarget.SetPositionAndRotation(startPosition, startRotation);

        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (firstPersonController != null)
        {
            firstPersonController.enabled = true;
        }

        if (playerInput != null)
        {
            playerInput.ActivateInput();
        }

        isDead = false;
        if (textYouDied != null)
        {
            textYouDied.SetActive(false);
        }
    }
}

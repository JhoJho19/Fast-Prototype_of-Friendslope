using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public sealed class PlayerInteractor : MonoBehaviour
{
    private const string InteractTag = "Interact";
    private const string InteractActionName = "Interact";

    [SerializeField] private float interactionRayDistance = 5f;
    [SerializeField]
    private LayerMask interactionLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private Vector2 viewportPoint = new(0.5f, 0.5f);

    private readonly List<Collider> trackedColliders = new();

    private Camera interactionCamera;
    private PlayerInput playerInput;
    private TMP_Text textHint;
    private InputAction interactAction;
    private Transform currentTarget;

    private void Awake()
    {
        CacheReferences();
        HideHint();
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        interactionRayDistance = Mathf.Max(0.1f, interactionRayDistance);
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeToInteractAction();
        HideHint();
    }

    private void OnDisable()
    {
        UnsubscribeFromInteractAction();
        currentTarget = null;
        HideHint();
    }

    private void Update()
    {
        if (interactionCamera == null || playerInput == null)
        {
            CacheReferences();
        }

        if (interactAction == null)
        {
            SubscribeToInteractAction();
        }

        PruneTrackedColliders();
        currentTarget = FindCurrentTarget();
        SetHintVisible(currentTarget != null);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidInteractCollider(other))
        {
            return;
        }

        if (!trackedColliders.Contains(other))
        {
            trackedColliders.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
        {
            return;
        }

        trackedColliders.Remove(other);

        if (currentTarget == null)
        {
            return;
        }

        Transform interactTransform =
            ResolveInteractTransform(other.transform);

        if (interactTransform == currentTarget &&
            !IsTracked(interactTransform))
        {
            currentTarget = null;
            HideHint();
        }
    }

    private void CacheReferences()
    {
        if (playerInput == null)
        {
            playerInput = GetComponentInParent<PlayerInput>();
        }

        if (interactionCamera == null)
        {
            interactionCamera = FindInteractionCamera();
        }

        if (textHint == null)
        {
            textHint = FindTextHint();
        }
    }

    private Camera FindInteractionCamera()
    {
        Transform root = transform.root;
        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);

        foreach (Camera candidate in cameras)
        {
            if (candidate != null && candidate.CompareTag("MainCamera"))
            {
                return candidate;
            }
        }

        return Camera.main;
    }

    private TMP_Text FindTextHint()
    {
        Transform root = transform.root;
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text candidate in texts)
        {
            if (candidate != null &&
                candidate.gameObject.name == "TextHint")
            {
                return candidate;
            }
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private void SubscribeToInteractAction()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            return;
        }

        InputAction nextInteractAction =
            playerInput.actions.FindAction(InteractActionName, false);

        if (nextInteractAction == null ||
            ReferenceEquals(interactAction, nextInteractAction))
        {
            return;
        }

        UnsubscribeFromInteractAction();

        interactAction = nextInteractAction;
        interactAction.performed += OnInteractPerformed;
    }

    private void UnsubscribeFromInteractAction()
    {
        if (interactAction == null)
        {
            return;
        }

        interactAction.performed -= OnInteractPerformed;
        interactAction = null;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (currentTarget == null)
        {
            return;
        }

        IInteractable interactable = FindInteractable(currentTarget);

        if (interactable == null)
        {
            return;
        }

        interactable.Interact();
    }

    private IInteractable FindInteractable(Transform interactTransform)
    {
        if (interactTransform == null)
        {
            return null;
        }

        IInteractable interactable =
            interactTransform.GetComponent(typeof(IInteractable))
                as IInteractable;

        if (interactable != null)
        {
            return interactable;
        }

        interactable =
            interactTransform.GetComponentInParent(typeof(IInteractable))
                as IInteractable;

        if (interactable != null)
        {
            return interactable;
        }

        return interactTransform.GetComponentInChildren(
                   typeof(IInteractable),
                   true) as IInteractable;
    }

    private Transform FindCurrentTarget()
    {
        if (interactionCamera == null || trackedColliders.Count == 0)
        {
            return null;
        }

        Ray ray = interactionCamera.ViewportPointToRay(
            new Vector3(viewportPoint.x, viewportPoint.y, 0f));

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactionRayDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        Transform interactTransform =
            ResolveInteractTransform(hit.collider.transform);

        if (interactTransform == null || !IsTracked(interactTransform))
        {
            return null;
        }

        return interactTransform;
    }

    private bool IsTracked(Transform interactTransform)
    {
        for (int i = trackedColliders.Count - 1; i >= 0; i--)
        {
            Collider trackedCollider = trackedColliders[i];

            if (trackedCollider == null)
            {
                trackedColliders.RemoveAt(i);
                continue;
            }

            Transform trackedTransform =
                ResolveInteractTransform(trackedCollider.transform);

            if (trackedTransform == interactTransform)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform ResolveInteractTransform(Transform origin)
    {
        if (origin == null)
        {
            return null;
        }

        if (origin.CompareTag(InteractTag))
        {
            return origin;
        }

        Transform parent = origin.parent;

        while (parent != null)
        {
            if (parent.CompareTag(InteractTag))
            {
                return parent;
            }

            parent = parent.parent;
        }

        Transform child = FindTaggedChild(origin);
        return child;
    }

    private static Transform FindTaggedChild(Transform origin)
    {
        int childCount = origin.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = origin.GetChild(i);

            if (child.CompareTag(InteractTag))
            {
                return child;
            }

            Transform nestedChild = FindTaggedChild(child);

            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    private bool IsValidInteractCollider(Collider other)
    {
        if (other == null || other.transform.root == transform.root)
        {
            return false;
        }

        return ResolveInteractTransform(other.transform) != null;
    }

    private void PruneTrackedColliders()
    {
        for (int i = trackedColliders.Count - 1; i >= 0; i--)
        {
            Collider trackedCollider = trackedColliders[i];

            if (trackedCollider == null ||
                !trackedCollider.enabled ||
                !trackedCollider.gameObject.activeInHierarchy ||
                ResolveInteractTransform(trackedCollider.transform) == null)
            {
                trackedColliders.RemoveAt(i);
            }
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void SetHintVisible(bool isVisible)
    {
        if (textHint == null)
        {
            return;
        }

        if (textHint.gameObject.activeSelf == isVisible)
        {
            return;
        }

        textHint.gameObject.SetActive(isVisible);
    }

    private void HideHint()
    {
        SetHintVisible(false);
    }
}

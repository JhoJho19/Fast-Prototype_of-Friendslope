using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class PlayerInteractor : MonoBehaviour
{
    private const string InteractTag = "Interact";

    [Header("Interaction")]
    [SerializeField, Min(0.1f)]
    private float interactionRayDistance = 5f;

    [SerializeField]
    private LayerMask interactionLayers = Physics.DefaultRaycastLayers;

    [SerializeField]
    private Vector2 viewportPoint = new(0.5f, 0.5f);

    private readonly List<Collider> trackedColliders = new();

    private Camera interactionCamera;
    private Transform currentTarget;

    public Transform CurrentTarget => currentTarget;
    public Camera InteractionCamera => interactionCamera;
    public Transform PlayerRoot => transform.root;

    private void Awake()
    {
        CacheReferences();
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
        currentTarget = null;
    }

    private void Update()
    {
        if (interactionCamera == null)
        {
            CacheReferences();
        }

        PruneTrackedColliders();
        currentTarget = FindCurrentTarget();
    }

    private void OnDisable()
    {
        trackedColliders.Clear();
        currentTarget = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidInteractionCollider(other) ||
            trackedColliders.Contains(other))
        {
            return;
        }

        trackedColliders.Add(other);
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

        Transform interactionTransform =
            ResolveInteractionTransform(other.transform);

        if (interactionTransform == currentTarget &&
            !IsTracked(interactionTransform))
        {
            currentTarget = null;
        }
    }

    public bool TryGetTargetComponent<T>(out T component)
        where T : class
    {
        component = FindComponentInHierarchy<T>(currentTarget);
        return component != null;
    }

    private void CacheReferences()
    {
        if (interactionCamera == null)
        {
            interactionCamera = FindInteractionCamera();
        }
    }

    private Camera FindInteractionCamera()
    {
        Transform root = transform.root;
        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);

        foreach (Camera candidate in cameras)
        {
            if (candidate != null &&
                candidate.CompareTag("MainCamera"))
            {
                return candidate;
            }
        }

        return Camera.main;
    }

    private Transform FindCurrentTarget()
    {
        if (interactionCamera == null ||
            trackedColliders.Count == 0)
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

        Transform interactionTransform =
            ResolveInteractionTransform(hit.collider.transform);

        if (interactionTransform == null ||
            !IsTracked(interactionTransform))
        {
            return null;
        }

        return interactionTransform;
    }

    private bool IsTracked(Transform interactionTransform)
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
                ResolveInteractionTransform(trackedCollider.transform);

            if (trackedTransform == interactionTransform)
            {
                return true;
            }
        }

        return false;
    }

    private static T FindComponentInHierarchy<T>(Transform target)
        where T : class
    {
        if (target == null)
        {
            return null;
        }

        T component =
            target.GetComponent(typeof(T)) as T;

        if (component != null)
        {
            return component;
        }

        component =
            target.GetComponentInParent(typeof(T)) as T;

        if (component != null)
        {
            return component;
        }

        return target.GetComponentInChildren(typeof(T), true) as T;
    }

    private static Transform ResolveInteractionTransform(Transform origin)
    {
        if (origin == null)
        {
            return null;
        }

        if (IsInteractionTransform(origin))
        {
            return origin;
        }

        Transform parent = origin.parent;

        while (parent != null)
        {
            if (IsInteractionTransform(parent))
            {
                return parent;
            }

            parent = parent.parent;
        }

        return FindInteractionChild(origin);
    }

    private static bool IsInteractionTransform(Transform target)
    {
        return target != null &&
               (target.CompareTag(InteractTag) ||
                target.GetComponent(typeof(IInteractable)) != null ||
                target.GetComponent<CatchableAnimal>() != null);
    }

    private static Transform FindInteractionChild(Transform origin)
    {
        int childCount = origin.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = origin.GetChild(i);

            if (IsInteractionTransform(child))
            {
                return child;
            }

            Transform nestedChild = FindInteractionChild(child);

            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    private bool IsValidInteractionCollider(Collider other)
    {
        return other != null &&
               other.transform.root != transform.root &&
               ResolveInteractionTransform(other.transform) != null;
    }

    private void PruneTrackedColliders()
    {
        for (int i = trackedColliders.Count - 1; i >= 0; i--)
        {
            Collider trackedCollider = trackedColliders[i];

            if (trackedCollider == null ||
                !trackedCollider.enabled ||
                !trackedCollider.gameObject.activeInHierarchy ||
                ResolveInteractionTransform(trackedCollider.transform) == null)
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
}

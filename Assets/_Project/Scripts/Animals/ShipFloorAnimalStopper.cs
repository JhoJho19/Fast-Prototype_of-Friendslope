using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShipFloorAnimalStopper : MonoBehaviour
{
    [SerializeField, Min(0f)] private float triggerHeight = 0.6f;
    [SerializeField] private LayerMask animalLayers = ~0;
    [SerializeField, Min(0.02f)] private float checkInterval = 0.1f;

    private BoxCollider triggerVolume;
    private Collider[] overlapBuffer;
    private float nextCheckTime;

    private void Awake()
    {
        overlapBuffer = new Collider[64];
        triggerVolume = GetComponent<BoxCollider>();

        if (triggerVolume == null)
        {
            triggerVolume = gameObject.AddComponent<BoxCollider>();
            triggerVolume.isTrigger = true;
        }

        ConfigureTriggerVolume();
    }

    private void Update()
    {
        if (Time.time < nextCheckTime)
        {
            return;
        }

        nextCheckTime = Time.time + checkInterval;

        if (triggerVolume == null)
        {
            return;
        }

        Transform volumeTransform = triggerVolume.transform;

        Vector3 worldHalfExtents =
            volumeTransform.TransformVector(triggerVolume.size * 0.5f);

        worldHalfExtents = new Vector3(
            Mathf.Abs(worldHalfExtents.x),
            Mathf.Abs(worldHalfExtents.y),
            Mathf.Abs(worldHalfExtents.z));

        int hitCount = Physics.OverlapBoxNonAlloc(
            volumeTransform.TransformPoint(triggerVolume.center),
            worldHalfExtents,
            overlapBuffer,
            volumeTransform.rotation,
            animalLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];

            if (hit == null ||
                hit.transform.root == transform.root)
            {
                continue;
            }

            CatchableAnimal animal =
                hit.GetComponentInParent<CatchableAnimal>();

            if (animal != null)
            {
                animal.Freeze();
            }
        }
    }

    private void ConfigureTriggerVolume()
    {
        if (triggerVolume == null ||
            !Application.isPlaying)
        {
            return;
        }

        Renderer floorRenderer = GetComponent<Renderer>();

        if (floorRenderer == null)
        {
            return;
        }

        Bounds floorBounds = floorRenderer.bounds;

        Vector3 size = floorBounds.size;
        size.y = Mathf.Max(size.y, triggerHeight);

        Transform volumeTransform = triggerVolume.transform;
        triggerVolume.center =
            volumeTransform.InverseTransformPoint(floorBounds.center);
        triggerVolume.size =
            volumeTransform.InverseTransformVector(size);
    }
}

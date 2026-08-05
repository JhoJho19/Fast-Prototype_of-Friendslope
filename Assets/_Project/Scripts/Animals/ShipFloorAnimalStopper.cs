using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShipFloorAnimalStopper : MonoBehaviour
{
    private void Awake()
    {
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CatchableAnimal animal =
            other.GetComponentInParent<CatchableAnimal>();

        if (animal != null)
        {
            animal.Freeze();
        }
    }
}

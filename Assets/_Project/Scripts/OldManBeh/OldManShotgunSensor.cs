using System.Collections.Generic;
using UnityEngine;

public sealed class OldManShotgunSensor : MonoBehaviour
{
    private readonly HashSet<Collider> playerColliders = new();

    public bool IsPlayerInSights => playerColliders.Count > 0;

    private void Awake()
    {
        Collider sightCollider = GetComponent<Collider>();
        sightCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerColliders.Add(other);

        Debug.Log("Игроков в прицеле " + playerColliders.Count);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playerColliders.Remove(other))
        {
            return;
        }

        Debug.Log("Игроков в прицеле " + playerColliders.Count);
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerHealth>() != null;
    }

    private void OnDisable()
    {
        playerColliders.Clear();
    }
}

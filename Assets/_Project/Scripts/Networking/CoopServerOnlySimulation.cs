using Mirror;
using UnityEngine;

public sealed class CoopServerOnlySimulation : NetworkBehaviour
{
    [SerializeField]
    private string[] serverOnlyComponentNames =
    {
        "AnimalStateMachine",
        "AnimalMovement",
        "AnimalPatrol",
        "OldManStateMachine",
        "OldManMovement",
        "OldManPatrol",
        "OldManCombat"
    };

    private void Awake()
    {
        if (NetworkClient.active && !NetworkServer.active)
        {
            SetSimulationEnabled(false);
        }
    }

    public override void OnStartServer()
    {
        SetSimulationEnabled(true);
    }

    public override void OnStartClient()
    {
        if (!isServer)
        {
            SetSimulationEnabled(false);
        }
    }

    private void SetSimulationEnabled(bool isEnabled)
    {
        foreach (MonoBehaviour behaviour in
                 GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            foreach (string componentName in serverOnlyComponentNames)
            {
                if (behaviour.GetType().Name != componentName)
                {
                    continue;
                }

                behaviour.enabled = isEnabled;
                break;
            }
        }
    }
}

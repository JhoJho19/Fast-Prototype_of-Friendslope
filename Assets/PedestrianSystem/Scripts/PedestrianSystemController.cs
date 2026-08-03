namespace Nuggets10.PedestrianSystem
{
    #if UNITY_EDITOR
    using UnityEditor;
    #endif
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class PedestrianSystemController : MonoBehaviour
    {
        [Header("___________________________________________________________________________________________________________________________________________")]
        [Header("Pedestrian configuration values")]
        [Space(10)]
        [Tooltip("The pedestrian prefab")]
        public GameObject pedestrianPrefab;
        [Space(10)]

        [Tooltip("The minimum speed of the pedestrians")]
        public float minimumSpeed = 0.5f;

        [Tooltip("The maximum speed of the pedestrians")]
        public float maximumSpeed = 2.0f;
        [Space(10)]

        [Tooltip("The number of pedestrians to spawn")]
        public int pedestrianNumber = 5;
        private Transform selectedNodeSpawnPoint;
        [Space(10)]
        [Tooltip("Do the pedestrians have a random wait time at each node?")]
        public bool hasWaitTime = false;

        [Tooltip("Minimum amount of time a pedestrian can wait at a node before moving to the next one")]
        public float minimumWaitTime = 0f;
        [Tooltip("Maximum amount of time a pedestrian can wait at a node before moving to the next one")]
        public float maximumWaitTime = 1f;
        [Space(10)]
        [Tooltip("Do the pedestrians change their color each time they spawn?")]
        public bool useRandomColor = false;
        public List<Color> colors = new List<Color>();

        [Header("___________________________________________________________________________________________________________________________________________")]
        [Header("Nodes configuration")]
        [Space(10)]
        [Tooltip("The parent object of all nodes")]
        public GameObject nodesParent;
        private GameObject mainNode;
        private List<GameObject> nodes = new List<GameObject>();

        void Start()
        {
            if (minimumWaitTime > maximumWaitTime)
            {
                Debug.LogError("Minimum pedestrian wait time cannot be greater than maximum pedestrian wait time");
                return;
            }

            foreach (Transform child in nodesParent.transform) // Check if the nodes are valid
            {
                if (child.gameObject.GetComponent<PedestrianSystemNode>() != null)
                {
                    nodes.Add(child.gameObject);
                    Debug.Log("Node " + child.gameObject.name + " added to the nodes list");
                }
                else
                {
                    Debug.LogError("Node " + child.gameObject.name + " is missing a PedestrianSystemNode component");
                }
            }

            // Instantiate pedestrians. If the number of pedestrians to be instantiated is less than the number of nodes, each pedestrian will spawn on a different node.
            // Otherwise, if the number of pedestrians exceeds the number of nodes, the extra pedestrians will spawn on already occupied nodes.

            HashSet<Transform> occupiedNodes = new HashSet<Transform>();
            int currentInstantiatedPedestrians = 0;

            while (currentInstantiatedPedestrians < pedestrianNumber) 
            {
                Transform selectedNodeSpawnPoint;

                if (occupiedNodes.Count < nodes.Count)
                {
                    do
                    {
                        selectedNodeSpawnPoint = nodes[Random.Range(0, nodes.Count)].transform;
                    }
                    while (occupiedNodes.Contains(selectedNodeSpawnPoint));

                    occupiedNodes.Add(selectedNodeSpawnPoint);
                }
                else
                {
                    selectedNodeSpawnPoint = nodes[Random.Range(0, nodes.Count)].transform;
                }

                GameObject newPedestrian = Instantiate(pedestrianPrefab, selectedNodeSpawnPoint.position, Quaternion.identity);

                if (useRandomColor && colors.Count > 0)
                {
                    Color randomColor = colors[Random.Range(0, colors.Count)];
                    Renderer pedestrianRenderer = newPedestrian.GetComponent<Renderer>();
                    if (pedestrianRenderer != null)
                    {
                        pedestrianRenderer.material.color = randomColor;
                    }
                }

                float randomSpeed = Random.Range(minimumSpeed, maximumSpeed);

                PedestrianMover mover = newPedestrian.GetComponent<PedestrianMover>();
                if (mover != null)
                {
                    mover.Initialize(
                        selectedNodeSpawnPoint.GetComponent<PedestrianSystemNode>(), 
                        randomSpeed,
                        hasWaitTime, 
                        minimumWaitTime, 
                        maximumWaitTime
                    );
                }

                currentInstantiatedPedestrians++;
            }
        }

        // Function to add the node prefab to the scene through the Inspector

        public void AddNode()
        {
            #if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:Prefab PedestrianSystemNodePrefab");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                mainNode = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            #endif

            Instantiate(mainNode, new Vector3(0, 0, 0), Quaternion.identity);
            Debug.Log("Node added to the scene at position (0, 0, 0)");
        }
    }
}

namespace Nuggets10.PedestrianSystem
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class PedestrianMover : MonoBehaviour
    {
        private float speed;
        private bool hasWaitTime;
        private float minWaitTime;
        private float maxWaitTime;
        private PedestrianSystemNode currentNode;

        // Get the value of each variable from the main script

        public void Initialize(PedestrianSystemNode startNode, float pedestrianSpeed, bool waitTimeEnabled, float minWait, float maxWait)
        {
            currentNode = startNode;
            speed = pedestrianSpeed;
            hasWaitTime = waitTimeEnabled;
            minWaitTime = minWait;
            maxWaitTime = maxWait;

            StartCoroutine(WalkPath());
        }

        // Move the pedestrians from an adiacent node to another in loop, at the speed defined in the inspector.
        // If the bool hasWaitTime is set to True, pedestrians will wait a random amount of time (also defined in the Inspector) at each node

        private IEnumerator WalkPath()
        {
            while (true)
            {
                if (currentNode.adiacentNodes.Count == 0)
                {
                    Debug.LogWarning(gameObject.name + " hasn't got any adiacent nodes.");
                    yield break;
                }

                GameObject nextNode = currentNode.adiacentNodes[Random.Range(0, currentNode.adiacentNodes.Count)];
                Vector3 targetPosition = nextNode.transform.position;

                while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
                    yield return null;
                }

                currentNode = nextNode.GetComponent<PedestrianSystemNode>();

                if (hasWaitTime)
                {
                    float waitTime = Random.Range(minWaitTime, maxWaitTime);
                    yield return new WaitForSeconds(waitTime);
                }
            }
        }
    }
}

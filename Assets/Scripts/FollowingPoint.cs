using UnityEngine;

public class FollowingPoint : MonoBehaviour
{
    [SerializeField] private GameObject[] points;
    private int currentPointIndex = 0;
    [SerializeField] private float speed = 2f;

    void Update()
    {
        if (points == null || points.Length == 0) return;
        if (points[currentPointIndex] == null) return;

        if (Vector2.Distance(points[currentPointIndex].transform.position, transform.position) < 0.1f)
        {
            currentPointIndex++;
            if (currentPointIndex >= points.Length) currentPointIndex = 0;
            if (points[currentPointIndex] == null) return;
        }

        transform.position = Vector2.MoveTowards(
            transform.position,
            points[currentPointIndex].transform.position,
            Time.deltaTime * speed
        );
    }
}

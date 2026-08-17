using UnityEngine;

[DefaultExecutionOrder(-200)]
public class FollowingPoint : MonoBehaviour
{
    [SerializeField] private GameObject[] points;
    private int currentPointIndex = 0;
    [SerializeField] private float speed = 2f;

    Rigidbody2D rb;

    public Vector2 DeltaPosition { get; private set; }
    public Vector2 Velocity => Time.fixedDeltaTime > 0f
        ? DeltaPosition / Time.fixedDeltaTime
        : Vector2.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void FixedUpdate()
    {
        DeltaPosition = Vector2.zero;
        if (points == null || points.Length == 0 || !points[currentPointIndex]) return;

        Vector2 currentPosition = rb.position;
        Vector2 targetPosition = points[currentPointIndex].transform.position;
        if (Vector2.Distance(targetPosition, currentPosition) < 0.1f)
        {
            currentPointIndex = (currentPointIndex + 1) % points.Length;
            if (!points[currentPointIndex]) return;
            targetPosition = points[currentPointIndex].transform.position;
        }

        Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition,
            speed * Time.fixedDeltaTime);
        DeltaPosition = nextPosition - currentPosition;
        rb.MovePosition(nextPosition);
    }
}

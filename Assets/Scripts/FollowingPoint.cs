using UnityEngine;

[DefaultExecutionOrder(-200)]
public class FollowingPoint : MonoBehaviour
{
    [SerializeField] private GameObject[] points;
    private int currentPointIndex = 0;
    [SerializeField] private float speed = 2f;

    Rigidbody2D rb;

    public bool TryGetRoute(out Vector2 start, out Vector2 end)
    {
        start = end = transform.position;
        if (points == null || points.Length != 2 || !points[0] || !points[1]) return false;
        start = points[0].transform.position;
        end = points[1].transform.position;
        return true;
    }

    public bool SetRoute(Vector2 start, Vector2 end, float startFraction)
    {
        if (!TryGetRoute(out _, out _)) return false;
        points[0].transform.position = start;
        points[1].transform.position = end;
        Vector2 initial = Vector2.Lerp(start, end, startFraction);
        transform.position = initial;
        rb.position = initial;
        currentPointIndex = startFraction > 0f ? 0 : 1;
        DeltaPosition = Vector2.zero;
        return true;
    }

    public GameObject RouteOwner => transform.parent && points != null && points.Length == 2 &&
        points[0] && points[1] && points[0].transform.parent == transform.parent &&
        points[1].transform.parent == transform.parent && transform.parent.childCount == 3
        ? transform.parent.gameObject : gameObject;

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
        if ((targetPosition - currentPosition).sqrMagnitude < 0.000001f)
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

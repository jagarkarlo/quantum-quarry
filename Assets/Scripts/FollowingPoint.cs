using UnityEngine;

[DefaultExecutionOrder(-200)]
public class FollowingPoint : MonoBehaviour
{
    [SerializeField] private GameObject[] points;
    private int currentPointIndex = 0;
    [SerializeField] private float speed = 2f;

    Rigidbody2D rb;
    Collider2D deck;
    ContactFilter2D routeFilter;
    readonly RaycastHit2D[] obstacles = new RaycastHit2D[4];
    bool customRoute;

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
        customRoute = true;
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
        deck = GetComponent<Collider2D>();
        routeFilter.SetLayerMask(LayerMask.GetMask("Ground", "Jump"));
        routeFilter.useTriggers = false;
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
        if (customRoute && deck && DeltaPosition.sqrMagnitude > 0f)
        {
            float distance = DeltaPosition.magnitude;
            Vector2 direction = DeltaPosition / distance;
            int hits = deck.Cast(direction, routeFilter, obstacles, distance + 0.02f);
            if (hits > 0)
            {
                float safe = distance;
                for (int i = 0; i < hits; i++)
                    safe = Mathf.Min(safe, Mathf.Max(0f, obstacles[i].distance - 0.02f));
                DeltaPosition = direction * safe;
                nextPosition = currentPosition + DeltaPosition;
                currentPointIndex = 1 - currentPointIndex;
            }
        }
        rb.MovePosition(nextPosition);
    }
}

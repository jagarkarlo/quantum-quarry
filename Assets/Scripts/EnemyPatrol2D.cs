using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPatrol2D : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Alert,
        Chase,
        Search
    }

    [Header("Movement")]
    [SerializeField] float speed = 1.5f;

    [Header("Awareness")]
    [SerializeField] float baseDetectionRange = 3f;
    [SerializeField] float detectionRangePerLevel = 0.6f;
    [SerializeField] float verticalDetectionRange = 2.5f;
    [SerializeField] float alertDuration = 0.35f;
    [SerializeField] float searchDuration = 2f;
    [SerializeField] float chaseSpeedMultiplier = 1.35f;
    [SerializeField] float difficultyPerLevel = 0.08f;

    [Header("Raycast Checks")]
    [SerializeField] Transform groundCheck;
    [SerializeField] Transform wallCheck;
    [SerializeField] float groundCheckDist = 0.25f;
    [SerializeField] float wallCheckDist = 0.1f;
    [SerializeField] LayerMask groundMask;

    Rigidbody2D rb;
    PlayerMovement player;
    GameSession session;
    EnemyState currentState;
    float stateTimer;
    int levelNumber = 1;
    int dir = 1;
    int supportMask;
    bool bounded;
    float laneLeft, laneRight;

    public void SetPatrolLane(float left, float right)
    {
        bounded = true;
        laneLeft = left;
        laneRight = right;
    }

    public EnemyState CurrentState => currentState;
    public float DetectionRange => (baseDetectionRange + (levelNumber - 1) * detectionRangePerLevel)
        * (session ? session.Pressure.PerceptionMultiplier : 1f);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        supportMask = groundMask.value | LayerMask.GetMask("Jump");
        dir = transform.localScale.x < 0f ? -1 : 1;
        levelNumber = GetLevelNumber(SceneManager.GetActiveScene().name);
        SetState(EnemyState.Patrol);
    }

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        FindPlayer();
    }

    void FixedUpdate()
    {
        if (!player) FindPlayer();

        switch (currentState)
        {
            case EnemyState.Patrol:
                Patrol();
                break;
            case EnemyState.Alert:
                Alert();
                break;
            case EnemyState.Chase:
                Chase();
                break;
            case EnemyState.Search:
                Search();
                break;
        }
    }

    void Patrol()
    {
        Move(speed);

        if (CanSeePlayer(true)) SetState(EnemyState.Alert);
    }

    void Alert()
    {
        StopMoving();
        FacePlayer();
        stateTimer -= Time.fixedDeltaTime;

        if (!CanSeePlayer(false)) SetState(EnemyState.Search);
        else if (stateTimer <= 0f) SetState(EnemyState.Chase);
    }

    void Chase()
    {
        if (!CanSeePlayer(false))
        {
            SetState(EnemyState.Search);
            return;
        }

        FacePlayer();
        float difficulty = 1f + (levelNumber - 1) * difficultyPerLevel;
        float pressureSpeed = session ? session.Pressure.ChaseMultiplier : 1f;
        Move(speed * chaseSpeedMultiplier * difficulty * pressureSpeed, false);
    }

    void Search()
    {
        Move(speed);
        stateTimer -= Time.fixedDeltaTime;

        if (CanSeePlayer(false)) SetState(EnemyState.Chase);
        else if (stateTimer <= 0f) SetState(EnemyState.Patrol);
    }

    void SetState(EnemyState nextState)
    {
        currentState = nextState;
        stateTimer = nextState == EnemyState.Alert ? alertDuration : searchDuration;
    }

    void Move(float moveSpeed, bool turnAround = true)
    {
        float step = moveSpeed * Time.fixedDeltaTime;
        float travel = SafeTravel(dir, step);
        if (travel <= 0f && turnAround)
        {
            travel = SafeTravel(-dir, step);
            if (travel > 0f) Flip();
        }
        rb.velocity = new Vector2(dir * travel / Time.fixedDeltaTime, rb.velocity.y);
    }

    float SafeTravel(int direction, float step)
    {
        if (!groundCheck || !wallCheck) return 0f;
        if (bounded)
            step = Mathf.Min(step, direction > 0 ? laneRight - rb.position.x : rb.position.x - laneLeft);
        if (step <= 0.00001f) return 0f;
        Vector2 ground = ProbePosition(groundCheck, direction) + Vector2.right * (direction * step);
        Vector2 wall = ProbePosition(wallCheck, direction);
        bool supported = Physics2D.Raycast(ground, Vector2.down, groundCheckDist, supportMask);
        bool obstructed = Physics2D.Raycast(wall, Vector2.right * direction, wallCheckDist + step, groundMask);
        return supported && !obstructed ? step : 0f;
    }

    Vector2 ProbePosition(Transform probe, int direction) =>
        new Vector2(rb.position.x + direction * Mathf.Abs(probe.position.x - transform.position.x),
            rb.position.y + probe.position.y - transform.position.y);

    bool CanSeePlayer(bool requireFacingPlayer)
    {
        if (!player || !player.IsAlive || player.IsInvisible) return false;

        Vector2 offset = player.transform.position - transform.position;
        if (Mathf.Abs(offset.y) > verticalDetectionRange || offset.sqrMagnitude > DetectionRange * DetectionRange)
            return false;

        if (requireFacingPlayer && offset.x * dir < 0f) return false;

        return !Physics2D.Linecast(transform.position, player.transform.position, groundMask);
    }

    void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject) player = playerObject.GetComponent<PlayerMovement>();
    }

    void FacePlayer()
    {
        if (!player) return;

        float horizontalOffset = player.transform.position.x - transform.position.x;
        if (Mathf.Abs(horizontalOffset) > Mathf.Epsilon)
        {
            int targetDirection = horizontalOffset > 0f ? 1 : -1;
            if (targetDirection != dir) Flip();
        }
    }

    void StopMoving()
    {
        rb.velocity = new Vector2(0f, rb.velocity.y);
    }

    void Flip()
    {
        dir *= -1;
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        transform.localScale = s;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? DetectionRange : baseDetectionRange);

        if (groundCheck)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDist);
        }
        if (wallCheck)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + Vector3.right * wallCheckDist);
        }
    }

    static int GetLevelNumber(string sceneName)
    {
        if (!sceneName.StartsWith("Level ")) return 1;
        return int.TryParse(sceneName.Substring(6), out int parsedLevel) ? Mathf.Max(1, parsedLevel) : 1;
    }
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPatrol2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float speed = 1.5f;

    [Header("Raycast Checks")]
    [SerializeField] Transform groundCheck;      // child at front, slightly below feet
    [SerializeField] Transform wallCheck;        // child at front, at body height
    [SerializeField] float groundCheckDist = 0.25f;
    [SerializeField] float wallCheckDist = 0.1f;
    [SerializeField] LayerMask groundMask;       // set to your Ground layer(s)

    Rigidbody2D rb;
    int dir = 1; // 1 = right, -1 = left

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    void FixedUpdate()
    {
        // If checks not assigned, just move and flip by velocity sign
        if (!groundCheck || !wallCheck)
        {
            rb.velocity = new Vector2(dir * speed, rb.velocity.y);
            AutoFlip();
            return;
        }

        bool groundAhead = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDist, groundMask);
        bool wallAhead   = Physics2D.Raycast(wallCheck.position, Vector2.right * dir, wallCheckDist, groundMask);

        if (!groundAhead || wallAhead)
            Flip();

        rb.velocity = new Vector2(dir * speed, rb.velocity.y);
    }

    void Flip()
    {
        dir *= -1;
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        transform.localScale = s;
    }

    void AutoFlip()
    {
        if (Mathf.Abs(rb.velocity.x) > Mathf.Epsilon)
        {
            int vdir = rb.velocity.x > 0 ? 1 : -1;
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * vdir;
            transform.localScale = s;
        }
    }

    void OnDrawGizmosSelected()
    {
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
}

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float runSpeed = 10f;
    [SerializeField] float jumpSpeed = 5f;
    [SerializeField] float climbSpeed = 5f;
    [SerializeField] Vector2 deathKick = new Vector2(10f, 10f);

    [Header("Combat")]
    [SerializeField] GameObject bullet;
    [SerializeField] Transform gun;

    [Header("Powerup Durations")]
    [SerializeField] float speedBoostMultiplier = 1.5f;
    [SerializeField] float defaultSpeedBoostSeconds = 10f;     // used only when queued by Store
    [SerializeField] float defaultInvisibilitySeconds = 10f;   // used only when queued by Store
    // removed defaultDoubleJumpSeconds warning source
    [SerializeField] int defaultDoubleJumpSeconds = 10;        // used only if queued by Store

    [Header("Optional Timer UI (assign in scene)")]
    [SerializeField] PowerupTimerUI speedBoostTimerUI;
    [SerializeField] PowerupTimerUI invisTimerUI;
    [SerializeField] PowerupTimerUI doubleJumpTimerUI;

    // runtime
    Vector2 moveInput;
    Rigidbody2D rb;
    Animator anim;
    CapsuleCollider2D bodyCol;
    BoxCollider2D feetCol;
    PlayerInput playerInput;

    float gravityAtStart;
    bool isAlive = true;

    // states
    bool speedBoostActive = false;
    bool invisibleActive = false;
    bool doubleJumpActive = false;
    bool usedDoubleJump = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        bodyCol = GetComponent<CapsuleCollider2D>();
        feetCol = GetComponent<BoxCollider2D>();
        playerInput = GetComponent<PlayerInput>();
        gravityAtStart = rb.gravityScale;

        // Auto-wire timers if left unassigned in Inspector
        if (!speedBoostTimerUI)  speedBoostTimerUI  = FindTimerByNamePart("speed");
        if (!invisTimerUI)       invisTimerUI       = FindTimerByNamePart("invis");
        if (!doubleJumpTimerUI)  doubleJumpTimerUI  = FindTimerByNamePart("double");

        // Consume queued powerups from the Store
        if (PlayerPrefs.GetInt(GameSession.SpeedBoostQueuedKey, 0) == 1)
        {
            PlayerPrefs.DeleteKey(GameSession.SpeedBoostQueuedKey);
            PlayerPrefs.Save();
            ActivateSpeedBoost(defaultSpeedBoostSeconds);
        }

        if (PlayerPrefs.GetInt(GameSession.InvisibilityQueuedKey, 0) == 1)
        {
            PlayerPrefs.DeleteKey(GameSession.InvisibilityQueuedKey);
            PlayerPrefs.Save();
            ActivateInvisibility(defaultInvisibilitySeconds);
        }

        int doubleJumpQueued = PlayerPrefs.GetInt(GameSession.DoubleJumpQueuedSecs, 0);
        if (doubleJumpQueued > 0)
        {
            PlayerPrefs.DeleteKey(GameSession.DoubleJumpQueuedSecs);
            PlayerPrefs.Save();
            ActivateDoubleJump(doubleJumpQueued);
        }
        // IMPORTANT: we do NOT auto-activate any powerup at start unless queued.
    }

    PowerupTimerUI FindTimerByNamePart(string namePart)
    {
        var timers = FindObjectsOfType<PowerupTimerUI>(true);
        foreach (var t in timers)
            if (t.name.ToLower().Contains(namePart.ToLower()))
                return t;
        return null;
    }

    void Update()
    {
        if (!isAlive) return;

        // reset double-jump when on ground
        if (feetCol.IsTouchingLayers(LayerMask.GetMask("Ground"))) usedDoubleJump = false;

        if (invisibleActive)
        {
            GhostMove(); // free-flight while invisible
            FlipSpriteFromVelocity();
            return; // skip normal movement/hazards while ghosting
        }

        Run();
        FlipSpriteFromVelocity();
        ClimbLadder();
        Die();
    }

    // ---------- Input ----------
    void OnMove(InputValue value)
    {
        if (!isAlive) return;
        moveInput = value.Get<Vector2>();
    }

    void OnJump(InputValue value)
    {
        if (!isAlive) return;
        if (!value.isPressed) return;

        if (invisibleActive) return; // ghost mode uses free-flight, no jump

        bool onGround = feetCol.IsTouchingLayers(LayerMask.GetMask("Ground"));
        if (onGround)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpSpeed);
            usedDoubleJump = false;
        }
        else if (doubleJumpActive && !usedDoubleJump)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpSpeed);
            usedDoubleJump = true;
        }
    }

    void OnFire(InputValue value)
    {
        if (!isAlive) return;

        // Do not shoot if time is frozen (pause/store)
        if (Time.timeScale == 0f) return;

        // Avoid EventSystem checks here (causes warning in callbacks).
        // If you use multiple action maps, ensure current map is "Player" when in gameplay.
        Instantiate(bullet, gun.position, transform.rotation);
    }

    // ---------- Movement helpers ----------
    void Run()
    {
        Vector2 vel = new Vector2(moveInput.x * runSpeed, rb.velocity.y);
        rb.velocity = vel;

        bool hasX = Mathf.Abs(rb.velocity.x) > Mathf.Epsilon;
        if (anim) anim.SetBool("run", hasX);
    }

    void GhostMove()
    {
        float ghostSpeed = runSpeed;
        rb.velocity = new Vector2(moveInput.x * ghostSpeed, moveInput.y * ghostSpeed);
    }

    void FlipSpriteFromVelocity()
    {
        bool hasX = Mathf.Abs(rb.velocity.x) > Mathf.Epsilon;
        if (hasX) transform.localScale = new Vector2(Mathf.Sign(rb.velocity.x), 1f);
    }

    void ClimbLadder()
    {
        if (!feetCol.IsTouchingLayers(LayerMask.GetMask("Climbing")))
        {
            rb.gravityScale = gravityAtStart;
            if (anim) anim.SetBool("climbing", false);
            return;
        }

        Vector2 climbVel = new Vector2(rb.velocity.x, moveInput.y * climbSpeed);
        rb.velocity = climbVel;
        rb.gravityScale = 0f;

        bool hasY = Mathf.Abs(rb.velocity.y) > Mathf.Epsilon;
        if (anim) anim.SetBool("climbing", hasY);
    }

    void Die()
    {
        if (bodyCol.IsTouchingLayers(LayerMask.GetMask("Enemy", "Water", "Hazard")))
        {
            isAlive = false;
            if (anim) anim.SetTrigger("dead");
            rb.velocity = deathKick;
            FindObjectOfType<GameSession>().ProcessPlayerDeath();
        }
    }

    // Immediately restore player to normal physics (use before leaving the scene)
    public void CancelAllPowerupsImmediately()
    {
        StopAllCoroutines();

        speedBoostActive = false;
        invisibleActive  = false;
        doubleJumpActive = false;
        usedDoubleJump   = false;

        rb.velocity = Vector2.zero;
        rb.gravityScale = gravityAtStart;

        if (bodyCol) bodyCol.enabled = true;
        if (feetCol) feetCol.enabled = true;

        if (speedBoostTimerUI) speedBoostTimerUI.StopTimer();
        if (invisTimerUI)      invisTimerUI.StopTimer();
        if (doubleJumpTimerUI) doubleJumpTimerUI.StopTimer();
    }

    // ---------- Powerups (public API) ----------
    public void ActivateSpeedBoost(float seconds) { if (!speedBoostActive) StartCoroutine(SpeedBoostCR(seconds)); }
    public void ActivateInvisibility(float seconds) { if (!invisibleActive) StartCoroutine(InvisibilityCR(seconds)); }
    public void ActivateDoubleJump(int seconds) { if (!doubleJumpActive) StartCoroutine(DoubleJumpCR(seconds)); }

    public void SetDoubleJumpUnlocked(bool enabled)
    {
        StopCoroutineSafe(doubleJumpRoutine);
        doubleJumpActive = enabled;
        usedDoubleJump = false;
        if (enabled && doubleJumpTimerUI) doubleJumpTimerUI.StopTimer(); // no timer for permanent
    }

    Coroutine speedRoutine, invisRoutine, doubleJumpRoutine;

    IEnumerator SpeedBoostCR(float seconds)
    {
        speedBoostActive = true;
        float original = runSpeed;
        runSpeed *= speedBoostMultiplier;

        if (speedBoostTimerUI) speedBoostTimerUI.StartTimer(seconds);

        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }

        runSpeed = original;
        speedBoostActive = false;
        if (speedBoostTimerUI) speedBoostTimerUI.StopTimer();
    }

    IEnumerator InvisibilityCR(float seconds)
    {
        invisibleActive = true;

        float oldGrav = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;
        bool bodyWas = bodyCol.enabled, feetWas = feetCol.enabled;
        bodyCol.enabled = false; feetCol.enabled = false;

        if (invisTimerUI) invisTimerUI.StartTimer(seconds);

        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }

        bodyCol.enabled = bodyWas; feetCol.enabled = feetWas;
        rb.gravityScale = oldGrav;
        invisibleActive = false;
        if (invisTimerUI) invisTimerUI.StopTimer();
    }

    IEnumerator DoubleJumpCR(int seconds)
    {
        doubleJumpActive = true;
        usedDoubleJump = false;

        if (doubleJumpTimerUI) doubleJumpTimerUI.StartTimer(seconds);

        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }

        doubleJumpActive = false;
        usedDoubleJump = false;
        if (doubleJumpTimerUI) doubleJumpTimerUI.StopTimer();
    }

    void StopCoroutineSafe(Coroutine c)
    {
        if (c != null) StopCoroutine(c);
    }
}

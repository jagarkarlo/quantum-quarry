using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float runSpeed = 10f;
    [SerializeField] float jumpSpeed = 5f;
    [SerializeField] float climbSpeed = 5f;
    [SerializeField] Vector2 deathKick = new Vector2(10f, 10f);

    [Header("Swimming")]
    [SerializeField] float swimSpeed = 5f;
    [SerializeField] float swimAcceleration = 18f;
    [SerializeField] float waterGravityMultiplier = 0.08f;
    [SerializeField] float swimJumpSpeed = 7f;

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
    Tilemap[] levelTilemaps;
    LiquidKind currentLiquid;
    int levelNumber = 1;

    // states
    bool speedBoostActive = false;
    bool invisibleActive = false;
    bool doubleJumpActive = false;
    bool usedDoubleJump = false;
    Vector2 lastSafeGhostPosition;
    StickyPlatform activePlatform;
    float platformAttachBlockedUntil;

    public bool IsAlive => isAlive;
    public bool IsInvisible => invisibleActive;
    public bool IsSwimming => currentLiquid == LiquidKind.Water && !invisibleActive;
    public bool IsInLava => currentLiquid == LiquidKind.Lava;
    public bool IsSubmerged { get; private set; }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        bodyCol = GetComponent<CapsuleCollider2D>();
        feetCol = GetComponent<BoxCollider2D>();
        playerInput = GetComponent<PlayerInput>();
        gravityAtStart = rb.gravityScale;
        levelNumber = GetLevelNumber(SceneManager.GetActiveScene().name);
        levelTilemaps = FindObjectsOfType<Tilemap>();
        TintLavaTiles();

        // Auto-wire timers if left unassigned in Inspector
        if (!speedBoostTimerUI)  speedBoostTimerUI  = FindTimerByNamePart("speed");
        if (!invisTimerUI)       invisTimerUI       = FindTimerByNamePart("invis");
        if (!doubleJumpTimerUI)  doubleJumpTimerUI  = FindTimerByNamePart("double");

        // Consume queued powerups from the Store
        int speedBoostQueued = GameSession.ConsumeQueuedPowerupSeconds(
            GameSession.SpeedBoostQueuedKey, Mathf.RoundToInt(defaultSpeedBoostSeconds));
        if (speedBoostQueued > 0)
        {
            ActivateSpeedBoost(speedBoostQueued);
        }

        int invisibilityQueued = GameSession.ConsumeQueuedPowerupSeconds(
            GameSession.InvisibilityQueuedKey, Mathf.RoundToInt(defaultInvisibilitySeconds));
        if (invisibilityQueued > 0)
        {
            ActivateInvisibility(invisibilityQueued);
        }

        int doubleJumpQueued = GameSession.ConsumeQueuedPowerupSeconds(
            GameSession.DoubleJumpQueuedSecs, defaultDoubleJumpSeconds);
        if (doubleJumpQueued > 0)
        {
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

        currentLiquid = GetLiquidAtPlayer();
        IsSubmerged = currentLiquid == LiquidKind.Water && IsHeadInWater();

        // reset double-jump when on ground
        if (feetCol.IsTouchingLayers(LayerMask.GetMask("Ground"))) usedDoubleJump = false;

        if (invisibleActive)
        {
            GhostMove(); // free-flight while invisible
            FlipSpriteFromVelocity();
            return; // skip normal movement/hazards while ghosting
        }

        if (IsSwimming)
        {
            Swim();
            FlipSpriteFromVelocity();
            return;
        }

        rb.gravityScale = gravityAtStart;
        Run();
        FlipSpriteFromVelocity();
        ClimbLadder();
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

        if (IsSwimming)
        {
            rb.velocity = new Vector2(rb.velocity.x, swimJumpSpeed);
            return;
        }

        bool onGround = feetCol.IsTouchingLayers(LayerMask.GetMask("Ground"));
        if (onGround)
        {
            Vector2 inheritedVelocity = activePlatform ? activePlatform.Velocity : Vector2.zero;
            DetachFromPlatform(activePlatform, true);
            rb.velocity = new Vector2(moveInput.x * runSpeed + inheritedVelocity.x,
                jumpSpeed + Mathf.Max(0f, inheritedVelocity.y));
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
        float platformVelocity = activePlatform ? activePlatform.Velocity.x : 0f;
        Vector2 vel = new Vector2(moveInput.x * runSpeed + platformVelocity, rb.velocity.y);
        rb.velocity = vel;

        bool hasX = Mathf.Abs(rb.velocity.x) > Mathf.Epsilon;
        if (anim) anim.SetBool("run", hasX);
    }

    void GhostMove()
    {
        float ghostSpeed = runSpeed;
        rb.velocity = new Vector2(moveInput.x * ghostSpeed, moveInput.y * ghostSpeed);
    }

    void Swim()
    {
        DetachFromPlatform(activePlatform, false);
        rb.gravityScale = gravityAtStart * waterGravityMultiplier;
        Vector2 targetVelocity = moveInput * swimSpeed;
        rb.velocity = Vector2.MoveTowards(rb.velocity, targetVelocity,
            swimAcceleration * Time.deltaTime);

        if (anim)
        {
            anim.SetBool("run", Mathf.Abs(rb.velocity.x) > Mathf.Epsilon);
            anim.SetBool("climbing", Mathf.Abs(rb.velocity.y) > Mathf.Epsilon);
        }
    }

    public LiquidKind GetLiquidAtPlayer()
    {
        if (levelTilemaps == null || levelTilemaps.Length == 0)
            levelTilemaps = FindObjectsOfType<Tilemap>();

        Bounds bounds = bodyCol ? bodyCol.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
        LiquidKind result = LiquidKind.None;
        foreach (Tilemap tilemap in levelTilemaps)
        {
            if (!tilemap || !tilemap.isActiveAndEnabled) continue;

            LiquidKind liquid = GetLiquidInBounds(tilemap, bounds);
            if (liquid == LiquidKind.Lava) return liquid;
            if (liquid == LiquidKind.Water) result = liquid;
        }

        return result;
    }

    public LiquidKind GetContactLiquid(GameObject contactObject)
    {
        Tilemap tilemap = contactObject ? contactObject.GetComponent<Tilemap>() : null;
        if (!tilemap) return LiquidKind.None;

        Bounds bounds = bodyCol ? bodyCol.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
        Vector3 center = bounds.center;
        LiquidKind result = LiquidKind.None;

        if (!MergeContactTile(tilemap, center, ref result)) return LiquidKind.None;
        if (!MergeContactTile(tilemap,
            new Vector3(center.x, bounds.min.y + 0.05f, center.z), ref result))
            return LiquidKind.None;
        if (!MergeContactTile(tilemap,
            new Vector3(center.x, bounds.max.y - 0.05f, center.z), ref result))
            return LiquidKind.None;

        return result;
    }

    bool MergeContactTile(Tilemap tilemap, Vector3 point, ref LiquidKind result)
    {
        TileBase tile = tilemap.GetTile(tilemap.WorldToCell(point));
        if (!tile) return true;

        LiquidKind liquid = LiquidRules.ClassifyTile(tile.name, levelNumber);
        if (liquid == LiquidKind.None) return false;

        result = MergeLiquid(result, liquid);
        return true;
    }

    LiquidKind GetLiquidInBounds(Tilemap tilemap, Bounds bounds)
    {
        Vector3 center = bounds.center;
        LiquidKind result = LiquidKind.None;
        result = MergeLiquid(result, GetLiquidAtPoint(tilemap, center));
        result = MergeLiquid(result, GetLiquidAtPoint(tilemap,
            new Vector3(center.x, bounds.min.y + 0.05f, center.z)));
        result = MergeLiquid(result, GetLiquidAtPoint(tilemap,
            new Vector3(center.x, bounds.max.y - 0.05f, center.z)));

        return result;
    }

    LiquidKind GetLiquidAtPoint(Tilemap tilemap, Vector3 point)
    {
        TileBase tile = tilemap.GetTile(tilemap.WorldToCell(point));
        return LiquidRules.ClassifyTile(tile ? tile.name : null, levelNumber);
    }

    static LiquidKind MergeLiquid(LiquidKind current, LiquidKind candidate)
    {
        return (LiquidKind)Mathf.Max((int)current, (int)candidate);
    }

    bool IsHeadInWater()
    {
        Bounds bounds = bodyCol ? bodyCol.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
        Vector3 headPosition = new Vector3(bounds.center.x, bounds.max.y - 0.05f, bounds.center.z);

        foreach (Tilemap tilemap in levelTilemaps)
        {
            if (!tilemap || !tilemap.isActiveAndEnabled) continue;

            TileBase tile = tilemap.GetTile(tilemap.WorldToCell(headPosition));
            if (LiquidRules.ClassifyTile(tile ? tile.name : null, levelNumber) == LiquidKind.Water)
                return true;
        }

        return false;
    }

    void TintLavaTiles()
    {
        if (levelNumber < 6 || levelTilemaps == null) return;

        Color lavaColor = new Color(1f, 0.28f, 0.05f, 1f);
        foreach (Tilemap tilemap in levelTilemaps)
        {
            if (!tilemap) continue;

            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                TileBase tile = tilemap.GetTile(cell);
                if (!tile || LiquidRules.ClassifyTile(tile.name, levelNumber) != LiquidKind.Lava) continue;

                tilemap.SetTileFlags(cell, TileFlags.None);
                tilemap.SetColor(cell, lavaColor);
            }
        }
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

    public void Kill(Vector2 kick)
    {
        if (!isAlive) return;

        isAlive = false;
        IsSubmerged = false;
        currentLiquid = LiquidKind.None;
        if (anim)
        {
            anim.SetBool("climbing", false);
            anim.SetBool("run", false);
            anim.SetTrigger("dead");
        }
        rb.velocity = kick == Vector2.zero ? deathKick : kick;

        GameSession session = FindObjectOfType<GameSession>();
        if (session) session.ProcessPlayerDeath();
        else Debug.LogError("GameSession not found: player death cannot be processed.");
    }

    public void AttachToPlatform(StickyPlatform platform)
    {
        if (!platform || invisibleActive || Time.time < platformAttachBlockedUntil) return;
        activePlatform = platform;
    }

    public void DetachFromPlatform(StickyPlatform platform, bool jumped)
    {
        if (platform && activePlatform != platform) return;
        activePlatform = null;
        if (jumped) platformAttachBlockedUntil = Time.time + 0.1f;
    }

    // Immediately restore player to normal physics (use before leaving the scene)
    public void CancelAllPowerupsImmediately()
    {
        StopAllCoroutines();
        DetachFromPlatform(activePlatform, false);

        speedBoostActive = false;
        if (invisibleActive) EndInvisibility(gravityAtStart, true, true);
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
        lastSafeGhostPosition = rb.position;

        float oldGrav = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;
        bool bodyWas = bodyCol.enabled, feetWas = feetCol.enabled;
        bodyCol.enabled = false; feetCol.enabled = false;

        if (invisTimerUI) invisTimerUI.StartTimer(seconds);

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            if (CanRematerializeAt(rb.position)) lastSafeGhostPosition = rb.position;
            yield return null;
        }

        EndInvisibility(oldGrav, bodyWas, feetWas);
    }

    bool CanRematerializeAt(Vector2 position)
    {
        Vector2 scale = new Vector2(Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y));
        Vector2 size = Vector2.Scale(bodyCol.size, scale) * 0.95f;
        Vector2 centerOffset = Vector2.Scale(bodyCol.offset, scale);
        Vector2 center = position + centerOffset;
        int solidLayers = LayerMask.GetMask("Ground", "Jump");
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = solidLayers,
            useTriggers = false
        };
        var overlaps = new Collider2D[1];

        return Physics2D.OverlapCapsule(center, size, bodyCol.direction,
            transform.eulerAngles.z, filter, overlaps) == 0;
    }

    void EndInvisibility(float restoredGravity, bool restoreBody, bool restoreFeet)
    {
        if (!CanRematerializeAt(rb.position))
        {
            rb.position = lastSafeGhostPosition;
            transform.position = lastSafeGhostPosition;
        }

        rb.velocity = Vector2.zero;
        bodyCol.enabled = restoreBody;
        feetCol.enabled = restoreFeet;
        rb.gravityScale = restoredGravity;
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

    static int GetLevelNumber(string sceneName)
    {
        if (!sceneName.StartsWith("Level ")) return 1;
        return int.TryParse(sceneName.Substring(6), out int parsedLevel) ? Mathf.Max(1, parsedLevel) : 1;
    }
}

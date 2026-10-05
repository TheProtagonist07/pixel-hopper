using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The hero: input, movement, jumping, animation, stomping and getting hurt.
///
/// How it is built: <see cref="Game"/> creates an empty GameObject named "Player" and adds this component.
/// Because of [RequireComponent], Unity automatically adds the Rigidbody2D and BoxCollider2D, and Awake then
/// configures them and builds the child objects (sprite and shadow) in code, so there is no prefab.
///
/// Movement feel (all tuned to be forgiving and responsive):
///  - Run speed 5.5 tiles per second, with fast acceleration (60) and braking (80).
///  - Jump apex is about 3.8 tiles (launch speed 15 at gravity scale 3). Holding jump gives the full jump;
///    releasing early cuts the rise short ("variable jump height").
///  - Gravity is stronger when falling (4.5 vs 3) so jumps feel snappy instead of floaty.
///  - Coyote time: you can still jump for 0.1 s after walking off a ledge.
///  - Jump buffering: pressing jump up to 0.12 s before landing still triggers the jump on landing.
///  - Squash and stretch on jump, land and stomp, plus a drop shadow that shrinks with height.
///
/// Physics runs in FixedUpdate (movement, ground check); input and visuals run in Update.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    // Physics layers. These two layers are not named in the Tag Manager; they are used purely as numbers
    // in layer masks so that rays and overlap tests can include or ignore the player and enemies.
    /// <summary>Physics layer the player lives on.</summary>
    public const int PlayerLayer = 8;
    /// <summary>Physics layer enemies live on.</summary>
    public const int EnemyLayer = 9;

    /// <summary>Layer mask of things that count as "ground": everything except the player and enemies.</summary>
    public static readonly int GroundMask = ~((1 << PlayerLayer) | (1 << EnemyLayer));

    /// <summary>Top running speed in tiles per second (also used by the camera for look-ahead).</summary>
    public const float MoveSpeed = 5.5f;

    // Tuning values. All velocities are in tiles (world units) per second.
    const float Accel = 60f;            // speeding up toward the input direction
    const float Decel = 80f;            // slowing down when there is no input
    const float JumpVelocity = 15f;     // upward launch speed of a jump
    const float StompBounce = 12f;      // upward speed after landing on an enemy
    const float CoyoteTime = 0.1f;      // seconds after leaving the ground that a jump is still allowed
    const float JumpBuffer = 0.12f;     // seconds a jump press is remembered before landing
    const float MaxFallSpeed = 22f;     // terminal velocity so falls stay readable

    Rigidbody2D rb;
    BoxCollider2D col;
    SpriteRenderer sr;
    SpriteRenderer shadowSr;
    Transform visual;
    Transform shadow;

    // Squash and stretch amount: positive = stretched tall, negative = squashed flat. Decays back to zero.
    float squash;

    float moveInput;
    float jumpBufferTimer;
    float coyoteTimer;
    float invulnTimer;      // blinks and ignores hits for a moment after respawning
    float animTimer;
    float stepTimer;
    float lastAirVelocity;  // most negative vertical speed during the current fall (decides if a landing is "hard")
    bool jumpHeld;
    bool grounded;
    bool wasGrounded;
    bool jumping;
    bool frozen;            // input and hits are ignored (title screen, help dialog, stage clear)

#if UNITY_EDITOR
    // Test-bot hooks. Compiled only in the editor so they can never ship in a build.
    // AutoPlayer writes these fields instead of the keyboard being read; see AutoPlayer.cs.
    public bool bot;
    public float botMove;
    public bool botJumpHeld;
    public bool botJumpPress;
#endif

    /// <summary>The player's solid collider (enemies use it to ignore physical collisions with the player).</summary>
    public Collider2D Collider => col;

    /// <summary>True while standing on something solid.</summary>
    public bool IsGrounded => grounded;

    /// <summary>+1 when facing right, -1 when facing left. The camera uses it to look ahead.</summary>
    public int Facing { get; private set; } = 1;

    /// <summary>Current horizontal speed (absolute value) in tiles per second.</summary>
    public float Speed => Mathf.Abs(rb.linearVelocity.x);

    void Awake()
    {
        gameObject.layer = PlayerLayer;

        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 3f;
        // Continuous detection stops the player tunnelling through thin colliders when falling fast.
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // Interpolate smooths the rendered position between physics steps (physics runs at 50 Hz).
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // The collider is slightly narrower than a tile (0.62) and a hair shorter (0.98) so the hero fits
        // through one-tile gaps without snagging. The offset places the collider's bottom at the feet.
        col = GetComponent<BoxCollider2D>();
        col.size = new Vector2(0.62f, 0.98f);
        col.offset = new Vector2(0f, 0.49f);
        // Zero friction so the hero never sticks to walls while pressing against them in the air.
        col.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };

        // The sprite lives on a child so it can be squashed and stretched without scaling the collider.
        var visualObject = new GameObject("Visual");
        visual = visualObject.transform;
        visual.SetParent(transform, false);
        sr = visualObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.HeroIdle;
        sr.sortingOrder = 3; // in front of enemies (2), coins (1) and scenery (-1)

        // Blob shadow on the ground under the hero; positioned every frame in UpdateShadowAndSquash.
        var shadowObject = new GameObject("Shadow");
        shadow = shadowObject.transform;
        shadowSr = shadowObject.AddComponent<SpriteRenderer>();
        shadowSr.sprite = PixelArt.Shadow;
        shadowSr.sortingOrder = 2;
    }

    // Eases the squash/stretch back to normal, applies it to the sprite, and drops the shadow onto the ground below.
    void UpdateShadowAndSquash()
    {
        squash = Mathf.Lerp(squash, 0f, 1f - Mathf.Exp(-16f * Time.deltaTime));
        // Stretching tall also makes the sprite a bit thinner (and the reverse), which keeps its volume looking constant.
        visual.localScale = new Vector3(1f - 0.35f * squash, 1f + 0.5f * squash, 1f);

        // Cast a ray straight down to find the ground; the shadow sits on it and shrinks the higher the hero is.
        Vector2 feet = new Vector2(transform.position.x, transform.position.y + 0.1f);
        var hit = Physics2D.Raycast(feet, Vector2.down, 9f, GroundMask);
        shadowSr.enabled = hit.collider != null && sr.enabled; // hidden over pits
        if (hit.collider != null)
        {
            float t = Mathf.Clamp01(hit.distance / 6f);
            shadow.position = new Vector3(transform.position.x, hit.point.y + 0.03f, 0f);
            shadow.localScale = Vector3.one * Mathf.Lerp(0.9f, 0.45f, t);
        }
    }

    void Update()
    {
        ReadInput();
        jumpBufferTimer -= Time.deltaTime;
        UpdateAnimation();
        UpdateShadowAndSquash();
        UpdateFootsteps();

        // Falling below the stage (into a pit) costs a life.
        if (transform.position.y < Game.KillY)
            Game.Instance.PlayerDied(this);
    }

    // Gathers input from keyboard, gamepad and touch into moveInput / jumpHeld / the jump buffer.
    // Reading happens in Update (every rendered frame) so no key press is missed;
    // the values are consumed later in FixedUpdate.
    void ReadInput()
    {
        if (frozen)
        {
            moveInput = 0f;
            jumpHeld = false;
            return;
        }

#if UNITY_EDITOR
        // The test bot replaces real input.
        if (bot)
        {
            moveInput = botMove;
            jumpHeld = botJumpHeld;
            if (botJumpPress)
            {
                jumpBufferTimer = JumpBuffer;
                botJumpPress = false;
            }
            return;
        }
#endif

        var kb = Keyboard.current;
        var gp = Gamepad.current;
        float x = 0f;
        bool pressed = false; // jump was pressed this frame
        bool held = false;    // jump is currently held

        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            pressed |= kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
            held |= kb.spaceKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed;
        }
        if (gp != null)
        {
            x += gp.leftStick.x.ReadValue() + gp.dpad.x.ReadValue();
            pressed |= gp.buttonSouth.wasPressedThisFrame;
            held |= gp.buttonSouth.isPressed;
        }

        // Touch zones are relative to the visible playfield, which Game computes every frame.
        TouchControls.Read(Game.Instance.ViewRect, out float tx, out bool tPressed, out bool tHeld);
        x += tx;
        pressed |= tPressed;
        held |= tHeld;

        moveInput = Mathf.Clamp(x, -1f, 1f);
        jumpHeld = held;
        if (pressed) jumpBufferTimer = JumpBuffer;
    }

    // All physics-affecting logic: ground check, horizontal movement, jumping and gravity.
    void FixedUpdate()
    {
        grounded = CheckGround();
        coyoteTimer = grounded ? CoyoteTime : coyoteTimer - Time.fixedDeltaTime;

        // Landing detection: remember the fastest downward speed while airborne, and when we touch ground
        // after a real fall (faster than 6 tiles/s) play the landing sound, squash and dust.
        if (!grounded)
        {
            lastAirVelocity = Mathf.Min(lastAirVelocity, rb.linearVelocity.y);
        }
        else if (!wasGrounded)
        {
            if (lastAirVelocity < -6f)
            {
                Sfx.Play(Clip.Land);
                squash = -0.45f;
                Fx.Dust(transform.position);
            }
            lastAirVelocity = 0f;
        }
        wasGrounded = grounded;

        // Horizontal movement: accelerate toward the target speed, or brake when there is no input.
        Vector2 v = rb.linearVelocity;
        float target = moveInput * MoveSpeed;
        float rate = Mathf.Abs(target) > 0.01f ? Accel : Decel;
        v.x = Mathf.MoveTowards(v.x, target, rate * Time.fixedDeltaTime);

        // Jump when a press is buffered AND we are (or just were) on the ground.
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            v.y = JumpVelocity;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f; // consumed, so no double jump from coyote time
            jumping = true;
            squash = 0.55f;
            Fx.Dust(transform.position, 4);
            Sfx.Play(Clip.Jump);
        }

        // Variable jump height: if the button is released while still rising, cap the upward speed at 40%.
        if (jumping && !jumpHeld && v.y > JumpVelocity * 0.4f)
            v.y = JumpVelocity * 0.4f;
        if (v.y <= 0f)
            jumping = false;

        // Heavier gravity while falling, plus a terminal velocity.
        rb.gravityScale = v.y < -0.1f ? 4.5f : 3f;
        v.y = Mathf.Max(v.y, -MaxFallSpeed);
        rb.linearVelocity = v;
    }

    // Ground check: a thin box just under the feet, slightly narrower than the body so the hero is not
    // "grounded" when only brushing a wall. GroundMask excludes the player and enemies, and triggers are
    // ignored globally (Game sets Physics2D.queriesHitTriggers = false), so only solid level geometry counts.
    bool CheckGround()
    {
        Bounds b = col.bounds;
        var center = new Vector2(b.center.x, b.min.y - 0.04f);
        var size = new Vector2(b.size.x * 0.9f, 0.1f);
        return Physics2D.OverlapBox(center, size, 0f, GroundMask) != null;
    }

    // Plays a soft step sound about every 0.27 s while running on the ground, with slight pitch variation.
    void UpdateFootsteps()
    {
        if (frozen || !grounded || Speed < 1f)
        {
            stepTimer = 0.05f; // so the first step sounds almost immediately after starting to run
            return;
        }
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            Sfx.Play(Clip.Step, 0.7f, Random.Range(0.9f, 1.2f));
            stepTimer = 0.27f;
        }
    }

    // Chooses the sprite frame (jump / walk cycle / idle), flips it to face the movement direction,
    // and blinks the sprite while invulnerable.
    void UpdateAnimation()
    {
        if (moveInput > 0.01f) { sr.flipX = false; Facing = 1; }
        else if (moveInput < -0.01f) { sr.flipX = true; Facing = -1; }

        if (!grounded)
        {
            sr.sprite = PixelArt.HeroJump;
        }
        else if (Speed > 0.5f)
        {
            // Two walk frames alternating every 0.1 s.
            animTimer += Time.deltaTime;
            sr.sprite = (Mathf.FloorToInt(animTimer / 0.1f) % 2 == 0) ? PixelArt.HeroWalkA : PixelArt.HeroWalkB;
        }
        else
        {
            animTimer = 0f;
            sr.sprite = PixelArt.HeroIdle;
        }

        // Flicker between 30% and 100% opacity 10 times per second while invulnerable.
        if (invulnTimer > 0f)
        {
            invulnTimer -= Time.deltaTime;
            bool faded = Mathf.FloorToInt(Time.time * 20f) % 2 == 0;
            sr.color = new Color(1f, 1f, 1f, faded ? 0.3f : 1f);
        }
        else
        {
            sr.color = Color.white;
        }
    }

    /// <summary>
    /// Called by an <see cref="Enemy"/> every physics step while the player overlaps its hit box.
    /// Decides between a stomp (kill the enemy and bounce) and getting hurt.
    /// It is a stomp if the player's feet are above the enemy's centre and the player is not moving up fast;
    /// this is lenient on purpose so landing on an enemy from the side while falling still counts.
    /// </summary>
    public void StompOrHurt(Enemy enemy)
    {
        if (frozen || enemy.IsDead) return;

        bool above = col.bounds.min.y > enemy.Center.y;
        if (above && rb.linearVelocity.y <= 2f)
        {
            Fx.StompPuff(enemy.Center);
            enemy.Squash();
            Sfx.Play(Clip.Stomp);
            squash = 0.4f;
            // Holding jump while stomping gives a higher bounce, like in classic platformers.
            Vector2 v = rb.linearVelocity;
            v.y = jumpHeld ? StompBounce + 3f : StompBounce;
            rb.linearVelocity = v;
            jumping = false;
        }
        else
        {
            Hurt();
        }
    }

    /// <summary>
    /// Takes a hit (spikes or an enemy's side). Ignored while frozen or invulnerable;
    /// otherwise <see cref="Game.PlayerDied"/> removes a life and respawns or ends the stage attempt.
    /// </summary>
    public void Hurt()
    {
        if (frozen || invulnTimer > 0f) return;
        Game.Instance.PlayerDied(this);
    }

    /// <summary>Moves the hero back to the stage start and gives 2 seconds of blinking invulnerability.</summary>
    public void Respawn(Vector3 position)
    {
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
        invulnTimer = 2f;
        jumpBufferTimer = 0f;
        lastAirVelocity = 0f;
    }

    /// <summary>Ignore input and hits (used on the title screen, in dialogs and at stage clear).</summary>
    public void Freeze() => frozen = true;

    /// <summary>Resume normal control.</summary>
    public void Unfreeze() => frozen = false;

    /// <summary>Turns the hero off completely (game over): no physics, no sprite, no shadow.</summary>
    public void Disable()
    {
        frozen = true;
        rb.simulated = false;
        sr.enabled = false;
        shadowSr.enabled = false;
    }
}

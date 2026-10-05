using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    public const int PlayerLayer = 8;
    public const int EnemyLayer = 9;
    public static readonly int GroundMask = ~((1 << PlayerLayer) | (1 << EnemyLayer));
    public const float MoveSpeed = 5.5f;

    const float Accel = 60f;
    const float Decel = 80f;
    const float JumpVelocity = 15f;
    const float StompBounce = 12f;
    const float CoyoteTime = 0.1f;
    const float JumpBuffer = 0.12f;
    const float MaxFallSpeed = 22f;

    Rigidbody2D rb;
    BoxCollider2D col;
    SpriteRenderer sr;
    SpriteRenderer shadowSr;
    Transform visual;
    Transform shadow;
    float squash;

    float moveInput;
    float jumpBufferTimer;
    float coyoteTimer;
    float invulnTimer;
    float animTimer;
    float stepTimer;
    float lastAirVelocity;
    bool jumpHeld;
    bool grounded;
    bool wasGrounded;
    bool jumping;
    bool frozen;

#if UNITY_EDITOR
    public bool bot;
    public float botMove;
    public bool botJumpHeld;
    public bool botJumpPress;
#endif

    public Collider2D Collider => col;
    public bool IsGrounded => grounded;
    public int Facing { get; private set; } = 1;
    public float Speed => Mathf.Abs(rb.linearVelocity.x);

    void Awake()
    {
        gameObject.layer = PlayerLayer;

        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        col = GetComponent<BoxCollider2D>();
        col.size = new Vector2(0.62f, 0.98f);
        col.offset = new Vector2(0f, 0.49f);
        col.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };

        var visualObject = new GameObject("Visual");
        visual = visualObject.transform;
        visual.SetParent(transform, false);
        sr = visualObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.HeroIdle;
        sr.sortingOrder = 3;

        var shadowObject = new GameObject("Shadow");
        shadow = shadowObject.transform;
        shadowSr = shadowObject.AddComponent<SpriteRenderer>();
        shadowSr.sprite = PixelArt.Shadow;
        shadowSr.sortingOrder = 2;
    }

    void UpdateShadowAndSquash()
    {
        squash = Mathf.Lerp(squash, 0f, 1f - Mathf.Exp(-16f * Time.deltaTime));
        visual.localScale = new Vector3(1f - 0.35f * squash, 1f + 0.5f * squash, 1f);

        Vector2 feet = new Vector2(transform.position.x, transform.position.y + 0.1f);
        var hit = Physics2D.Raycast(feet, Vector2.down, 9f, GroundMask);
        shadowSr.enabled = hit.collider != null && sr.enabled;
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

        if (transform.position.y < Game.KillY)
            Game.Instance.PlayerDied(this);
    }

    void ReadInput()
    {
        if (frozen)
        {
            moveInput = 0f;
            jumpHeld = false;
            return;
        }

#if UNITY_EDITOR
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
        bool pressed = false;
        bool held = false;

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

        TouchControls.Read(Game.Instance.ViewRect, out float tx, out bool tPressed, out bool tHeld);
        x += tx;
        pressed |= tPressed;
        held |= tHeld;

        moveInput = Mathf.Clamp(x, -1f, 1f);
        jumpHeld = held;
        if (pressed) jumpBufferTimer = JumpBuffer;
    }

    void FixedUpdate()
    {
        grounded = CheckGround();
        coyoteTimer = grounded ? CoyoteTime : coyoteTimer - Time.fixedDeltaTime;

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

        Vector2 v = rb.linearVelocity;
        float target = moveInput * MoveSpeed;
        float rate = Mathf.Abs(target) > 0.01f ? Accel : Decel;
        v.x = Mathf.MoveTowards(v.x, target, rate * Time.fixedDeltaTime);

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            v.y = JumpVelocity;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            jumping = true;
            squash = 0.55f;
            Fx.Dust(transform.position, 4);
            Sfx.Play(Clip.Jump);
        }

        if (jumping && !jumpHeld && v.y > JumpVelocity * 0.4f)
            v.y = JumpVelocity * 0.4f;
        if (v.y <= 0f)
            jumping = false;

        rb.gravityScale = v.y < -0.1f ? 4.5f : 3f;
        v.y = Mathf.Max(v.y, -MaxFallSpeed);
        rb.linearVelocity = v;
    }

    bool CheckGround()
    {
        Bounds b = col.bounds;
        var center = new Vector2(b.center.x, b.min.y - 0.04f);
        var size = new Vector2(b.size.x * 0.9f, 0.1f);
        return Physics2D.OverlapBox(center, size, 0f, GroundMask) != null;
    }

    void UpdateFootsteps()
    {
        if (frozen || !grounded || Speed < 1f)
        {
            stepTimer = 0.05f;
            return;
        }
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            Sfx.Play(Clip.Step, 0.7f, Random.Range(0.9f, 1.2f));
            stepTimer = 0.27f;
        }
    }

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
            animTimer += Time.deltaTime;
            sr.sprite = (Mathf.FloorToInt(animTimer / 0.1f) % 2 == 0) ? PixelArt.HeroWalkA : PixelArt.HeroWalkB;
        }
        else
        {
            animTimer = 0f;
            sr.sprite = PixelArt.HeroIdle;
        }

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

    public void Hurt()
    {
        if (frozen || invulnTimer > 0f) return;
        Game.Instance.PlayerDied(this);
    }

    public void Respawn(Vector3 position)
    {
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
        invulnTimer = 2f;
        jumpBufferTimer = 0f;
        lastAirVelocity = 0f;
    }

    public void Freeze() => frozen = true;
    public void Unfreeze() => frozen = false;

    public void Disable()
    {
        frozen = true;
        rb.simulated = false;
        sr.enabled = false;
        shadowSr.enabled = false;
    }
}

using UnityEngine;

/// <summary>
/// A walking enemy (the purple blob). It patrols back and forth on its platform, turns around at walls and at
/// ledges (so it never walks into a pit), and can be stomped from above.
///
/// Why the enemy has two colliders on one object:
///  - body (solid): lets it stand on the ground and bump into walls and other enemies.
///  - hitbox (trigger, slightly larger): detects the player overlapping it, and tells the player to
///    decide between a stomp and a hurt (<see cref="PlayerController.StompOrHurt"/>).
/// The player and the enemy bodies deliberately do NOT collide physically (see <see cref="Start"/>), so the player is
/// never pushed around or bounced off by an enemy; every interaction is decided by the hitbox and the stomp rule.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Enemy : MonoBehaviour
{
    /// <summary>Walking speed in tiles per second. Game sets it per stage (faster in later stages).</summary>
    public float speed = 2f;

    // Rays that look for walls and ground should see every solid thing, including other enemies (so they
    // turn around when they meet), but must ignore the player.
    static readonly int TurnMask = ~(1 << PlayerController.PlayerLayer);

    Rigidbody2D rb;
    BoxCollider2D body;
    BoxCollider2D hitbox;
    SpriteRenderer sr;
    SpriteRenderer shadowSr;
    Transform visual;
    Transform shadow;
    int dir = -1;          // -1 = walking left, +1 = right. Enemies start by walking toward the left.
    float flipCooldown;    // after turning, wait a moment before checking again (prevents jitter at corners)
    float animTimer;
    bool dead;

    /// <summary>True once stomped (the squash animation plays, then the object is destroyed).</summary>
    public bool IsDead => dead;

    /// <summary>Centre of the enemy's body in world space; the player compares its feet to this to detect a stomp.</summary>
    public Vector2 Center => body.bounds.center;

    void Awake()
    {
        gameObject.layer = PlayerController.EnemyLayer;

        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Solid body: 0.8 x 0.8 tiles with its bottom at the object's origin (the enemy's feet).
        body = GetComponent<BoxCollider2D>();
        body.size = new Vector2(0.8f, 0.8f);
        body.offset = new Vector2(0f, 0.4f);
        body.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };

        // Trigger hit box: a bit bigger than the body so contact is detected a moment before the sprites overlap.
        hitbox = gameObject.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size = new Vector2(0.9f, 0.9f);
        hitbox.offset = new Vector2(0f, 0.45f);

        // Sprite on a child so the stomp animation can flatten it without touching the colliders.
        var visualObject = new GameObject("Visual");
        visual = visualObject.transform;
        visual.SetParent(transform, false);
        sr = visualObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.EnemyA;
        sr.sortingOrder = 2;

        var shadowObject = new GameObject("Shadow");
        shadow = shadowObject.transform;
        shadow.SetParent(transform, false);
        shadow.localPosition = new Vector3(0f, 0.03f, 0f);
        shadow.localScale = Vector3.one * 0.85f;
        shadowSr = shadowObject.AddComponent<SpriteRenderer>();
        shadowSr.sprite = PixelArt.Shadow;
        shadowSr.sortingOrder = 1;
    }

    // Start (not Awake) because the player must already exist: Game builds the player before the first frame,
    // and Start runs on the first frame. Ignoring the player-vs-body collision pair means they never push each other,
    // while the trigger hitbox (a separate collider) still detects the player.
    void Start()
    {
        var player = Game.Instance != null ? Game.Instance.Player : null;
        if (player != null) Physics2D.IgnoreCollision(body, player.Collider);
    }

    // Alternate the two walking frames every 0.2 s.
    void Update()
    {
        if (dead) return;
        animTimer += Time.deltaTime;
        sr.sprite = (Mathf.FloorToInt(animTimer / 0.2f) % 2 == 0) ? PixelArt.EnemyA : PixelArt.EnemyB;
    }

    // Patrol logic. Two short rays are cast from the leading edge: one forward (wall?) and one down (ground?).
    // If there is a wall ahead or no ground ahead, the enemy turns around.
    void FixedUpdate()
    {
        if (dead) return;

        flipCooldown -= Time.fixedDeltaTime;
        if (flipCooldown <= 0f)
        {
            Bounds b = body.bounds;
            float edgeX = dir > 0 ? b.max.x : b.min.x; // the edge in the walking direction

            // Wall ray starts just outside the body so it does not hit the enemy itself.
            bool wall = Physics2D.Raycast(new Vector2(edgeX + dir * 0.02f, b.center.y), Vector2.right * dir, 0.15f, TurnMask).collider != null;

            // Ground ray starts a little ahead of the edge, near the feet, and points down.
            bool ground = Physics2D.Raycast(new Vector2(edgeX + dir * 0.1f, b.min.y + 0.1f), Vector2.down, 0.5f, TurnMask).collider != null;
            if (wall || !ground)
            {
                dir = -dir;
                flipCooldown = 0.3f;
            }
        }

        // Keep the vertical speed (gravity and falling is handled by physics) and set the horizontal speed directly.
        rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
    }

    // Fires every physics step while something overlaps the hit box. Only the player matters here.
    void OnTriggerStay2D(Collider2D other)
    {
        if (dead) return;
        if (other.TryGetComponent(out PlayerController player))
            player.StompOrHurt(this);
    }

    /// <summary>
    /// Kills the enemy after a stomp: turns off physics and colliders, flattens the sprite for a moment,
    /// and destroys the object after half a second.
    /// </summary>
    public void Squash()
    {
        if (dead) return;
        dead = true;
        body.enabled = false;
        hitbox.enabled = false;
        rb.simulated = false;
        visual.localScale = new Vector3(1.2f, 0.3f, 1f);
        shadowSr.enabled = false;
        Destroy(gameObject, 0.5f);
    }
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Enemy : MonoBehaviour
{
    public float speed = 2f;
    static readonly int TurnMask = ~(1 << PlayerController.PlayerLayer);

    Rigidbody2D rb;
    BoxCollider2D body;
    BoxCollider2D hitbox;
    SpriteRenderer sr;
    SpriteRenderer shadowSr;
    Transform visual;
    Transform shadow;
    int dir = -1;
    float flipCooldown;
    float animTimer;
    bool dead;

    public bool IsDead => dead;
    public Vector2 Center => body.bounds.center;

    void Awake()
    {
        gameObject.layer = PlayerController.EnemyLayer;

        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        body = GetComponent<BoxCollider2D>();
        body.size = new Vector2(0.8f, 0.8f);
        body.offset = new Vector2(0f, 0.4f);
        body.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };

        hitbox = gameObject.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size = new Vector2(0.9f, 0.9f);
        hitbox.offset = new Vector2(0f, 0.45f);

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

    void Start()
    {
        var player = Game.Instance != null ? Game.Instance.Player : null;
        if (player != null) Physics2D.IgnoreCollision(body, player.Collider);
    }

    void Update()
    {
        if (dead) return;
        animTimer += Time.deltaTime;
        sr.sprite = (Mathf.FloorToInt(animTimer / 0.2f) % 2 == 0) ? PixelArt.EnemyA : PixelArt.EnemyB;
    }

    void FixedUpdate()
    {
        if (dead) return;

        flipCooldown -= Time.fixedDeltaTime;
        if (flipCooldown <= 0f)
        {
            Bounds b = body.bounds;
            float edgeX = dir > 0 ? b.max.x : b.min.x;
            bool wall = Physics2D.Raycast(new Vector2(edgeX + dir * 0.02f, b.center.y), Vector2.right * dir, 0.15f, TurnMask).collider != null;
            bool ground = Physics2D.Raycast(new Vector2(edgeX + dir * 0.1f, b.min.y + 0.1f), Vector2.down, 0.5f, TurnMask).collider != null;
            if (wall || !ground)
            {
                dir = -dir;
                flipCooldown = 0.3f;
            }
        }

        rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (dead) return;
        if (other.TryGetComponent(out PlayerController player))
            player.StompOrHurt(this);
    }

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

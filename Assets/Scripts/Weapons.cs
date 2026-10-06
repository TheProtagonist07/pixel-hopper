using UnityEngine;

// The blaster: the pickup that arms the player and the shots it fires. Nothing here is placed by hand;
// Game.BuildLevel creates a GunPickup for every 'g' tile and PlayerController spawns a Bullet for every shot.

/// <summary>
/// A floating blaster pickup. Touching it gives the player a full magazine (see <see cref="PlayerController.GiveGun"/>).
/// It bobs and pulses so it reads as a power-up. The pickup is consumed, so each one can be used once per attempt.
/// </summary>
public class GunPickup : MonoBehaviour
{
    // Shots granted by one pickup.
    const int Shots = 12;

    Vector3 basePosition;
    float phase;
    SpriteRenderer sr;

    void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.GunPickup;
        sr.sortingOrder = 1; // behind enemies (2) and the player (3), like coins

        // A trigger: the player walks through it and OnTriggerEnter2D fires, nothing blocks movement.
        var c = gameObject.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 0.55f;

        phase = Random.value * Mathf.PI * 2f;
    }

    // The position is assigned by Game after AddComponent but before the first frame, so read it in Start.
    void Start() => basePosition = transform.position + Vector3.up * 0.3f;

    void Update()
    {
        // Bob 0.12 units and pulse the size by 6% so the aura seems to breathe.
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 3f + phase) * 0.12f);
        float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 6f + phase);
        transform.localScale = new Vector3(pulse, pulse, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController player)) return;
        player.GiveGun(Shots);
        Sfx.Play(Clip.PowerUp);
        Fx.Burst(transform.position, new Color(0.4f, 0.95f, 1f, 1f), 14, 4f, 0.5f, 2f, 0.14f);
        Destroy(gameObject);
    }
}

/// <summary>
/// One blaster shot. It flies straight at a fixed speed with no gravity, hurts the first enemy it touches
/// (see <see cref="Enemy.Shot"/>), and disappears when it hits solid ground or runs out of time.
///
/// Physics: a kinematic Rigidbody2D (moved by velocity, not pushed by anything) with a small trigger collider.
/// A trigger needs a Rigidbody2D on at least one of the two objects to produce events; enemies and the world
/// are other colliders, so OnTriggerEnter2D fires when the shot overlaps them.
/// </summary>
public class Bullet : MonoBehaviour
{
    const float Speed = 14f;       // tiles per second; a stage view is only 8.4 tiles wide, so shots cross the screen in about 0.6 s
    const float Lifetime = 1.0f;   // seconds before the shot fades out by itself (about 14 tiles of range)

    float age;
    int direction = 1;

    /// <summary>Creates a shot at a position, flying left (-1) or right (+1).</summary>
    public static void Spawn(Vector3 position, int direction)
    {
        var go = new GameObject("Bullet");
        go.transform.position = position;
        var b = go.AddComponent<Bullet>();
        b.direction = direction;
        go.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(direction * Speed, 0f);
        go.GetComponent<SpriteRenderer>().flipX = direction < 0;
    }

    void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.Bullet;
        sr.sortingOrder = 4; // in front of the player (3)

        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var c = gameObject.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 0.16f;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // The hero's own collider is overlapping the muzzle when the shot is created, so ignore it.
        if (other.TryGetComponent(out PlayerController _)) return;

        if (other.TryGetComponent(out Enemy enemy))
        {
            // Both of an enemy's colliders (body and hit box) report here; a dead enemy is ignored.
            if (enemy.IsDead) return;
            enemy.Shot();
            Impact(new Color(0.4f, 0.95f, 1f, 1f));
            return;
        }

        // Triggers are coins, spikes, pickups, the exit flag and other shots: shots pass through them.
        if (other.isTrigger) return;

        // Anything else that is solid is the level (ground and bricks): the shot bursts against it.
        Impact(new Color(1f, 0.95f, 0.7f, 1f));
    }

    void Impact(Color color)
    {
        Fx.Burst(transform.position, color, 6, 3f, 0.25f, 0f, 0.1f);
        Destroy(gameObject);
    }
}

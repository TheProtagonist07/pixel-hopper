using UnityEngine;

// Small world objects the player interacts with by touching them: coins, spike hazards and the level-exit flag.
// None of them is placed in the scene by hand. Game.BuildLevel creates an empty GameObject at the right spot and
// adds the component; each component builds its own sprite and collider in Awake. This keeps the stage layout
// fully data-driven (see Levels.cs) and means there are no prefabs to maintain.

/// <summary>
/// A collectible coin. It bobs gently, spins through four pre-drawn frames (a cheap fake 3D rotation),
/// and is collected when the player touches it.
/// </summary>
public class Coin : MonoBehaviour
{
    // Resting position; the visible position oscillates around it.
    Vector3 basePosition;

    // Random offset so neighbouring coins do not bob and spin in perfect sync.
    float phase;
    SpriteRenderer sr;

    void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.CoinFrame(0);
        sr.sortingOrder = 1; // behind enemies (2) and the player (3)

        // A trigger collider: the player passes through it and OnTriggerEnter2D fires, nothing blocks movement.
        var c = gameObject.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 0.4f;

        phase = Random.value * Mathf.PI * 2f;
    }

    // The position is assigned by Game after AddComponent but before the first frame, so read it in Start, not Awake.
    void Start() => basePosition = transform.position;

    void Update()
    {
        // Bob 0.08 units up and down, and advance the spin animation at about 7 frames per second.
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 4f + phase) * 0.08f);
        sr.sprite = PixelArt.CoinFrame(Mathf.FloorToInt(Time.time * 7f + phase));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore enemies and other objects; only the player collects coins.
        if (!other.TryGetComponent(out PlayerController _)) return;
        Fx.CoinSparkle(transform.position);
        Game.Instance.AddCoin();
        Destroy(gameObject);
    }
}

/// <summary>
/// A row of spikes on the ground. Touching them hurts the player (costs a life).
/// </summary>
public class Hazard : MonoBehaviour
{
    void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.Spikes;
        sr.sortingOrder = 1;

        // The hurt box is deliberately smaller than the sprite (0.7 x 0.4 of a 1 x 1 tile, shifted down)
        // so grazing the tip of a spike or clipping its edge is forgiving, not instantly fatal.
        var c = gameObject.AddComponent<BoxCollider2D>();
        c.isTrigger = true;
        c.size = new Vector2(0.7f, 0.4f);
        c.offset = new Vector2(0f, -0.25f);
    }

    // Stay, not Enter: if the player is invulnerable (just respawned) while overlapping, they should still
    // be hurt again as soon as the invulnerability ends, instead of standing safely inside the spikes.
    void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController player))
            player.Hurt();
    }
}

/// <summary>
/// The goal flag at the end of each stage. Touching it completes the stage.
/// </summary>
public class LevelExit : MonoBehaviour
{
    void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.Flag;
        sr.sortingOrder = 1;

        // The flag sprite is 3 tiles tall (pivot at its base), but the trigger is a tall 1 x 8 line from the ground up.
        // A full jump reaches about 3.8 tiles, so a trigger as short as the pole could be jumped clean over
        // without finishing the stage (found after release 1.0.2). Like the goal line in classic platformers,
        // crossing the flag's column at any height now counts.
        var c = gameObject.AddComponent<BoxCollider2D>();
        c.isTrigger = true;
        c.size = new Vector2(1f, 8f);
        c.offset = new Vector2(0f, 4f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController _))
            Game.Instance.LevelComplete();
    }
}

/// <summary>
/// A bonus heart placed inside a stage. Touching it gives the player one extra life, up to the maximum of 5.
/// If the player is already at the maximum the heart stays where it is (it is not wasted). It bobs and pulses like a
/// coin so it reads as a collectible, and its sprite is the same heart used in the HUD.
/// </summary>
public class HeartPickup : MonoBehaviour
{
    Vector3 basePosition;
    float phase;

    void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.Heart;
        sr.sortingOrder = 1; // behind enemies (2) and the player (3), like coins

        var c = gameObject.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 0.45f;

        phase = Random.value * Mathf.PI * 2f;
    }

    // The position is assigned by Game after AddComponent but before the first frame, so read it in Start.
    void Start() => basePosition = transform.position;

    void Update()
    {
        // Bob 0.1 units and pulse the size by 10% (about twice a second) so it looks like a beating heart.
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 3f + phase) * 0.1f);
        float pulse = 1.15f + 0.1f * Mathf.Sin(Time.time * 7f + phase);
        transform.localScale = new Vector3(pulse, pulse, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController _)) return;
        if (!Game.Instance.AddLife()) return; // already at the maximum: leave the heart for later
        Fx.Burst(transform.position, new Color(1f, 0.4f, 0.5f, 1f), 12, 4f, 0.5f, 2f, 0.14f);
        Fx.Burst(transform.position, Color.white, 4, 2.5f, 0.3f, 0f, 0.1f);
        Destroy(gameObject);
    }
}

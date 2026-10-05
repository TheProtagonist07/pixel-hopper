using UnityEngine;

/// <summary>
/// One tiny visual-effect particle (dust puff, coin sparkle, stomp burst, ...).
/// Particles are plain GameObjects with a SpriteRenderer rather than Unity's ParticleSystem on purpose:
/// they need no extra material or shader (which keeps the WebGL build small and avoids Shader.Find),
/// and a few dozen short-lived sprites cost almost nothing.
/// Created in bulk by <see cref="Fx.Burst"/>; each one moves, shrinks, fades and then destroys itself.
/// </summary>
public class FxParticle : MonoBehaviour
{
    /// <summary>Current velocity in world units per second.</summary>
    public Vector2 velocity;

    /// <summary>Downward acceleration. Negative values make the particle float upward.</summary>
    public float gravity;

    /// <summary>Total lifetime in seconds. The particle is destroyed when its age reaches this.</summary>
    public float life;

    /// <summary>Starting size in world units (a tile is 1 unit).</summary>
    public float size;

    SpriteRenderer sr;
    Color color;
    float age;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>Sets the base color; alpha is faded out over the particle's life.</summary>
    public void Begin(Color c)
    {
        color = c;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        // Simple ballistic motion.
        velocity.y -= gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);

        // t runs 0..1 over the lifetime. Shrink to 40% of the start size and fade out with a squared curve
        // so the particle stays visible for most of its life and vanishes quickly at the end.
        // The Dot sprite is 2x2 pixels at 32 pixels per unit = 0.0625 units, hence the division to reach 'size'.
        float t = age / life;
        float s = size * (1f - t * 0.6f) / 0.0625f;
        transform.localScale = new Vector3(s, s, 1f);
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - t * t));
    }
}

/// <summary>
/// Static helpers that spawn groups of <see cref="FxParticle"/>s for the game's common effects.
/// Call these from gameplay code (player jump/land, coin pickup, stomp, death, stage clear).
/// </summary>
public static class Fx
{
    /// <summary>
    /// Spawns <paramref name="count"/> particles at <paramref name="position"/> flying in random directions.
    /// </summary>
    /// <param name="position">World position of the burst centre.</param>
    /// <param name="color">Particle color (alpha is the starting opacity).</param>
    /// <param name="count">How many particles to spawn.</param>
    /// <param name="speed">Maximum launch speed; each particle gets 40% to 100% of it.</param>
    /// <param name="life">Average lifetime in seconds; each particle varies by about plus or minus 30%.</param>
    /// <param name="gravity">Downward pull. Use a negative value to make particles drift up.</param>
    /// <param name="size">Average particle size in world units.</param>
    /// <param name="upBias">Extra upward velocity added to every particle so the burst favours going up.</param>
    public static void Burst(Vector3 position, Color color, int count, float speed, float life, float gravity, float size, float upBias = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("fx");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Dot;
            sr.sortingOrder = 6; // above the player (3) and enemies (2)
            sr.color = color;

            var p = go.AddComponent<FxParticle>();

            // Random direction and speed, with a little variation in life and size so bursts look organic.
            float angle = Random.value * Mathf.PI * 2f;
            float s = speed * (0.4f + Random.value * 0.6f);
            p.velocity = new Vector2(Mathf.Cos(angle) * s, Mathf.Sin(angle) * s + upBias);
            p.gravity = gravity;
            p.life = life * (0.7f + Random.value * 0.5f);
            p.size = size * (0.7f + Random.value * 0.6f);
            p.Begin(color);
        }
    }

    /// <summary>Soft cream-colored puff for jumping and landing. Floats slightly upward.</summary>
    public static void Dust(Vector3 position, int count = 5)
    {
        Burst(position, new Color(1f, 0.97f, 0.88f, 0.85f), count, 1.6f, 0.35f, -1.5f, 0.16f, 0.4f);
    }

    /// <summary>Gold sparkles plus a few white glints when a coin is collected.</summary>
    public static void CoinSparkle(Vector3 position)
    {
        Burst(position, new Color(1f, 0.9f, 0.35f, 1f), 8, 3.2f, 0.45f, 3f, 0.12f);
        Burst(position, Color.white, 3, 2f, 0.3f, 0f, 0.1f);
    }

    /// <summary>Purple burst (matching the enemy's color) plus white flecks when an enemy is stomped.</summary>
    public static void StompPuff(Vector3 position)
    {
        Burst(position, new Color(0.78f, 0.45f, 0.92f, 1f), 10, 4.2f, 0.5f, 8f, 0.18f, 1.5f);
        Burst(position, Color.white, 4, 3f, 0.3f, 2f, 0.12f, 1f);
    }
}

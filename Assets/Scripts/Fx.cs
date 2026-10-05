using UnityEngine;

public class FxParticle : MonoBehaviour
{
    public Vector2 velocity;
    public float gravity;
    public float life;
    public float size;
    SpriteRenderer sr;
    Color color;
    float age;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

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
        velocity.y -= gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        float t = age / life;
        float s = size * (1f - t * 0.6f) / 0.0625f;
        transform.localScale = new Vector3(s, s, 1f);
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - t * t));
    }
}

public static class Fx
{
    public static void Burst(Vector3 position, Color color, int count, float speed, float life, float gravity, float size, float upBias = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("fx");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Dot;
            sr.sortingOrder = 6;
            sr.color = color;

            var p = go.AddComponent<FxParticle>();
            float angle = Random.value * Mathf.PI * 2f;
            float s = speed * (0.4f + Random.value * 0.6f);
            p.velocity = new Vector2(Mathf.Cos(angle) * s, Mathf.Sin(angle) * s + upBias);
            p.gravity = gravity;
            p.life = life * (0.7f + Random.value * 0.5f);
            p.size = size * (0.7f + Random.value * 0.6f);
            p.Begin(color);
        }
    }

    public static void Dust(Vector3 position, int count = 5)
    {
        Burst(position, new Color(1f, 0.97f, 0.88f, 0.85f), count, 1.6f, 0.35f, -1.5f, 0.16f, 0.4f);
    }

    public static void CoinSparkle(Vector3 position)
    {
        Burst(position, new Color(1f, 0.9f, 0.35f, 1f), 8, 3.2f, 0.45f, 3f, 0.12f);
        Burst(position, Color.white, 3, 2f, 0.3f, 0f, 0.1f);
    }

    public static void StompPuff(Vector3 position)
    {
        Burst(position, new Color(0.78f, 0.45f, 0.92f, 1f), 10, 4.2f, 0.5f, 8f, 0.18f, 1.5f);
        Burst(position, Color.white, 4, 3f, 0.3f, 2f, 0.12f, 1f);
    }
}

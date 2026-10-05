using UnityEngine;

public class Coin : MonoBehaviour
{
    Vector3 basePosition;
    float phase;
    SpriteRenderer sr;

    void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.CoinFrame(0);
        sr.sortingOrder = 1;

        var c = gameObject.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 0.4f;

        phase = Random.value * Mathf.PI * 2f;
    }

    void Start() => basePosition = transform.position;

    void Update()
    {
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 4f + phase) * 0.08f);
        sr.sprite = PixelArt.CoinFrame(Mathf.FloorToInt(Time.time * 7f + phase));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController _)) return;
        Fx.CoinSparkle(transform.position);
        Game.Instance.AddCoin();
        Destroy(gameObject);
    }
}

public class Hazard : MonoBehaviour
{
    void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.Spikes;
        sr.sortingOrder = 1;

        var c = gameObject.AddComponent<BoxCollider2D>();
        c.isTrigger = true;
        c.size = new Vector2(0.7f, 0.4f);
        c.offset = new Vector2(0f, -0.25f);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController player))
            player.Hurt();
    }
}

public class LevelExit : MonoBehaviour
{
    void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.Flag;
        sr.sortingOrder = 1;

        var c = gameObject.AddComponent<BoxCollider2D>();
        c.isTrigger = true;
        c.size = new Vector2(1f, 3f);
        c.offset = new Vector2(0f, 1.5f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController _))
            Game.Instance.LevelComplete();
    }
}

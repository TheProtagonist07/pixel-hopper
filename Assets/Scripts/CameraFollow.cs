using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    const float MaxLookAhead = 2.4f;
    const float IdleLookAhead = 0.6f;

    public Transform target;
    public Vector2 levelSize;

    Camera cam;
    PlayerController player;
    float look;
    Vector3 basePosition;
    float shakeLeft;
    float shakeMagnitude;

    void Awake() => cam = GetComponent<Camera>();

    public void SetTarget(PlayerController p, Vector2 size)
    {
        player = p;
        target = p.transform;
        levelSize = size;
        look = IdleLookAhead * p.Facing;
        basePosition = Desired();
        shakeLeft = 0f;
        transform.position = Snap(basePosition);
    }

    void LateUpdate()
    {
        if (target == null) return;

        float speed01 = Mathf.Clamp01(player.Speed / PlayerController.MoveSpeed);
        float lookTarget = player.Facing * Mathf.Lerp(IdleLookAhead, MaxLookAhead, speed01);
        look = Mathf.Lerp(look, lookTarget, 1f - Mathf.Exp(-3f * Time.deltaTime));

        basePosition = Vector3.Lerp(basePosition, Desired(), 1f - Mathf.Exp(-10f * Time.deltaTime));

        Vector3 offset = Vector3.zero;
        if (shakeLeft > 0f)
        {
            shakeLeft -= Time.deltaTime;
            float m = shakeMagnitude * Mathf.Clamp01(shakeLeft / 0.25f);
            offset = new Vector3(Random.Range(-m, m), Random.Range(-m, m), 0f);
        }
        transform.position = Snap(basePosition + offset);
    }

    public void Shake(float duration, float magnitude)
    {
        shakeLeft = duration;
        shakeMagnitude = magnitude;
    }

    Vector3 Desired()
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float x = Mathf.Clamp(target.position.x + look, halfW, Mathf.Max(halfW, levelSize.x - halfW));
        float y = Mathf.Clamp(target.position.y + 2f, halfH, Mathf.Max(halfH, levelSize.y - halfH));
        return new Vector3(x, y, -10f);
    }

    static Vector3 Snap(Vector3 p)
    {
        const float unit = 1f / PixelArt.PPU;
        return new Vector3(Mathf.Round(p.x / unit) * unit, Mathf.Round(p.y / unit) * unit, -10f);
    }
}

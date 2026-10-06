using UnityEngine;

/// <summary>
/// The side-scrolling camera. Attached to the main camera at runtime by <see cref="Game"/>.
/// It follows the player with these goals:
///  - Look ahead: the view shifts toward the direction the player is facing, and further the faster they run,
///    because the portrait view is narrow (about 8.4 tiles wide) and players need to see hazards coming.
///  - Smooth: positions are eased with an exponential curve, which is framerate independent.
///  - Pixel-perfect: the final position is snapped to the 1/32 unit pixel grid so sprites do not shimmer.
///  - One-way: it never scrolls back. The furthest x it has reached is remembered, and the left edge of the screen
///    becomes a wall for the player (see <see cref="LeftEdge"/> and PlayerController), as in classic platformers.
///  - Clamped: it never shows beyond the level edges. Vertically the level is exactly as tall as the view, so the
///    camera effectively never moves up or down.
///  - Shake: <see cref="Shake"/> adds a short random jitter (used when the player is hurt).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    // How far (in tiles) the camera leads the player at full speed, and when standing still.
    const float MaxLookAhead = 2.4f;
    const float IdleLookAhead = 0.6f;

    /// <summary>What the camera follows (the player's transform). Set through <see cref="SetTarget"/>.</summary>
    public Transform target;

    /// <summary>Level size in tiles (width, height), used to clamp the camera inside the level.</summary>
    public Vector2 levelSize;

    Camera cam;
    PlayerController player;

    // Current horizontal look-ahead offset in tiles (smoothed; positive = ahead to the right).
    float look;

    // Smoothed camera position before shake and pixel snapping are applied.
    Vector3 basePosition;

    float shakeLeft;
    float shakeMagnitude;

    // Furthest x the camera has reached in this attempt. The camera is not allowed to go below it.
    float maxX;

    void Awake() => cam = GetComponent<Camera>();

    /// <summary>
    /// World x of the left edge of the visible area (without shake or pixel snapping, so it is stable).
    /// Because the camera never moves back, this value only increases until the next <see cref="SetTarget"/> or
    /// <see cref="Recenter"/>. The player is stopped at this line.
    /// </summary>
    public float LeftEdge => basePosition.x - cam.orthographicSize * cam.aspect;

    /// <summary>
    /// Puts the camera straight back on the player with no easing and forgets how far it had scrolled.
    /// Used after a respawn: the player returns to the stage start, so the one-way camera must return too.
    /// </summary>
    public void Recenter()
    {
        if (player == null) return;
        look = IdleLookAhead * player.Facing;
        basePosition = Desired();
        maxX = basePosition.x;
        shakeLeft = 0f;
        transform.position = Snap(basePosition);
    }

    /// <summary>
    /// Points the camera at a (new) player and instantly places it, with no easing, so a new stage
    /// or a respawn never starts with the camera sliding in from the previous position.
    /// </summary>
    public void SetTarget(PlayerController p, Vector2 size)
    {
        player = p;
        target = p.transform;
        levelSize = size;
        look = IdleLookAhead * p.Facing;
        basePosition = Desired();
        maxX = basePosition.x;
        shakeLeft = 0f;
        transform.position = Snap(basePosition);
    }

    // LateUpdate so the camera moves after the player has moved this frame.
    void LateUpdate()
    {
        if (target == null) return;

        // Look further ahead the faster the player runs, in the direction they face.
        float speed01 = Mathf.Clamp01(player.Speed / PlayerController.MoveSpeed);
        float lookTarget = player.Facing * Mathf.Lerp(IdleLookAhead, MaxLookAhead, speed01);

        // 1 - exp(-k * dt) is the framerate-independent version of "move a fixed fraction toward the target per frame".
        look = Mathf.Lerp(look, lookTarget, 1f - Mathf.Exp(-3f * Time.deltaTime));
        basePosition = Vector3.Lerp(basePosition, Desired(), 1f - Mathf.Exp(-10f * Time.deltaTime));
        // One-way scrolling: never move left of the furthest point reached, and remember a new furthest point.
        basePosition.x = Mathf.Max(basePosition.x, maxX);
        maxX = basePosition.x;

        // Screen shake fades out over the last 0.25 s so it ends smoothly instead of stopping abruptly.
        Vector3 offset = Vector3.zero;
        if (shakeLeft > 0f)
        {
            shakeLeft -= Time.deltaTime;
            float m = shakeMagnitude * Mathf.Clamp01(shakeLeft / 0.25f);
            offset = new Vector3(Random.Range(-m, m), Random.Range(-m, m), 0f);
        }
        transform.position = Snap(basePosition + offset);
    }

    /// <summary>Starts a screen shake.</summary>
    /// <param name="duration">Seconds the shake lasts.</param>
    /// <param name="magnitude">Maximum offset in world units at the start of the shake.</param>
    public void Shake(float duration, float magnitude)
    {
        shakeLeft = duration;
        shakeMagnitude = magnitude;
    }

    // Where the camera wants to be: centred on the player (plus look-ahead), clamped to the level bounds.
    Vector3 Desired()
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        // Max(halfW, ...) keeps the clamp valid when the level is narrower than the view.
        float x = Mathf.Clamp(target.position.x + look, halfW, Mathf.Max(halfW, levelSize.x - halfW));

        // The +2 sits the player in the lower part of the screen so more of the sky is visible.
        float y = Mathf.Clamp(target.position.y + 2f, halfH, Mathf.Max(halfH, levelSize.y - halfH));
        return new Vector3(x, y, -10f);
    }

    // Rounds the position to the nearest pixel (1/32 unit) so pixel art stays crisp while the camera moves.
    static Vector3 Snap(Vector3 p)
    {
        const float unit = 1f / PixelArt.PPU;
        return new Vector3(Mathf.Round(p.x / unit) * unit, Mathf.Round(p.y / unit) * unit, -10f);
    }
}

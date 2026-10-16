#if UNITY_EDITOR
// Editor-only helpers for recording a promo clip (see Assets/Editor/ClipRecorder.cs). Not compiled into builds.
//   ClipBot     - a scripted player that runs the stage like AutoPlayer but AIMS to land on the first enemy it meets
//                 (a stomp looks good on camera; AutoPlayer jumps over enemies instead).
//   ClipCapture - saves every rendered frame as a PNG while Time.captureFramerate fixes the game clock, so the video is
//                 smooth even if saving a frame takes longer than one frame of game time.
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// A scripted player for promo footage. It always runs right and jumps at ledges, walls and spikes (like
/// <see cref="AutoPlayer"/>), but for enemies it predicts where the enemy will be when a jump comes down
/// (<see cref="landingTime"/> seconds later) and jumps when that spot matches where the hero will be, so the hero lands on top.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class ClipBot : MonoBehaviour
{
    /// <summary>Seconds a full jump takes to come back down to enemy-head height (about 0.88 s with the current physics).</summary>
    public float landingTime = 0.88f;

    PlayerController player;
    Collider2D col;
    float jumpHoldUntil;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        col = GetComponent<Collider2D>();
        player.bot = true;
    }

    void Update()
    {
        player.botMove = 1f;
        player.botJumpHeld = Time.time < jumpHoldUntil;
        if (!player.IsGrounded) return;

        Bounds b = col.bounds;
        float front = b.max.x;
        bool jump = false;

        // Ledge: no ground just ahead of the feet.
        if (Physics2D.Raycast(new Vector2(front + 0.3f, b.min.y + 0.2f), Vector2.down, 0.6f, PlayerController.GroundMask).collider == null) jump = true;
        // Wall: a solid tile in front at knee height.
        if (Physics2D.Raycast(new Vector2(front, b.min.y + 0.3f), Vector2.right, 0.8f, PlayerController.GroundMask).collider != null) jump = true;

        // Spikes within 1.2 tiles ahead (they are triggers, so switch trigger queries on for this one check).
        Physics2D.queriesHitTriggers = true;
        var spikes = Physics2D.OverlapBoxAll(new Vector2(front + 0.6f, b.center.y), new Vector2(1.2f, 1.0f), 0f);
        Physics2D.queriesHitTriggers = false;
        foreach (var s in spikes) if (s.GetComponent<Hazard>() != null) jump = true;

        // Enemies: jump when the hero's landing spot (after landingTime seconds at full run speed) meets the enemy's predicted spot.
        foreach (var e in FindObjectsByType<Enemy>())
        {
            if (e.IsDead) continue;
            float dx = e.transform.position.x - transform.position.x;
            if (dx < 0.4f || dx > 9f) continue;                                   // only enemies ahead
            if (Mathf.Abs(e.transform.position.y - transform.position.y) > 1.2f) continue; // same level
            float enemyVx = e.GetComponent<Rigidbody2D>().linearVelocity.x;
            float enemyAt = e.transform.position.x + enemyVx * landingTime;
            float heroAt = transform.position.x + PlayerController.MoveSpeed * landingTime;
            if (Mathf.Abs(enemyAt - heroAt) < 0.3f) jump = true;
            else if (dx < 1.3f) jump = true;                                      // safety: too close, jump anyway
        }

        if (jump)
        {
            player.botJumpPress = true;
            player.botJumpHeld = true;
            jumpHoldUntil = Time.time + 0.7f;
        }
    }
}

/// <summary>
/// Saves every rendered frame (including the UI) as numbered PNGs (00000.png, 00001.png ...) into a folder.
/// Time.captureFramerate makes the game advance exactly 1/fps seconds per frame, so the footage plays back at the right speed
/// however long saving each frame takes. Stop with <see cref="Stop"/>.
/// </summary>
public class ClipCapture : MonoBehaviour
{
    /// <summary>Number of frames written so far.</summary>
    public int Frames { get; private set; }

    bool running;

    /// <summary>Starts capturing into <paramref name="dir"/> (created if missing) at <paramref name="fps"/> frames per second of game time.</summary>
    public static ClipCapture Begin(string dir, int fps, int superSize)
    {
        Directory.CreateDirectory(dir);
        var go = new GameObject("ClipCapture");
        var c = go.AddComponent<ClipCapture>();
        c.running = true;
        Time.captureFramerate = fps;
        c.StartCoroutine(c.Run(dir, superSize));
        return c;
    }

    /// <summary>Stops capturing and restores normal timing.</summary>
    public void Stop()
    {
        running = false;
        Time.captureFramerate = 0;
        Destroy(gameObject, 0.5f);
    }

    IEnumerator Run(string dir, int superSize)
    {
        while (running)
        {
            yield return new WaitForEndOfFrame();
            if (!running) break;
            var tex = ScreenCapture.CaptureScreenshotAsTexture(superSize);
            File.WriteAllBytes(Path.Combine(dir, Frames.ToString("D5") + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Frames++;
        }
    }
}
#endif

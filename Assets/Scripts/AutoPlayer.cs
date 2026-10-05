#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

// Editor-only: records the game view to numbered PNG frames for promo clips.
public class DebugRecorder : MonoBehaviour
{
    public static void Record(string dir, float seconds, float fps, int superSize = 1)
    {
        var go = new GameObject("DebugRecorder");
        var rec = go.AddComponent<DebugRecorder>();
        rec.StartCoroutine(rec.Run(dir, seconds, fps, superSize));
    }

    IEnumerator Run(string dir, float seconds, float fps, int superSize)
    {
        System.IO.Directory.CreateDirectory(dir);
        float interval = 1f / fps;
        float start = Time.unscaledTime;
        float next = start;
        int index = 0;
        while (Time.unscaledTime - start < seconds)
        {
            yield return new WaitForEndOfFrame();
            if (Time.unscaledTime < next) continue;
            var tex = ScreenCapture.CaptureScreenshotAsTexture(superSize);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, index.ToString("D4") + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            index++;
            next += interval;
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "done.txt"), index + " frames over " + (Time.unscaledTime - start).ToString("F2") + "s");
        Destroy(gameObject);
    }
}

// Editor-only: captures the full game view (including IMGUI) at end of frame.
public class DebugShot : MonoBehaviour
{
    public static void Capture(string path, int superSize = 1)
    {
        var go = new GameObject("DebugShot");
        var shot = go.AddComponent<DebugShot>();
        shot.StartCoroutine(shot.Run(path, superSize));
    }

    IEnumerator Run(string path, int superSize)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture(superSize);
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        Destroy(gameObject);
    }
}

// Editor-only test bot used to check that every stage can be completed. Not part of builds.
[RequireComponent(typeof(PlayerController))]
public class AutoPlayer : MonoBehaviour
{
    PlayerController player;
    Collider2D col;
    float jumpHoldUntil;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        col = GetComponent<Collider2D>();
    }

    void Update()
    {
        player.botMove = 1f;
        player.botJumpHeld = Time.time < jumpHoldUntil;

        if (!player.IsGrounded) return;

        Bounds b = col.bounds;
        float front = b.max.x;
        bool jump = false;

        bool groundAhead = Physics2D.Raycast(new Vector2(front + 0.3f, b.min.y + 0.2f), Vector2.down, 0.6f, PlayerController.GroundMask).collider != null;
        if (!groundAhead) jump = true;

        if (Physics2D.Raycast(new Vector2(front, b.min.y + 0.3f), Vector2.right, 0.8f, PlayerController.GroundMask).collider != null)
            jump = true;

        Physics2D.queriesHitTriggers = true;
        var ahead = Physics2D.OverlapBoxAll(new Vector2(front + 1.0f, b.center.y), new Vector2(2.0f, 1.0f), 0f);
        var spikesAhead = Physics2D.OverlapBoxAll(new Vector2(front + 0.6f, b.center.y), new Vector2(1.2f, 1.0f), 0f);
        Physics2D.queriesHitTriggers = false;
        foreach (var hit in ahead)
        {
            if (hit.GetComponent<Enemy>() != null) jump = true;
        }
        foreach (var hit in spikesAhead)
        {
            if (hit.GetComponent<Hazard>() != null) jump = true;
        }

        if (jump)
        {
            player.botJumpPress = true;
            player.botJumpHeld = true;
            jumpHoldUntil = Time.time + 0.7f;
        }
    }
}
#endif

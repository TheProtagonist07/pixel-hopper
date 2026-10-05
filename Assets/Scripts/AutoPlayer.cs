#if UNITY_EDITOR
// Editor-only developer tools. This whole file is wrapped in UNITY_EDITOR, so none of it is compiled into
// builds (it cannot ship in the WebGL game). It contains three helpers:
//   DebugRecorder - saves the game view as numbered PNG frames (used to make the promo clip),
//   DebugShot     - saves one screenshot of the game view including the UI (used for store screenshots),
//   AutoPlayer    - a simple bot that plays a stage, used to prove every stage can be completed.
// They are started from an editor script or a menu item, for example:
//   DebugShot.Capture("/path/shot.png", 3);
//   Game.AutoPlay = true; Game.DebugStartStage = 0;   (then enter Play mode)
using System.Collections;
using UnityEngine;

/// <summary>
/// Records the game view to numbered PNG files (0000.png, 0001.png, ...) for promo videos and GIFs.
/// Frames are captured at the end of each rendered frame so the UI canvas is included.
/// A "done.txt" file is written when finished. Turn the frames into a video with ffmpeg.
/// </summary>
public class DebugRecorder : MonoBehaviour
{
    /// <summary>Starts recording.</summary>
    /// <param name="dir">Folder to write the frames into (created if missing).</param>
    /// <param name="seconds">How long to record, in real seconds.</param>
    /// <param name="fps">Capture rate in frames per second.</param>
    /// <param name="superSize">Resolution multiplier of the game view (2 doubles width and height).</param>
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
        float next = start;   // real time at which the next frame should be captured
        int index = 0;
        while (Time.unscaledTime - start < seconds)
        {
            // Wait until all rendering (including UI) for this frame is finished, then maybe capture.
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

/// <summary>
/// Saves a single screenshot of the full game view, including the UI.
/// The standard Unity screenshot tool can miss the UI overlay; waiting for the end of the frame fixes that.
/// The image contains black bars if the game view is wider than 9:16; crop them afterwards.
/// </summary>
public class DebugShot : MonoBehaviour
{
    /// <summary>Takes the screenshot at the end of the current frame and writes a PNG.</summary>
    /// <param name="path">Full file path of the PNG to write.</param>
    /// <param name="superSize">Resolution multiplier (3 gives crisp pixel art).</param>
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

/// <summary>
/// A simple test bot. It always runs right and decides when to jump by looking ahead with rays and overlap boxes:
/// it jumps at ledges (no ground ahead), at walls, and when spikes or an enemy are close in front.
/// It feeds PlayerController through the editor-only bot fields instead of the keyboard.
///
/// It is not meant to play perfectly (it gets hit sometimes), only to show that each generated stage can be
/// finished. Game.AutoPlay adds it to the player; deaths and stage times are recorded in Game.DebugLog.
/// Run it again whenever physics or level generation changes.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class AutoPlayer : MonoBehaviour
{
    PlayerController player;
    Collider2D col;
    float jumpHoldUntil; // the bot holds the jump button for 0.7 s after jumping, to get a full-height jump

    void Awake()
    {
        player = GetComponent<PlayerController>();
        col = GetComponent<Collider2D>();
    }

    void Update()
    {
        player.botMove = 1f;
        player.botJumpHeld = Time.time < jumpHoldUntil;

        // Only decide to jump while standing on the ground.
        if (!player.IsGrounded) return;

        Bounds b = col.bounds;
        float front = b.max.x; // the bot's leading edge
        bool jump = false;

        // 1) Ledge: no ground just ahead of the feet => jump now.
        bool groundAhead = Physics2D.Raycast(new Vector2(front + 0.3f, b.min.y + 0.2f), Vector2.down, 0.6f, PlayerController.GroundMask).collider != null;
        if (!groundAhead) jump = true;

        // 2) Wall: a solid tile right in front at knee height => jump.
        if (Physics2D.Raycast(new Vector2(front, b.min.y + 0.3f), Vector2.right, 0.8f, PlayerController.GroundMask).collider != null)
            jump = true;

        // 3) Enemies within 2 tiles ahead, spikes within 1.2 tiles ahead => jump.
        // Spikes and enemy hit boxes are triggers, which rays normally ignore, so trigger queries are
        // switched on just for these two checks and restored right after.
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
            // Set held immediately: if it were released in the same frame, the jump would be cut short.
            player.botJumpHeld = true;
            jumpHoldUntil = Time.time + 0.7f;
        }
    }
}
#endif

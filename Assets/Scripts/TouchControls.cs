using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns screen touches into movement and jump input for phones and tablets.
/// The playfield is split into zones (relative to the visible 9:16 view, not the whole screen):
/// the left quarter moves left, the second quarter moves right, and the right half jumps. While the player holds
/// a gun, the right half is split: the middle zone (50% to 72% of the width) fires and only the last 28% jumps.
/// This works with a thumb on each side of the phone. Several touches are tracked at once, so you can
/// hold "right" with one thumb and tap "jump" with the other.
/// </summary>
public static class TouchControls
{
    /// <summary>Right edge of the fire zone, as a fraction of the view width (the jump zone is everything to its right).</summary>
    public const float FireZoneEnd = 0.72f;

    // Becomes true the first time a real touch is seen in this session.
    static bool touchUsed;

    /// <summary>
    /// True when touch UI (on-screen buttons, "TAP TO START" text) should be shown.
    /// Desktop browsers often report a touchscreen device that is never actually used, so we only switch to
    /// touch mode on mobile platforms or after a real touch has happened. A laptop with a touchscreen therefore
    /// keeps the keyboard instructions until someone really touches the screen.
    /// </summary>
    public static bool Available => touchUsed || Application.isMobilePlatform;

    /// <summary>
    /// Reads all active touches and reports the combined input for this frame.
    /// </summary>
    /// <param name="view">The visible playfield rectangle in screen pixels. Touch zones are relative to it,
    /// so pillarbox bars on wide screens do not shift the zones.</param>
    /// <param name="armed">True while the player has a gun; this adds the fire zone and shrinks the jump zone.</param>
    /// <param name="move">-1 (left), 0, or +1 (right); two opposing touches cancel out.</param>
    /// <param name="jumpPressed">True only on the frame a touch started in the jump zone (used for jump buffering).</param>
    /// <param name="jumpHeld">True while any touch is held in the jump zone (used for variable jump height).</param>
    /// <param name="firePressed">True only on the frame a touch started in the fire zone (only while armed).</param>
    public static void Read(Rect view, bool armed, out float move, out bool jumpPressed, out bool jumpHeld, out bool firePressed)
    {
        move = 0f;
        jumpPressed = false;
        jumpHeld = false;
        firePressed = false;

        var screen = Touchscreen.current;
        if (screen == null) return;

        foreach (var touch in screen.touches)
        {
            if (!touch.press.isPressed) continue;
            touchUsed = true;

            // Convert the touch x position to 0..1 across the visible playfield.
            float x = touch.position.ReadValue().x;
            float rel = (x - view.x) / view.width;
            if (rel < 0.25f) move -= 1f;
            else if (rel < 0.5f) move += 1f;
            else if (armed && rel < FireZoneEnd)
            {
                if (touch.press.wasPressedThisFrame) firePressed = true;
            }
            else
            {
                jumpHeld = true;
                if (touch.press.wasPressedThisFrame) jumpPressed = true;
            }
        }
    }

    /// <summary>
    /// True on the frame the primary touch begins. Used for "tap to start / retry" on the title and result screens.
    /// </summary>
    public static bool AnyPress()
    {
        var screen = Touchscreen.current;
        if (screen == null || !screen.primaryTouch.press.wasPressedThisFrame) return false;
        touchUsed = true;
        return true;
    }
}

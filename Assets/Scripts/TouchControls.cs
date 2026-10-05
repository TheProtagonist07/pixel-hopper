using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns screen touches into movement and jump input for phones and tablets.
/// The playfield is split into three zones (relative to the visible 9:16 view, not the whole screen):
/// the left quarter moves left, the second quarter moves right, and the whole right half jumps.
/// This works with a thumb on each side of the phone. Several touches are tracked at once, so you can
/// hold "right" with one thumb and tap "jump" with the other.
/// </summary>
public static class TouchControls
{
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
    /// <param name="move">-1 (left), 0, or +1 (right); two opposing touches cancel out.</param>
    /// <param name="jumpPressed">True only on the frame a touch started in the jump zone (used for jump buffering).</param>
    /// <param name="jumpHeld">True while any touch is held in the jump zone (used for variable jump height).</param>
    public static void Read(Rect view, out float move, out bool jumpPressed, out bool jumpHeld)
    {
        move = 0f;
        jumpPressed = false;
        jumpHeld = false;

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

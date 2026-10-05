using UnityEngine;
using UnityEngine.InputSystem;

public static class TouchControls
{
    static bool touchUsed;

    // Touch UI shows on mobile devices, or once a real touch has been seen (desktop browsers often report a touchscreen device that is never used).
    public static bool Available => touchUsed || Application.isMobilePlatform;

    // Left quarter = move left, second quarter = move right, right half = jump.
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

    public static bool AnyPress()
    {
        var screen = Touchscreen.current;
        if (screen == null || !screen.primaryTouch.press.wasPressedThisFrame) return false;
        touchUsed = true;
        return true;
    }
}

using UnityEngine;

// The game stops while the Esc menu is open: time stands still, the cursor is free, and
// the player's look, movement and hands ignore input until it closes.
public static class GamePause
{
    static float timeScaleBefore = 1f;

    public static bool Paused { get; private set; }

    public static void Set(bool paused)
    {
        if (paused == Paused) return;
        Paused = paused;
        if (paused)
        {
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = timeScaleBefore;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Paused = false;
}

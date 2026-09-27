using UnityEngine;
using UnityEngine.SceneManagement;

// Esc → Restart this shift. The store is loaded again from scratch (shelves, spills,
// customers, Karen, and you back where you start), and the shift you were on begins
// again when you clock in; between shifts, the next one does. What Karen has learnt about
// you over earlier shifts stays: it lives in her ledger, not in the scene.
public static class ShiftRestart
{
    static int completed = -1;

    public static void Restart()
    {
        ShiftManager shift = Object.FindAnyObjectByType<ShiftManager>();
        completed = shift == null ? -1 : shift.IsShiftActive ? shift.ShiftNumber - 1 : shift.ShiftNumber;
        GamePause.Set(false);

        SceneManager.sceneLoaded -= Restore;
        SceneManager.sceneLoaded += Restore;
        Scene active = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // A scene that isn't in the build list can still be reloaded in the editor, by path.
        if (active.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(active.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(active.buildIndex, LoadSceneMode.Single);
    }

    static void Restore(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= Restore;
        if (completed < 0) return;
        ShiftManager shift = Object.FindAnyObjectByType<ShiftManager>();
        if (shift != null) shift.SetShiftNumber(completed);
        completed = -1;
    }
}

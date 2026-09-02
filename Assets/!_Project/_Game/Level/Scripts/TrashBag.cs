using UnityEngine;

// A bagged-up sack of rubbish pulled out of a bin. Carried like any other item, and only
// counted as dealt with once it has been dropped into the container out back.
public class TrashBag : MonoBehaviour
{
    // Bags still waiting to be taken out — the trash task isn't done while any exist.
    public static int ActiveCount { get; private set; }

    void OnEnable()
    {
        ActiveCount++;
        TaskManager.NotifyWorldChanged();
    }

    void OnDisable()
    {
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
        TaskManager.NotifyWorldChanged();
    }
}

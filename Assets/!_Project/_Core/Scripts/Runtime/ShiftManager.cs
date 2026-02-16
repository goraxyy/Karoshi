using UnityEngine;

public class ShiftManager : MonoBehaviour
{
    [Header("Layouts")]
    public GameObject[] layouts;

    [Header("Shift Settings")]
    public float shiftDurationSeconds = 480f; // 8 minutes can be increased

    [Header("References")]
    public TaskManager taskManager;
    public BurnoutSystem burnoutSystem;
    public EnemyAI enemyAI;

    int currentShiftIndex = -1;
    float shiftTimeRemaining;
    bool shiftActive;

    void Start()
    {
        StartNextShift();
    }

    void Update()
    {
        if (!shiftActive) return;

        shiftTimeRemaining -= Time.deltaTime;

        if (shiftTimeRemaining <= 0f)
            EndShift();
    }

    public void StartNextShift()
    {
        currentShiftIndex++;

        if (currentShiftIndex >= layouts.Length)
        {
            Debug.Log("Demo complete! All shifts finished.");
            // TODO: Show completion screen
            return;
        }

        // Activate correct layout
        for (int i = 0; i < layouts.Length; i++)
            layouts[i].SetActive(i == currentShiftIndex);

        shiftTimeRemaining = shiftDurationSeconds;
        shiftActive = true;

        // Initialize systems
        if (taskManager != null)
            taskManager.GenerateShiftTasks();

        if (burnoutSystem != null)
            burnoutSystem.ResetForNewShift(currentShiftIndex);

        if (enemyAI != null)
            enemyAI.ResetForNewShift();

        Debug.Log($"Shift {currentShiftIndex + 1} started!");
    }

    public void EndShiftEarly()
    {
        Debug.Log("All tasks complete! Ending shift early.");
        EndShift();
    }

    void EndShift()
    {
        shiftActive = false;
        Debug.Log($"Shift {currentShiftIndex + 1} complete!");

        // TODO: Show results screen, then:
        Invoke(nameof(StartNextShift), 3f);
    }
}

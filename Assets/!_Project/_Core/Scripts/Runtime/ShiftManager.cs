using System;
using UnityEngine;

// A shift runs from clocking in at the puncher to clocking out again.
// The clock stops letting customers in once the shift time is up, but the shift itself
// only ends when the player punches out.
public class ShiftManager : MonoBehaviour
{
    [Header("Shift Settings")]
    [Tooltip("How long customers keep arriving, in seconds.")]
    public float shiftDurationSeconds = 300f;   // 5 minutes

    [Header("References")]
    public TaskManager taskManager;
    public CustomerSpawner customerSpawner;
    public BurnoutSystem burnoutSystem;
    public EnemyAI enemyAI;

    public bool IsShiftActive { get; private set; }
    public bool CustomersAllowed { get; private set; }
    public float TimeRemaining { get; private set; }
    public int ShiftNumber { get; private set; }

    public event Action ShiftStateChanged;

    void Awake()
    {
        if (taskManager == null) taskManager = FindAnyObjectByType<TaskManager>();
        if (customerSpawner == null) customerSpawner = FindAnyObjectByType<CustomerSpawner>();
        if (burnoutSystem == null) burnoutSystem = FindAnyObjectByType<BurnoutSystem>();
        if (enemyAI == null) enemyAI = FindAnyObjectByType<EnemyAI>();
    }

    void Start()
    {
        // Nothing runs until the player clocks in.
        SetCustomersAllowed(false);
    }

    void Update()
    {
        if (!IsShiftActive || !CustomersAllowed) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            SetCustomersAllowed(false);      // doors close; shift ends when the player punches out
            ShiftStateChanged?.Invoke();
        }
    }

    // The shift can't be closed with spills on the floor, a full bin, or gaps on the shelves.
    public bool CanClockOut => !IsShiftActive || taskManager == null || taskManager.AllComplete;

    // Called by the puncher.
    public void ToggleShift()
    {
        if (!IsShiftActive) { StartShift(); return; }

        if (!CanClockOut)
        {
            Debug.Log("Can't clock out yet — finish the shift tasks first.");
            return;
        }

        EndShift();
    }

    public void StartShift()
    {
        if (IsShiftActive) return;

        ShiftNumber++;
        IsShiftActive = true;
        TimeRemaining = shiftDurationSeconds;
        SetCustomersAllowed(true);

        if (taskManager != null)
            taskManager.BeginShift(ShiftNumber);

        if (burnoutSystem != null)
            burnoutSystem.ResetForNewShift(ShiftNumber - 1);

        if (enemyAI != null)
            enemyAI.ResetForNewShift();

        Debug.Log($"Shift {ShiftNumber} started ({shiftDurationSeconds:0}s).");
        ShiftStateChanged?.Invoke();
    }

    public void EndShift()
    {
        if (!IsShiftActive) return;

        IsShiftActive = false;
        TimeRemaining = 0f;
        SetCustomersAllowed(false);

        if (taskManager != null)
            taskManager.EndShift();

        Debug.Log($"Shift {ShiftNumber} ended.");
        ShiftStateChanged?.Invoke();
    }

    void SetCustomersAllowed(bool allowed)
    {
        CustomersAllowed = allowed;
        if (customerSpawner != null)
            customerSpawner.spawningEnabled = allowed;
    }
}

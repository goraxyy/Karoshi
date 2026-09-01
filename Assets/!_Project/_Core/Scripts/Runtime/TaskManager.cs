using System;
using System.Collections.Generic;
using UnityEngine;

// The shift checklist. Every task reflects LIVE world state rather than a running tally, so a
// task un-scratches itself the moment a customer drops litter, uses the bin, or empties a shelf.
// The shift can only be clocked out once all three read zero.
public class TaskManager : MonoBehaviour
{
    public enum TaskKind { Mop, Trash, Stock }

    public readonly struct ShiftTask
    {
        public readonly TaskKind Kind;
        public readonly string Label;
        public readonly int Outstanding;
        public readonly int Max;        // 0 means "no fixed cap"

        public ShiftTask(TaskKind kind, string label, int outstanding, int max)
        {
            Kind = kind; Label = label; Outstanding = outstanding; Max = max;
        }

        public bool IsComplete => Outstanding == 0;

        public override string ToString()
        {
            return Max > 0 ? $"{Label}  {Outstanding}/{Max}" : $"{Label}  {Outstanding}";
        }
    }

    [Header("Mess allowance")]
    [Tooltip("How many spills can exist at once on the first shift.")]
    public int baseMaxDirt = 5;
    public int dirtGrowthPerShift = 2;

    public int MaxDirt { get; private set; }
    public bool ShiftRunning { get; private set; }

    public event Action Changed;

    // ---- live world state ----
    public int DirtOutstanding => Dirt.ActiveCount;

    public int TrashOutstanding
    {
        get
        {
            int total = 0;
            var cans = Trashcan.All;
            for (int i = 0; i < cans.Count; i++) total += cans[i].UsageCount;
            return total;
        }
    }

    public int TrashCapacity
    {
        get
        {
            int total = 0;
            var cans = Trashcan.All;
            for (int i = 0; i < cans.Count; i++) total += cans[i].capacity;
            return total;
        }
    }

    public int ShelvesOutstanding => ShelfUnit.NotFullCount;

    public bool AllComplete =>
        DirtOutstanding == 0 && TrashOutstanding == 0 && ShelvesOutstanding == 0;

    public bool HasTasks => ShiftRunning;

    public IEnumerable<ShiftTask> Tasks
    {
        get
        {
            yield return new ShiftTask(TaskKind.Mop, "Mop up spills", DirtOutstanding, MaxDirt);
            yield return new ShiftTask(TaskKind.Trash, "Empty the trash", TrashOutstanding, TrashCapacity);
            yield return new ShiftTask(TaskKind.Stock, "Restock shelves", ShelvesOutstanding, 0);
        }
    }

    // shiftNumber is 1-based: the first shift allows baseMaxDirt spills.
    public void BeginShift(int shiftNumber)
    {
        MaxDirt = Mathf.Max(1, baseMaxDirt + dirtGrowthPerShift * Mathf.Max(0, shiftNumber - 1));
        ShiftRunning = true;
        NotifyChanged();
    }

    public void EndShift()
    {
        ShiftRunning = false;
        NotifyChanged();
    }

    // Called by dirt, shelves and bins whenever they change state.
    public void NotifyChanged() => Changed?.Invoke();

    public static void NotifyWorldChanged()
    {
        if (Instance != null) Instance.NotifyChanged();
    }

    static TaskManager instance;
    public static TaskManager Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<TaskManager>();
            return instance;
        }
    }

    void Awake() => instance = this;
}

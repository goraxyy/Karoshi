using System;
using System.Collections.Generic;
using UnityEngine;

// The shift checklist.
//
// Mopping is a counted quota: clean the shift's allowance of spills (5, then 7, ...).
// Restocking and the bin are simple states — either everything is stocked / the bin is
// empty, or the task is open again. A state task un-checks itself the moment a customer
// takes something off a shelf or uses the bin.
//
// The shift can only be clocked out once all three read complete.
public class TaskManager : MonoBehaviour
{
    public enum TaskKind { Mop, Trash, Stock, Serve }

    public readonly struct ShiftTask
    {
        public readonly TaskKind Kind;
        public readonly string Label;
        public readonly string Detail;     // "3/5" for the quota task, empty for state tasks
        public readonly bool IsComplete;

        public ShiftTask(TaskKind kind, string label, string detail, bool isComplete)
        {
            Kind = kind; Label = label; Detail = detail; IsComplete = isComplete;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Detail) ? Label : $"{Label}  {Detail}";
        }
    }

    [Header("Mopping quota")]
    [Tooltip("How many spills must be mopped on the first shift.")]
    public int baseMopQuota = 5;
    [Tooltip("Added to the quota each following shift (5, 7, 9, ...).")]
    public int mopQuotaGrowth = 2;

    public int MopQuota { get; private set; }
    public int MoppedThisShift { get; private set; }
    public bool ShiftRunning { get; private set; }

    // Spills on the floor at once are capped at the same number as the quota.
    public int MaxDirt => MopQuota;

    public event Action Changed;

    // ---- live world state ----
    public bool MopQuotaMet => MoppedThisShift >= MopQuota;
    public bool ShelvesStocked => ShelfUnit.NotFullCount == 0;

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

    // Bagging a bin isn't enough — the bag has to reach the container out back.
    public bool TrashEmpty => TrashOutstanding == 0 && TrashBag.ActiveCount == 0;

    // Nobody may be left standing at the till when the shift closes.
    public int CustomersWaiting => CustomerNPC.WaitingCount;
    public bool AllCustomersServed => CustomersWaiting == 0;

    public bool AllComplete => MopQuotaMet && ShelvesStocked && TrashEmpty && AllCustomersServed;
    public bool HasTasks => ShiftRunning;

    public IEnumerable<ShiftTask> Tasks
    {
        get
        {
            yield return new ShiftTask(TaskKind.Mop, "Mop up spills",
                $"{Mathf.Min(MoppedThisShift, MopQuota)}/{MopQuota}", MopQuotaMet);

            yield return new ShiftTask(TaskKind.Stock, "Restock the shelves",
                string.Empty, ShelvesStocked);

            string trashDetail = TrashBag.ActiveCount > 0 ? "take the bag out back" : string.Empty;
            yield return new ShiftTask(TaskKind.Trash, "Empty the trash", trashDetail, TrashEmpty);

            string serveDetail = CustomersWaiting > 0 ? $"{CustomersWaiting} waiting" : string.Empty;
            yield return new ShiftTask(TaskKind.Serve, "Serve the customers", serveDetail, AllCustomersServed);
        }
    }

    // shiftNumber is 1-based: the first shift uses the base quota.
    public void BeginShift(int shiftNumber)
    {
        MopQuota = Mathf.Max(1, baseMopQuota + mopQuotaGrowth * Mathf.Max(0, shiftNumber - 1));
        MoppedThisShift = 0;
        ShiftRunning = true;
        NotifyChanged();
    }

    public void EndShift()
    {
        ShiftRunning = false;
        NotifyChanged();
    }

    // Counted by Dirt when a spill is fully mopped.
    public void ReportMopped()
    {
        MoppedThisShift++;
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

using System.Collections.Generic;
using UnityEngine;

public class TaskManager : MonoBehaviour
{
    public List<ShelfSlot> activeTasks = new List<ShelfSlot>();
    public int tasksPerShift = 8;

    public void GenerateShiftTasks()
    {
        activeTasks.Clear();

        ShelfSlot[] allSlots = FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None);
        List<ShelfSlot> emptySlots = new List<ShelfSlot>();

        foreach (var slot in allSlots)
        {
            if (!slot.isFilled)
                emptySlots.Add(slot);
        }

        // Pick random empty slots
        int count = Mathf.Min(tasksPerShift, emptySlots.Count);
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, emptySlots.Count);
            activeTasks.Add(emptySlots[randomIndex]);
            emptySlots.RemoveAt(randomIndex);
        }

        Debug.Log($"Generated {activeTasks.Count} tasks for this shift.");
    }

    public void NotifySlotFilled(ShelfSlot slot)
    {
        if (activeTasks.Contains(slot))
        {
            activeTasks.Remove(slot);
            Debug.Log($"Task complete! {activeTasks.Count} remaining.");

            if (activeTasks.Count == 0)
            {
                ShiftManager shiftManager = GetComponent<ShiftManager>();
                if (shiftManager != null)
                    shiftManager.EndShiftEarly();
            }
        }
    }
}
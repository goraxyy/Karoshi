using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public CarrySlot carrySlot;

    [Header("Slot UI Elements")]
    public Image[] slotBackgrounds = new Image[4];  // The 4 black box backgrounds
    public Image[] itemFills = new Image[4];         // White square inside each box (visible when occupied)
    public Image[] slotBorders = new Image[4];       // Border image for active slot highlight

    [Header("Colors")]
    public Color activeSlotBorderColor = Color.yellow;
    public Color inactiveSlotBorderColor = Color.clear;

    void Update()
    {
        for (int i = 0; i < 4; i++)
        {
            bool occupied = carrySlot.items[i] != null;
            bool active = carrySlot.activeSlot == i;

            // Show white fill if slot has an item
            if (itemFills[i] != null)
                itemFills[i].gameObject.SetActive(occupied);

            // Highlight border if this is the active slot
            if (slotBorders[i] != null)
                slotBorders[i].color = active ? activeSlotBorderColor : inactiveSlotBorderColor;
        }
    }
}
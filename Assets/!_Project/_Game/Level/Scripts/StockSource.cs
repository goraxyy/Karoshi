using UnityEngine;

// The stock pallet in the back room. Interacting takes a smaller clone off it —
// the source object itself is never modified or consumed.
public class StockSource : HighlightInteractable
{
    [Header("Clone")]
    [Tooltip("Item spawned into shelf slots when this crate is used.")]
    public GameObject itemPrefab;

    [Tooltip("How much smaller the carried clone is than the source.")]
    public float cloneScale = 0.35f;

    [Tooltip("Where the clone sits in the player's hands.")]
    public Vector3 holdPosition = new Vector3(0.35f, -0.3f, 0.7f);
    public Vector3 holdRotation = new Vector3(0f, 0f, 0f);

    public override void Interact(PlayerInteract player)
    {
        if (player.tools == null || player.tools.IsHolding) return;

        // Clone from the source's own geometry, then strip the parts that only make
        // sense on the pallet so the carried copy is just a crate.
        GameObject clone = Instantiate(gameObject, transform.position, transform.rotation);
        clone.name = "Stock_crate";

        var sourceComponent = clone.GetComponent<StockSource>();
        if (sourceComponent != null) DestroyImmediate(sourceComponent);

        var staleOutline = clone.GetComponent<OutlineHighlight>();
        if (staleOutline != null) DestroyImmediate(staleOutline);

        var crate = clone.AddComponent<StockCrate>();
        crate.itemPrefab = itemPrefab;

        player.tools.Hold(clone, holdPosition, holdRotation, cloneScale);
    }

    public override string GetPrompt() => "Take stock crate";
}

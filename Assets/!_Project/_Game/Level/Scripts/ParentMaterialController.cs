using UnityEngine;

public class ParentMaterialController : MonoBehaviour
{
    public Material sharedMaterial; // Drag your material here in Inspector

    void Start()
    {
        ApplyMaterialToChildren();
    }

    [ContextMenu("Apply Material to Children")]
    public void ApplyMaterialToChildren()
    {
        if (sharedMaterial == null) return;

        MeshRenderer[] allRenderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer rend in allRenderers)
        {
            // Items stocked on this shelf keep their own look.
            if (rend.GetComponentInParent<Item>() != null) continue;

            // sharedMaterial, not material: assigning .material clones the material per
            // renderer at runtime, which breaks batching and leaks a material per shelf part.
            if (rend.sharedMaterial != sharedMaterial)
                rend.sharedMaterial = sharedMaterial;
        }
    }
}

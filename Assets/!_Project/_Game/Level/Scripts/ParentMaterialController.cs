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
        MeshRenderer[] allRenderers = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer rend in allRenderers)
        {
            rend.material = sharedMaterial; // Assigns shared material
            // Or for color: rend.material.color = Color.red;
        }
    }
}

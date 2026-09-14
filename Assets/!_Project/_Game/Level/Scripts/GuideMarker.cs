using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// "Look here" markers built entirely in code, so nothing needs authoring in the scene:
// a flat ring that lies on the floor and a bobbing arrow that hangs over a shelf.
//
// Meshes and materials are shared between every marker in the level — a hundred of these
// cost one draw call each and no assets.
public class GuideMarker : MonoBehaviour
{
    [Tooltip("Stay flat on the floor under this transform. Leave null to sit still.")]
    public Transform follow;
    public float followHeight = 0.03f;

    public float bobAmplitude;
    public float bobSpeed = 2f;
    public float spinSpeed;

    Vector3 anchor;
    float phase;

    void Awake()
    {
        anchor = transform.position;
        phase = Random.value * 10f;
    }

    public void SetAnchor(Vector3 position) => anchor = position;

    void LateUpdate()
    {
        Vector3 position = follow != null ? follow.position : anchor;

        if (follow != null)
            position.y = follow.position.y + followHeight;
        else if (bobAmplitude > 0f)
            position.y = anchor.y + Mathf.Sin((Time.time + phase) * bobSpeed) * bobAmplitude;

        transform.position = position;

        if (spinSpeed != 0f)
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    // --- factories ----------------------------------------------------------

    // A flat ring lying on the floor. radius is in metres; innerRatio 0 gives a filled disc.
    public static GuideMarker CreateRing(string name, Color colour, float radius, float innerRatio)
    {
        GuideMarker marker = Build(name, RingMesh(innerRatio), colour, false);
        marker.SetRadius(radius);
        return marker;
    }

    // A downward-pointing arrow that floats above something and can be seen from a distance.
    public static GuideMarker CreateBeacon(string name, Color colour, float size)
    {
        GuideMarker marker = Build(name, ArrowMesh(), colour, true);
        marker.transform.localScale = new Vector3(size, size * 1.4f, size);
        marker.bobAmplitude = size * 0.35f;
        marker.spinSpeed = 55f;
        return marker;
    }

    public void SetRadius(float radius)
    {
        transform.localScale = new Vector3(radius, 1f, radius);
    }

    static GuideMarker Build(string name, Mesh mesh, Color colour, bool onTop)
    {
        GameObject go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = MaterialFor(colour, onTop);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;

        return go.AddComponent<GuideMarker>();
    }

    // --- shared materials ---------------------------------------------------

    static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();

    static Material MaterialFor(Color colour, bool onTop)
    {
        int key = colour.GetHashCode() * 2 + (onTop ? 1 : 0);
        if (materials.TryGetValue(key, out Material cached) && cached != null) return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material material = new Material(shader) { hideFlags = HideFlags.DontSave };

        // Transparent, unlit, double-sided, no depth writes.
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        // A beacon is useless if a shelf hides it, so it draws over the world where the
        // pipeline lets us say so. Floor rings keep normal depth testing.
        if (onTop && material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)CompareFunction.Always);

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
        material.color = colour;
        material.renderQueue = (int)RenderQueue.Transparent + (onTop ? 50 : 10);

        materials[key] = material;
        return material;
    }

    // --- shared meshes ------------------------------------------------------

    static readonly Dictionary<int, Mesh> ringMeshes = new Dictionary<int, Mesh>();
    static Mesh arrowMesh;

    // Unit-radius ring in the XZ plane. Scale the transform to size it.
    static Mesh RingMesh(float innerRatio)
    {
        int key = Mathf.RoundToInt(Mathf.Clamp01(innerRatio) * 100f);
        if (ringMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        const int segments = 48;
        float inner = key / 100f;

        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            vertices[i * 2] = direction * inner;
            vertices[i * 2 + 1] = direction;

            int next = (i + 1) % segments;
            int t = i * 6;
            triangles[t] = i * 2;
            triangles[t + 1] = i * 2 + 1;
            triangles[t + 2] = next * 2 + 1;
            triangles[t + 3] = i * 2;
            triangles[t + 4] = next * 2 + 1;
            triangles[t + 5] = next * 2;
        }

        Mesh mesh = new Mesh { name = $"GuideRing_{key}", hideFlags = HideFlags.DontSave };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        ringMeshes[key] = mesh;
        return mesh;
    }

    // A four-sided pyramid with its point at the origin, opening upwards.
    static Mesh ArrowMesh()
    {
        if (arrowMesh != null) return arrowMesh;

        Vector3[] vertices =
        {
            new Vector3(0f, 0f, 0f),        // tip, pointing down at the target
            new Vector3(-0.5f, 1f, -0.5f),
            new Vector3(0.5f, 1f, -0.5f),
            new Vector3(0.5f, 1f, 0.5f),
            new Vector3(-0.5f, 1f, 0.5f)
        };

        int[] triangles =
        {
            0, 2, 1,  0, 3, 2,  0, 4, 3,  0, 1, 4,   // sides
            1, 2, 3,  1, 3, 4                        // cap
        };

        arrowMesh = new Mesh { name = "GuideArrow", hideFlags = HideFlags.DontSave };
        arrowMesh.vertices = vertices;
        arrowMesh.triangles = triangles;
        arrowMesh.RecalculateNormals();
        arrowMesh.RecalculateBounds();
        return arrowMesh;
    }
}

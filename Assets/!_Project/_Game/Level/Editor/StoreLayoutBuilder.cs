using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Authoring front end for StoreLayout, which is where the plan itself lives.
//
// The game stocks the shop at load, so neither of these is needed to play. They are here
// for working on the layout: "Report" prints what the plan makes of the scene as it stands,
// and "Apply" bakes it in so the Inspector shows each bay's section and each facing's
// product instead of the placeholder cereal they were built with.
//
// Baking is optional and costs something — every facing becomes a prefab override in a
// scene file that is already a megabyte — so reach for the report first.
public static class StoreLayoutBuilder
{
    [MenuItem("Kehai/Store/Report Layout")]
    public static void ReportLayout()
    {
        Debug.Log(StoreLayout.Describe());
    }

    [MenuItem("Kehai/Store/Apply Layout")]
    public static void ApplyLayout()
    {
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply store layout");

        int facings = StoreLayout.ApplyToScene(
            beforeWrite: o => Undo.RecordObject(o, "Apply store layout"),
            afterWrite: o =>
            {
                // Bays are prefab instances, so a changed field only sticks once it has
                // been registered as an override.
                if (PrefabUtility.IsPartOfPrefabInstance(o))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(o);
                else
                    EditorUtility.SetDirty(o);
            });

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Baked the store layout into the scene: {facings} facings.\n\n" +
                  StoreLayout.Describe());
    }
}

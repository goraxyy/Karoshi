using UnityEngine;

namespace Kehai
{
    public static class ComponentExtensions
    {
        // GetComponent<T>() ?? AddComponent<T>() looks right and isn't: in the editor a
        // missing component comes back as a "fake null" object that compares equal to null
        // but slips straight past ??, so nothing is ever added. Unity's == does see it.
        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}

using UnityEngine;

// Shared pool of positional AudioSources.
// Shelf slots number in the thousands, so giving each one its own AudioSource
// (as ShelfSlot used to) wastes memory and component overhead for a sound that
// only ever plays on interaction. A handful of reusable voices covers it.
public static class OneShotAudio
{
    const int PoolSize = 8;

    static AudioSource[] pool;
    static int next;

    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        EnsurePool();

        AudioSource source = pool[next];
        next = (next + 1) % pool.Length;

        source.transform.position = position;
        source.PlayOneShot(clip, volume);
    }

    static void EnsurePool()
    {
        // pool[0] is also null-checked: statics survive a scene load, the objects don't.
        if (pool != null && pool[0] != null) return;

        GameObject root = new GameObject("~OneShotAudio");
        Object.DontDestroyOnLoad(root);

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject voice = new GameObject("Voice" + i);
            voice.transform.SetParent(root.transform);

            AudioSource source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            pool[i] = source;
        }
    }
}

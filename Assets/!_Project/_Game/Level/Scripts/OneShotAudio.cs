using UnityEngine;

// Shared pool of positional AudioSources.
// Shelf slots number in the thousands, so giving each one its own AudioSource
// (as ShelfSlot used to) wastes memory and component overhead for a sound that
// only ever plays on interaction. A handful of reusable voices covers it.
public static class OneShotAudio
{
    const int PoolSize = 16;

    static AudioSource[] pool;
    static int next;

    // An ordinary sound in the world: full volume within a couple of metres, fading
    // linearly to nothing at 30. (Unity's default — logarithmic from 1 m — made anything
    // more than a few metres away all but silent.)
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, SoundKind kind = SoundKind.Effects) =>
        PlayAt(clip, position, volume, 2f, 30f, 1f, kind);

    // Full control: `near`/`far` in metres, `spatial` 0 = everywhere at once, 1 = fully 3D.
    // `kind` is which volume slider in the Esc menu it answers to.
    public static void PlayAt(AudioClip clip, Vector3 position, float volume, float near, float far, float spatial, SoundKind kind = SoundKind.Effects)
    {
        if (clip == null) return;

        EnsurePool();

        AudioSource source = pool[next];
        next = (next + 1) % pool.Length;

        source.transform.position = position;
        source.minDistance = near;
        source.maxDistance = Mathf.Max(near + 0.1f, far);
        source.spatialBlend = Mathf.Clamp01(spatial);
        source.PlayOneShot(clip, volume * SoundSettings.Get(kind));
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
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            pool[i] = source;
        }
    }
}

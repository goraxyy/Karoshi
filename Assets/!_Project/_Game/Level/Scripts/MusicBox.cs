using UnityEngine;

// The store radio. Ceiling speakers are scattered across the whole mall; this box
// switches the lot on and off with E and owns their mix.
//
// Two problems come with driving dozens of copies of one track at once, and both are
// handled here:
//
//   * Voices. Unity only keeps a few dozen real voices; sixty-odd sources fighting over
//     them get virtualised and come back at the wrong place in the song, which is what
//     makes a distant speaker sound like an echo. So only the speakers near the listener
//     are left playing at all, and the rest are stopped outright.
//
//   * Drift. Whenever a speaker is started it is seeked to where the song *should* be,
//     measured from one shared DSP start time rather than from when that speaker
//     happened to begin. Walk away and back and it picks up in step.
public class MusicBox : HighlightInteractable
{
    [Header("Track")]
    public AudioClip track;

    [Tooltip("Start the shift with the radio already on.")]
    public bool playOnStart;

    [Header("Speakers")]
    [Tooltip("Every AudioSource underneath this transform plays the track. " +
             "Leave empty to use this object's own children.")]
    public Transform speakerRoot;

    [Header("Mix")]
    [Range(0f, 1f)]
    [Tooltip("Volume of every ceiling speaker. Safe to drag while playing.")]
    public float volume = 0.25f;

    [Min(0.1f)]
    [Tooltip("Right under a speaker it is at full volume out to this radius.")]
    public float fullVolumeRadius = 1.5f;

    [Min(1f)]
    [Tooltip("Distance at which a speaker has faded to silence.")]
    public float audibleRange = 11f;

    [Range(0f, 180f)]
    [Tooltip("How wide the sound sits in the stereo field. Higher feels like a room " +
             "full of sound rather than a dot in the ceiling.")]
    public float spread = 80f;

    [Range(0f, 1.1f)]
    [Tooltip("How much of the track goes through the scene's reverb zones.")]
    public float reverbMix = 1f;

    [Header("Sync")]
    [Tooltip("How often speakers are culled and nudged back into step.")]
    public float syncInterval = 0.25f;

    [Tooltip("Resync a speaker once it is this far out, in seconds.")]
    public float driftTolerance = 0.05f;

    public bool IsPlaying { get; private set; }
    public bool HasPower { get; private set; } = true;

    AudioSource[] speakers;
    Transform ear;
    AnimationCurve rolloff;

    double startDsp;
    int playhead;              // where the track was when it was last switched off
    float nextSyncTime;

    float appliedVolume = -1f;
    float appliedRange = -1f;
    float appliedSpread = -1f;

    protected override void Awake()
    {
        base.Awake();

        CollectSpeakers();
        ApplyMix();

        if (playOnStart) SetPlaying(true);
    }

    public override void Interact(PlayerInteract player)
    {
        if (!HasPower) return;
        SetPlaying(!IsPlaying);
    }

    public override string GetPrompt()
    {
        if (!HasPower) return "No power";
        return IsPlaying ? "Turn the music off" : "Turn the music on";
    }

    void Update()
    {
        // Dragging a slider in the Inspector should be audible straight away, but
        // writing to 60-odd AudioSources every frame is pointless — only push on change.
        if (!Mathf.Approximately(volume, appliedVolume)
            || !Mathf.Approximately(audibleRange, appliedRange)
            || !Mathf.Approximately(spread, appliedSpread))
            ApplyMix();

        if (!IsPlaying) return;
        if (Time.unscaledTime < nextSyncTime) return;

        nextSyncTime = Time.unscaledTime + Mathf.Max(0.05f, syncInterval);
        SyncSpeakers();
    }

    // --- power ---------------------------------------------------------------

    // Called by PowerSystem. Losing the mains kills the music and locks the switch.
    public void SetPowered(bool powered)
    {
        HasPower = powered;
        if (powered) return;

        if (IsPlaying) SetPlaying(false);

        // Switching the radio off with E pauses it; pulling the mains wipes it, so the
        // track starts from the top once the power is back.
        playhead = 0;
    }

    // --- transport -----------------------------------------------------------

    public void SetPlaying(bool on)
    {
        if (speakers == null || speakers.Length == 0) CollectSpeakers();

        if (!on)
        {
            // Remember the spot so E picks the song up again rather than restarting it.
            if (IsPlaying && track != null) playhead = MasterSample();

            IsPlaying = false;
            StopAll();
            return;
        }

        if (track == null)
        {
            Debug.LogWarning("MusicBox has no track assigned.", this);
            return;
        }

        if (speakers.Length == 0)
        {
            Debug.LogWarning("MusicBox has no speakers to drive.", this);
            return;
        }

        IsPlaying = true;

        // One clock for the whole store. Every speaker is positioned against this, now
        // and every time one is brought back in later. Winding it back by the playhead
        // is what makes the track resume where it left off instead of restarting.
        startDsp = AudioSettings.dspTime - (double)playhead / track.frequency;
        nextSyncTime = 0f;

        SyncSpeakers();
    }

    void StopAll()
    {
        if (speakers == null) return;

        for (int i = 0; i < speakers.Length; i++)
            if (speakers[i] != null) speakers[i].Stop();
    }

    // Starts the speakers near the listener at the right place in the song, stops the
    // rest so they aren't competing for voices, and pulls back anything that slipped.
    void SyncSpeakers()
    {
        if (track == null) return;
        if (ear == null) ear = FindEar();
        if (ear == null) return;

        Vector3 here = ear.position;
        int master = MasterSample();
        int tolerance = Mathf.Max(1, Mathf.RoundToInt(driftTolerance * track.frequency));

        // A little past the audible range, so a speaker is already running by the time
        // it can be heard rather than snapping in at the edge.
        float cull = audibleRange * 1.25f;
        float cullSqr = cull * cull;

        for (int i = 0; i < speakers.Length; i++)
        {
            AudioSource source = speakers[i];
            if (source == null) continue;

            if ((source.transform.position - here).sqrMagnitude > cullSqr)
            {
                if (source.isPlaying) source.Stop();
                continue;
            }

            if (!source.isPlaying)
            {
                source.clip = track;
                source.timeSamples = master;
                source.Play();
            }
            else if (Mathf.Abs(source.timeSamples - master) > tolerance)
            {
                source.timeSamples = master;
            }
        }
    }

    // Where the song should be right now, measured from the shared start.
    int MasterSample()
    {
        int total = track.samples;
        if (total <= 0) return 0;

        double elapsed = AudioSettings.dspTime - startDsp;
        long position = (long)(elapsed * track.frequency);
        return (int)(((position % total) + total) % total);
    }

    // --- setup ---------------------------------------------------------------

    void CollectSpeakers()
    {
        Transform root = speakerRoot != null ? speakerRoot : transform;

        // The box itself isn't a speaker, so skip any source sitting on this object.
        AudioSource[] found = root.GetComponentsInChildren<AudioSource>(true);
        int keep = 0;
        for (int i = 0; i < found.Length; i++)
            if (found[i].gameObject != gameObject) found[keep++] = found[i];

        speakers = new AudioSource[keep];
        System.Array.Copy(found, speakers, keep);
    }

    void ApplyMix()
    {
        appliedVolume = volume;
        appliedRange = audibleRange;
        appliedSpread = spread;

        rolloff = BuildRolloff();

        if (speakers == null) return;

        for (int i = 0; i < speakers.Length; i++)
        {
            AudioSource source = speakers[i];
            if (source == null) continue;

            source.volume = volume;
            source.spatialBlend = 1f;              // fully positional, or it never fades
            source.spread = spread;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = reverbMix;
            source.minDistance = Mathf.Min(fullVolumeRadius, audibleRange * 0.5f);
            source.maxDistance = audibleRange;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
            source.loop = true;
            source.playOnAwake = false;
            if (source.clip == null) source.clip = track;
        }
    }

    // Smooth like a logarithmic falloff, but it actually reaches zero at maxDistance
    // instead of trailing off forever — which is what stops sixty speakers stacking up
    // into a wall of sound no matter where you stand.
    AnimationCurve BuildRolloff()
    {
        float near = Mathf.Clamp01(Mathf.Min(fullVolumeRadius, audibleRange * 0.5f) / Mathf.Max(0.001f, audibleRange));

        var curve = new AnimationCurve();
        curve.AddKey(new Keyframe(0f, 1f, 0f, 0f));
        curve.AddKey(new Keyframe(near, 1f, 0f, 0f));
        curve.AddKey(new Keyframe(Mathf.Lerp(near, 1f, 0.35f), 0.45f));
        curve.AddKey(new Keyframe(Mathf.Lerp(near, 1f, 0.7f), 0.12f));
        curve.AddKey(new Keyframe(1f, 0f, 0f, 0f));
        return curve;
    }

    static Transform FindEar()
    {
        var listener = FindAnyObjectByType<AudioListener>();
        return listener != null ? listener.transform : null;
    }

#if UNITY_EDITOR
    // Retuning the mix from the Inspector, in play mode or out of it.
    void OnValidate()
    {
        if (speakers == null || speakers.Length == 0) CollectSpeakers();
        ApplyMix();
    }
#endif
}

using UnityEngine;

// A torch you can carry. The beam lives on a child at the lens end, so it points
// wherever the body points — held, that's wherever you are looking; dropped, it lies
// on the floor still shining across it.
[RequireComponent(typeof(Item))]
public class Flashlight : MonoBehaviour
{
    [Header("Beam")]
    [Tooltip("The spot light at the lens. Found in the children if left empty.")]
    public Light beam;

    [Tooltip("Starts switched on.")]
    public bool isOn;

    public AudioClip clickSound;

    Item item;

    public bool IsOn => isOn;

    void Awake()
    {
        item = GetComponent<Item>();
        if (beam == null) beam = GetComponentInChildren<Light>(true);

        ApplyBeam();
    }

    // Switched with E while it is the thing in your hand and nothing else is under the
    // crosshair — PlayerInteract makes that call, so E stays a single verb. A torch left
    // on the floor keeps whatever state you dropped it in.

    public void Toggle() => SetOn(!isOn);

    public void SetOn(bool on)
    {
        if (isOn == on) return;

        isOn = on;
        ApplyBeam();
        OneShotAudio.PlayAt(clickSound, transform.position, 0.6f);
    }

    void ApplyBeam()
    {
        if (beam != null) beam.enabled = isOn;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (beam == null) beam = GetComponentInChildren<Light>(true);
        if (beam != null) beam.enabled = isOn;
    }
#endif
}

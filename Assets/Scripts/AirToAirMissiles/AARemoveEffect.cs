using UnityEngine;
using UnityEngine.VFX;

[DisallowMultipleComponent]
public class AARemoveEffect : MonoBehaviour
{
    VisualEffect[] visualEffects;
    public bool readyToDestroy = false;

    float effectStartTime = 0.0f;

    void OnEnable()
    {
        visualEffects = GetComponentsInChildren<VisualEffect>();
        effectStartTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {
        bool allVfxCountZero = true;
        foreach (VisualEffect vfx in visualEffects)
        {
            if (vfx != null  && vfx.aliveParticleCount > 0)
            {
                allVfxCountZero = false;
                break;
            }
        }

        // Only work this if the effect has been alive for longer than a second. Prevents effects from
        // destroying themselves before they can even start.
        if (readyToDestroy && allVfxCountZero && Time.time - effectStartTime > 1.0f)
            Destroy(gameObject);
    }
}

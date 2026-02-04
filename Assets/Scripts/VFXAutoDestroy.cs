using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Automatically destroys a GameObject with VisualEffect after all particles have finished playing.
/// </summary>
public class VFXAutoDestroy : MonoBehaviour
{
    private VisualEffect[] _visualEffects;
    private float _startTime;
    private float _minLifetime = 1f; // Minimum time before checking for destruction

    void Start()
    {
        _visualEffects = GetComponentsInChildren<VisualEffect>();
        _startTime = Time.time;
    }

    void Update()
    {
        // Don't check until minimum lifetime has passed
        if (Time.time - _startTime < _minLifetime)
            return;

        // Check if all VFX have finished
        bool allFinished = true;
        foreach (VisualEffect vfx in _visualEffects)
        {
            if (vfx != null && vfx.aliveParticleCount > 0)
            {
                allFinished = false;
                break;
            }
        }

        // Destroy when all particles are done
        if (allFinished)
        {
            Destroy(gameObject);
        }
    }
}

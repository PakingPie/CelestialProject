using UnityEngine;

/// <summary>
/// Third-person follow camera for observing a boid vehicle.
/// Attach to the main camera and drag an active boid into the Target field.
/// </summary>
public class BoidFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag an active Boid here to follow it.")]
    public Boid Target;

    [Tooltip("The BoidsManager to cycle through. If not set, finds one automatically.")]
    public BoidsManager BoidManager;

    [HideInInspector] public int CurrentBoidIndex = -1;

    [Header("Follow Settings")]
    [Tooltip("Distance behind the boid.")]
    public float FollowDistance = 50f;

    [Tooltip("Height above the boid.")]
    public float HeightOffset = 20f;

    [Tooltip("How quickly the camera catches up to the desired position.")]
    [Range(0.5f, 20f)]
    public float SmoothSpeed = 5f;

    [Tooltip("How quickly the camera rotates to look at the boid.")]
    [Range(0.5f, 20f)]
    public float RotationSmoothSpeed = 5f;

    [Header("Look Ahead")]
    [Tooltip("How far ahead of the boid the camera looks (based on velocity).")]
    public float LookAheadFactor = 0.5f;

    private Vector3 _currentVelocity;

    public void SwitchToNextBoid()
    {
        if (!EnsureManager()) return;

        CurrentBoidIndex = (CurrentBoidIndex + 1) % BoidManager.BoidCount;
        SkipNullBoids(1);
    }

    public void SwitchToPreviousBoid()
    {
        if (!EnsureManager()) return;

        CurrentBoidIndex = (CurrentBoidIndex - 1 + BoidManager.BoidCount) % BoidManager.BoidCount;
        SkipNullBoids(-1);
    }

    public void SwitchToBoid(int index)
    {
        if (!EnsureManager()) return;

        if (index < 0 || index >= BoidManager.BoidCount) return;

        CurrentBoidIndex = index;
        if (BoidManager.Boids[CurrentBoidIndex] != null)
            Target = BoidManager.Boids[CurrentBoidIndex];
    }

    private bool EnsureManager()
    {
        if (BoidManager == null)
            BoidManager = FindFirstObjectByType<BoidsManager>();

        return BoidManager != null && BoidManager.BoidCount > 0;
    }

    private void SkipNullBoids(int direction)
    {
        int attempts = BoidManager.BoidCount;
        while (attempts > 0 && BoidManager.Boids[CurrentBoidIndex] == null)
        {
            CurrentBoidIndex = (CurrentBoidIndex + direction + BoidManager.BoidCount) % BoidManager.BoidCount;
            attempts--;
        }

        if (BoidManager.Boids[CurrentBoidIndex] != null)
            Target = BoidManager.Boids[CurrentBoidIndex];
    }

    private void LateUpdate()
    {
        if (Target == null)
            return;

        Vector3 boidPosition = Target.position;
        Vector3 boidForward = Target.forward;

        // Desired position: behind and above the boid
        Vector3 desiredPosition = boidPosition
            - boidForward * FollowDistance
            + Vector3.up * HeightOffset;

        // Smoothly move towards the desired position
        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref _currentVelocity, 1f / SmoothSpeed);

        // Look at the boid with a slight look-ahead based on velocity
        Vector3 lookTarget = boidPosition + Target.Velocity * LookAheadFactor;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRotation, RotationSmoothSpeed * Time.deltaTime);
    }
}

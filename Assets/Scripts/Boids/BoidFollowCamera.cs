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

    [Tooltip("The BoidsManager to cycle through. Auto-assigned if not set. Press M to cycle managers.")]
    public BoidsManager BoidManager;

    [HideInInspector] public int CurrentBoidIndex = -1;
    private BoidsManager[] _allManagers;
    private int _currentManagerIndex = -1;

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

    [Header("Camera Rotation")]
    [Tooltip("Mouse sensitivity for horizontal orbit.")]
    public float HorizontalSensitivity = 3f;

    [Tooltip("Mouse sensitivity for vertical orbit.")]
    public float VerticalSensitivity = 2f;

    [Tooltip("Minimum vertical angle (degrees). Negative = look from below.")]
    public float MinVerticalAngle = -30f;

    [Tooltip("Maximum vertical angle (degrees). Positive = look from above.")]
    public float MaxVerticalAngle = 80f;

    [Tooltip("If true, camera only orbits while holding right mouse button.")]
    public bool RequireRightClick = true;

    [Tooltip("How quickly the orbit resets to behind the boid when not rotating. Set 0 to disable reset.")]
    [Range(0f, 10f)]
    public float OrbitResetSpeed = 2f;

    [Header("Look Ahead")]
    [Tooltip("How far ahead of the boid the camera looks (based on velocity).")]
    public float LookAheadFactor = 0.5f;

    private Vector3 _currentVelocity;
    private float _orbitYaw;
    private float _orbitPitch;
    private bool _orbitInitialized;
    private bool _isUserRotating;

    private void Start()
    {
        RefreshManagers();
    }

    public void RefreshManagers()
    {
        _allManagers = FindObjectsByType<BoidsManager>(FindObjectsSortMode.None);
        if (_allManagers.Length > 0 && BoidManager == null)
        {
            _currentManagerIndex = 0;
            SetManager(_allManagers[0]);
        }
        else if (BoidManager != null)
        {
            _currentManagerIndex = System.Array.IndexOf(_allManagers, BoidManager);
        }
    }

    public void SwitchToNextManager()
    {
        RefreshManagers();
        if (_allManagers == null || _allManagers.Length == 0) return;

        _currentManagerIndex = (_currentManagerIndex + 1) % _allManagers.Length;
        SetManager(_allManagers[_currentManagerIndex]);
    }

    public void SwitchToPreviousManager()
    {
        RefreshManagers();
        if (_allManagers == null || _allManagers.Length == 0) return;

        _currentManagerIndex = (_currentManagerIndex - 1 + _allManagers.Length) % _allManagers.Length;
        SetManager(_allManagers[_currentManagerIndex]);
    }

    private void SetManager(BoidsManager manager)
    {
        BoidManager = manager;
        CurrentBoidIndex = -1;
        Target = null;
        _orbitInitialized = false;
        if (BoidManager != null && BoidManager.BoidCount > 0)
            SwitchToNextBoid();
    }

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
            RefreshManagers();

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

        // Initialize orbit angles to match the boid's current heading
        if (!_orbitInitialized)
        {
            _orbitYaw = Mathf.Atan2(boidForward.x, boidForward.z) * Mathf.Rad2Deg;
            _orbitPitch = HeightOffset > 0 ? Mathf.Atan2(HeightOffset, FollowDistance) * Mathf.Rad2Deg : 10f;
            _orbitInitialized = true;
        }

        // Handle orbit input
        bool wantsRotate = !RequireRightClick || Input.GetMouseButton(1);
        _isUserRotating = wantsRotate && (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.01f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.01f);

        if (wantsRotate)
        {
            _orbitYaw += Input.GetAxis("Mouse X") * HorizontalSensitivity;
            _orbitPitch -= Input.GetAxis("Mouse Y") * VerticalSensitivity;
            _orbitPitch = Mathf.Clamp(_orbitPitch, MinVerticalAngle, MaxVerticalAngle);
        }
        else if (OrbitResetSpeed > 0f)
        {
            // Smoothly reset orbit behind the boid when not rotating
            float targetYaw = Mathf.Atan2(boidForward.x, boidForward.z) * Mathf.Rad2Deg;
            float targetPitch = HeightOffset > 0 ? Mathf.Atan2(HeightOffset, FollowDistance) * Mathf.Rad2Deg : 10f;
            _orbitYaw = Mathf.LerpAngle(_orbitYaw, targetYaw, OrbitResetSpeed * Time.deltaTime);
            _orbitPitch = Mathf.Lerp(_orbitPitch, targetPitch, OrbitResetSpeed * Time.deltaTime);
        }

        // Compute desired position from orbit angles
        Quaternion orbitRotation = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
        Vector3 offset = orbitRotation * Vector3.back * FollowDistance;
        Vector3 desiredPosition = boidPosition + offset;

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

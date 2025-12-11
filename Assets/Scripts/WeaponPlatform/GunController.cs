using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class GunController : MonoBehaviour
{
    public enum FiringMode
    {
        Automatic,  // AI controlled, fires at Targeted
        Manual      // Player controlled, fires at mouse position
    }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform ship;

    [Header("Gun Groups")]
    [Tooltip("Primary weapons - Fire with Left Mouse Button")]
    [SerializeField] private List<Gun> primaryGuns = new List<Gun>();

    [Tooltip("Secondary weapons - Fire with Right Mouse Button")]
    [SerializeField] private List<Gun> secondaryGuns = new List<Gun>();

    [Tooltip("Tertiary weapons - Fire with Middle Mouse Button")]
    [SerializeField] private List<Gun> tertiaryGuns = new List<Gun>();

    [Header("Firing Settings")]
    [SerializeField] private FiringMode firingMode = FiringMode.Manual;
    [SerializeField] private float aimDistance = 500f;
    [SerializeField] private LayerMask aimLayerMask = ~0; // Everything by default

    [Header("Input Keys (for keyboard control)")]
    [SerializeField] private Key primaryFireKey = Key.None;
    [SerializeField] private Key secondaryFireKey = Key.None;
    [SerializeField] private Key tertiaryFireKey = Key.None;
    [SerializeField] private Key toggleModeKey = Key.T;

    // Current aim position in world space
    public Vector3 AimWorldPosition { get; private set; }
    public bool IsManualMode => firingMode == FiringMode.Manual;

    private bool isPrimaryFiring = false;
    private bool isSecondaryFiring = false;
    private bool isTertiaryFiring = false;

    public List<Gun> PrimaryGuns => primaryGuns;
    public List<Gun> SecondaryGuns => secondaryGuns;
    public List<Gun> TertiaryGuns => tertiaryGuns;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (ship == null)
            ship = transform;
    }

    private void Update()
    {
        HandleModeToggle();
        UpdateAimPosition();
        HandleInput();
        UpdateGuns();
    }

    private void HandleModeToggle()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleModeKey].wasPressedThisFrame)
        {
            firingMode = firingMode == FiringMode.Automatic ? FiringMode.Manual : FiringMode.Automatic;
            Debug.Log($"Firing Mode: {firingMode}");
        }
    }

    private void UpdateAimPosition()
    {
        if (firingMode == FiringMode.Manual)
        {
            AimWorldPosition = GetMouseWorldPosition();
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null || Mouse.current == null)
            return ship.position + ship.forward * aimDistance;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);

        // Simply return a point along the ray at aim distance
        // No raycast needed - just aim where the mouse is pointing
        return ray.GetPoint(aimDistance);
    }

    private void HandleInput()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;

        if (mouse != null)
        {
            // Mouse button input
            isPrimaryFiring = mouse.leftButton.isPressed;
            isSecondaryFiring = mouse.leftButton.isPressed;
            isTertiaryFiring = mouse.middleButton.isPressed;
        }

        // Keyboard override (if keys are assigned)
        if (kb != null)
        {
            if (primaryFireKey != Key.None)
                isPrimaryFiring |= kb[primaryFireKey].isPressed;

            if (secondaryFireKey != Key.None)
                isSecondaryFiring |= kb[secondaryFireKey].isPressed;

            if (tertiaryFireKey != Key.None)
                isTertiaryFiring |= kb[tertiaryFireKey].isPressed;
        }
    }

    private void UpdateGuns()
    {
        if (firingMode == FiringMode.Manual)
        {
            // Always update aim position for all guns, pass firing state separately
            UpdateGunGroup(primaryGuns, isPrimaryFiring, AimWorldPosition);
            UpdateGunGroup(secondaryGuns, isSecondaryFiring, AimWorldPosition);
            UpdateGunGroup(tertiaryGuns, isTertiaryFiring, AimWorldPosition);
        }
        else
        {
            // Automatic mode
            foreach (var gun in primaryGuns)
                gun.SetAutomaticMode();
            foreach (var gun in secondaryGuns)
                gun.SetAutomaticMode();
            foreach (var gun in tertiaryGuns)
                gun.SetAutomaticMode();
        }
    }

    private void UpdateGunGroup(List<Gun> guns, bool isFiring, Vector3 aimPosition)
    {
        foreach (var gun in guns)
        {
            if (gun == null) continue;
            // Always pass aim position, regardless of firing state
            gun.SetManualFiring(isFiring, aimPosition);
        }
    }

    /// <summary>
    /// Add a gun to a specific group at runtime
    /// </summary>
    public void AddGunToGroup(Gun gun, int groupIndex)
    {
        switch (groupIndex)
        {
            case 0: primaryGuns.Add(gun); break;
            case 1: secondaryGuns.Add(gun); break;
            case 2: tertiaryGuns.Add(gun); break;
        }
    }

    /// <summary>
    /// Set firing mode for all guns
    /// </summary>
    public void SetFiringMode(FiringMode mode)
    {
        firingMode = mode;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Draw aim position
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AimWorldPosition, 2f);

        // Draw line from ship to aim
        if (ship != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(ship.position, AimWorldPosition);
        }
    }
}
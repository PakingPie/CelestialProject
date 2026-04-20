using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using static GlobalHelper;

public class PlayerGunController : MonoBehaviour
{
    public enum FiringMode
    {
        Automatic,  // AI controlled, fires at Targeted
        Manual      // Player controlled, fires at mouse position
    }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform ship;

    [Header("Weapon Groups")]
    [Tooltip("Primary weapons - Fire with Left Mouse Button")]
    [SerializeField] private List<WeaponBase> primaryWeapons = new List<WeaponBase>();

    [Tooltip("Secondary weapons - Also fires with Left Mouse Button")]
    [SerializeField] private List<WeaponBase> secondaryWeapons = new List<WeaponBase>();

    [Tooltip("Tertiary weapons - Fire with Middle Mouse Button")]
    [SerializeField] private List<WeaponBase> tertiaryWeapons = new List<WeaponBase>();

    [Header("Firing Settings")]
    [SerializeField] private FiringMode firingMode = FiringMode.Manual;
    [SerializeField] private float aimDistance = 5000f;

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

    public List<WeaponBase> PrimaryWeapons => primaryWeapons;
    public List<WeaponBase> SecondaryWeapons => secondaryWeapons;
    public List<WeaponBase> TertiaryWeapons => tertiaryWeapons;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (ship == null)
            ship = transform;
    }

    void Start()
    {
        InitializeWeaponGroups();
    }

    public void InitializeWeaponGroups()
    {
        primaryWeapons.Clear();
        secondaryWeapons.Clear();
        tertiaryWeapons.Clear();

        WeaponBase[] allWeapons = GetComponentsInChildren<WeaponBase>();

        foreach (var weapon in allWeapons)
        {
            // Only include guns in weapon groups; missiles use separate lock-on
            if (weapon.WeaponCategory != WeaponType.Gun)
                continue;

            switch (weapon.WeaponSizeClass)
            {
                case WeaponSize.Large:
                    primaryWeapons.Add(weapon);
                    break;
                case WeaponSize.Medium:
                    secondaryWeapons.Add(weapon);
                    break;
                case WeaponSize.Small:
                    tertiaryWeapons.Add(weapon);
                    break;
            }
        }
    }

    private void Update()
    {
        HandleModeToggle();
        UpdateAimPosition();
        HandleInput();
        UpdateWeapons();
    }

    private void HandleModeToggle()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleModeKey].wasPressedThisFrame)
        {
            firingMode = firingMode == FiringMode.Automatic ? FiringMode.Manual : FiringMode.Automatic;
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

        return ray.GetPoint(aimDistance);
    }

    private void HandleInput()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;

        if (mouse != null)
        {
            isPrimaryFiring = mouse.leftButton.isPressed;
            isSecondaryFiring = mouse.leftButton.isPressed;
            isTertiaryFiring = mouse.middleButton.isPressed;
        }

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

    private void UpdateWeapons()
    {
        if (firingMode == FiringMode.Manual)
        {
            UpdateWeaponGroup(primaryWeapons, isPrimaryFiring, AimWorldPosition);
            UpdateWeaponGroup(secondaryWeapons, isSecondaryFiring, AimWorldPosition);
            UpdateWeaponGroup(tertiaryWeapons, isTertiaryFiring, AimWorldPosition);
        }
        else
        {
            SetGroupAutomatic(primaryWeapons);
            SetGroupAutomatic(secondaryWeapons);
            SetGroupAutomatic(tertiaryWeapons);
        }
    }

    private void UpdateWeaponGroup(List<WeaponBase> weapons, bool isFiring, Vector3 aimPosition)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponBase weapon = weapons[i];
            if (weapon == null) continue;

            if (weapon is Gun gun)
            {
                gun.SetManualFiring(isFiring, aimPosition);
            }
            else if (weapon is MissileSalvo salvo)
            {
                salvo.SetManualFiring(isFiring, aimPosition);
            }
        }
    }

    private void SetGroupAutomatic(List<WeaponBase> weapons)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponBase weapon = weapons[i];
            if (weapon == null) continue;

            if (weapon is Gun gun)
            {
                gun.SetAutomaticMode();
            }
            else if (weapon is MissileSalvo salvo)
            {
                salvo.SetAutomaticMode();
            }
        }
    }

    /// <summary>
    /// Add a weapon to a specific group at runtime.
    /// </summary>
    public void AddWeaponToGroup(WeaponBase weapon, int groupIndex)
    {
        switch (groupIndex)
        {
            case 0: primaryWeapons.Add(weapon); break;
            case 1: secondaryWeapons.Add(weapon); break;
            case 2: tertiaryWeapons.Add(weapon); break;
        }
    }

    /// <summary>
    /// Set firing mode for all weapons.
    /// </summary>
    public void SetFiringMode(FiringMode mode)
    {
        firingMode = mode;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AimWorldPosition, 2f);

        if (ship != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(ship.position, AimWorldPosition);
        }
    }
}
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

    [Header("Missile Weapons")]
    [SerializeField] private List<MissileSalvo> missileWeapons = new List<MissileSalvo>();

    [Header("Firing Settings")]
    [SerializeField] private FiringMode firingMode = FiringMode.Manual;
    [SerializeField] private float aimDistance = 5000f;

    [Header("Missile Lock-On")]
    [Tooltip("Screen-space radius (pixels) for proximity target picking")]
    [SerializeField] private float lockOnScreenRadius = 300f;
    [Tooltip("Maximum lock-on range in world units")]
    [SerializeField] private float lockOnMaxRange = 3000f;

    [Header("Input Keys")]
    [SerializeField] private Key primaryFireKey = Key.None;
    [SerializeField] private Key secondaryFireKey = Key.None;
    [SerializeField] private Key missileLaunchKey = Key.Space;
    [SerializeField] private Key toggleModeKey = Key.T;

    // Current aim position in world space
    public Vector3 AimWorldPosition { get; private set; }
    public bool IsManualMode => firingMode == FiringMode.Manual;

    // Missile lock-on state
    public Transform LockedTarget { get; private set; }

    private bool isPrimaryFiring = false;
    private bool isSecondaryFiring = false;
    private bool isMissileFiring = false;

    public List<WeaponBase> PrimaryWeapons => primaryWeapons;
    public List<WeaponBase> SecondaryWeapons => secondaryWeapons;

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
        missileWeapons.Clear();

        WeaponBase[] allWeapons = GetComponentsInChildren<WeaponBase>();

        foreach (var weapon in allWeapons)
        {
            if (weapon is MissileSalvo salvo)
            {
                missileWeapons.Add(salvo);
                continue;
            }

            if (weapon.WeaponCategory != WeaponType.Gun)
                continue;

            switch (weapon.WeaponSizeClass)
            {
                case WeaponSize.Large:
                    primaryWeapons.Add(weapon);
                    break;
                case WeaponSize.Medium:
                case WeaponSize.Small:
                    secondaryWeapons.Add(weapon);
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
        UpdateMissileLockOn();
        UpdateMissileWeapons();
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

        isPrimaryFiring = false;
        isSecondaryFiring = false;
        isMissileFiring = false;

        if (mouse != null)
        {
            isPrimaryFiring = mouse.leftButton.isPressed;
            isSecondaryFiring = mouse.leftButton.isPressed;

            // Middle click: lock-on
            if (mouse.middleButton.wasPressedThisFrame)
            {
                TryLockOnTarget();

            }
        }

        if (kb != null)
        {
            if (primaryFireKey != Key.None)
                isPrimaryFiring |= kb[primaryFireKey].isPressed;

            if (secondaryFireKey != Key.None)
                isSecondaryFiring |= kb[secondaryFireKey].isPressed;

            if (missileLaunchKey != Key.None)
                isMissileFiring = kb[missileLaunchKey].isPressed;
        }
    }

    private void UpdateWeapons()
    {
        if (firingMode == FiringMode.Manual)
        {
            UpdateWeaponGroup(primaryWeapons, isPrimaryFiring, AimWorldPosition);
            UpdateWeaponGroup(secondaryWeapons, isSecondaryFiring, AimWorldPosition);
        }
        else
        {
            SetGroupAutomatic(primaryWeapons);
            SetGroupAutomatic(secondaryWeapons);
        }
    }

    // ── Missile lock-on system ──

    private void TryLockOnTarget()
    {
        Transform picked = PickTargetNearCursor();
        if (picked != null)
        {
            LockedTarget = picked;
        }
        else
        {
            // Click on empty space → clear lock
            LockedTarget = null;
        }
    }

    private Transform PickTargetNearCursor()
    {
        if (mainCamera == null || Mouse.current == null) return null;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 shipPos = ship.position;
        float maxRangeSqr = lockOnMaxRange * lockOnMaxRange;

        // Get all potential enemies from CombatRegistry
        List<VehicleBase> enemies = new List<VehicleBase>(32);
        CombatRegistry.GetNearbyEnemies(shipPos, lockOnMaxRange, Faction.Foe, enemies, false);

        Transform bestTarget = null;
        float bestScreenDist = lockOnScreenRadius;

        for (int i = 0; i < enemies.Count; i++)
        {
            VehicleBase enemy = enemies[i];
            if (enemy == null) continue;

            Transform enemyTransform = enemy.transform;

            // Range check
            float distSqr = (enemyTransform.position - shipPos).sqrMagnitude;
            if (distSqr > maxRangeSqr) continue;

            // Project to screen
            Vector3 screenPos = mainCamera.WorldToScreenPoint(enemyTransform.position);
            if (screenPos.z <= 0) continue; // Behind camera

            // Screen-space distance
            float screenDist = Vector2.Distance(mouseScreen, new Vector2(screenPos.x, screenPos.y));
            if (screenDist < bestScreenDist)
            {
                bestScreenDist = screenDist;
                bestTarget = enemyTransform;
            }
        }

        return bestTarget;
    }

    private void UpdateMissileLockOn()
    {
        // Clear lock if target is destroyed or out of range
        if (LockedTarget != null)
        {
            if (LockedTarget.gameObject == null || !LockedTarget.gameObject.activeInHierarchy)
            {
                LockedTarget = null;
                return;
            }

            float distSqr = (LockedTarget.position - ship.position).sqrMagnitude;
            if (distSqr > lockOnMaxRange * lockOnMaxRange)
            {
                LockedTarget = null;
            }
        }
    }

    private void UpdateMissileWeapons()
    {
        for (int i = 0; i < missileWeapons.Count; i++)
        {
            MissileSalvo salvo = missileWeapons[i];
            if (salvo == null) continue;

            if (LockedTarget != null)
            {
                // Override the salvo's target to the locked target
                salvo.Targeted = LockedTarget;
                salvo.SetManualFiring(isMissileFiring, LockedTarget.position);
            }
            else
            {
                // No lock — stay in manual mode but don't fire
                salvo.SetManualFiring(false, Vector3.zero);
            }
        }
    }

    // ── Gun group helpers ──

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
        }
    }

    /// <summary>
    /// Set firing mode for all weapons.
    /// </summary>
    public void SetFiringMode(FiringMode mode)
    {
        firingMode = mode;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || mainCamera == null) return;

        if (LockedTarget != null)
        {
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(LockedTarget.position);
            if (viewportPos.z > 0)
            {
                float guiX = viewportPos.x * Screen.width;
                float guiY = (1f - viewportPos.y) * Screen.height;
                float dist = Vector3.Distance(ship.position, LockedTarget.position);

                DrawLockOnReticle(guiX, guiY, Color.green, 24f);

                GUI.color = Color.green;
                GUI.Label(new Rect(guiX + 30f, guiY - 10f, 300f, 20f),
                    $"LOCKED: {LockedTarget.name}  [{dist:F0}m]");
            }
        }

        GUI.color = Color.white;
    }

    private static void DrawLockOnReticle(float x, float y, Color color, float size)
    {
        Color prev = GUI.color;
        GUI.color = color;

        // Crosshair
        float h = size;
        GUI.DrawTexture(new Rect(x - 1, y - h, 2, h * 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - h, y - 1, h * 2, 2), Texture2D.whiteTexture);

        // Corner brackets
        float b = size * 0.7f;
        float t = 2f;
        float arm = b * 0.4f;
        // Top-left
        GUI.DrawTexture(new Rect(x - b, y - b, arm, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - b, y - b, t, arm), Texture2D.whiteTexture);
        // Top-right
        GUI.DrawTexture(new Rect(x + b - arm, y - b, arm, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + b - t, y - b, t, arm), Texture2D.whiteTexture);
        // Bottom-left
        GUI.DrawTexture(new Rect(x - b, y + b - t, arm, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - b, y + b - arm, t, arm), Texture2D.whiteTexture);
        // Bottom-right
        GUI.DrawTexture(new Rect(x + b - arm, y + b - t, arm, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + b - t, y + b - arm, t, arm), Texture2D.whiteTexture);

        GUI.color = prev;
    }
}
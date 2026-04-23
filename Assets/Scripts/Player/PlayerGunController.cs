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

    [Header("Debug Off-Screen Indicators")]
    [Tooltip("Maximum range used when scanning for off-screen indicators")]
    [SerializeField] private float offscreenIndicatorRange = 3000f;
    [Tooltip("Screen edge margin used when placing off-screen indicators")]
    [SerializeField] private float offscreenIndicatorEdgeMargin = 36f;
    [Tooltip("Rendered triangle size for off-screen indicators")]
    [SerializeField] private float offscreenIndicatorSize = 28f;
    [Tooltip("Maximum number of off-screen indicators rendered per faction")]
    [SerializeField] private int maxOffscreenIndicatorsPerFaction = 8;
    [SerializeField] private Color offscreenEnemyColor = new Color(1f, 0.2f, 0.2f, 0.65f);
    [SerializeField] private Color offscreenAllyColor = new Color(0.2f, 1f, 1f, 0.6f);

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

    private readonly List<VehicleBase> offscreenEnemyBuffer = new List<VehicleBase>(32);
    private readonly List<VehicleBase> offscreenAllyBuffer = new List<VehicleBase>(32);

    private static Texture2D offscreenIndicatorTexture;

    public List<WeaponBase> PrimaryWeapons => primaryWeapons;
    public List<WeaponBase> SecondaryWeapons => secondaryWeapons;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (ship == null)
            ship = transform;

        EnsureOffscreenIndicatorTexture();
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

        DrawOffscreenIndicators(offscreenEnemyBuffer, Faction.Foe, offscreenEnemyColor);
        DrawOffscreenIndicators(offscreenAllyBuffer, Faction.Ally, offscreenAllyColor);

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

    private void DrawOffscreenIndicators(List<VehicleBase> buffer, Faction faction, Color indicatorColor)
    {
        float queryRange = offscreenIndicatorRange > 0f ? offscreenIndicatorRange : lockOnMaxRange;
        if (queryRange <= 0f || maxOffscreenIndicatorsPerFaction <= 0)
            return;

        CombatRegistry.GetNearbyEnemies(ship.position, queryRange, faction, buffer, false);
        if (buffer.Count == 0)
            return;

        float safeHalfWidth = Mathf.Max(0f, (Screen.width * 0.5f) - offscreenIndicatorEdgeMargin);
        float safeHalfHeight = Mathf.Max(0f, (Screen.height * 0.5f) - offscreenIndicatorEdgeMargin);
        if (safeHalfWidth <= 0f || safeHalfHeight <= 0f)
            return;

        int drawnCount = 0;

        for (int i = 0; i < buffer.Count; i++)
        {
            if (drawnCount >= maxOffscreenIndicatorsPerFaction)
                break;

            VehicleBase vehicle = buffer[i];
            if (vehicle == null)
                continue;

            Transform target = vehicle.transform;
            if (target == null || target == ship || !target.gameObject.activeInHierarchy)
                continue;

            Vector3 viewportPos = mainCamera.WorldToViewportPoint(target.position);
            if (IsTargetVisibleOnScreen(viewportPos))
                continue;

            Vector2 indicatorPosition;
            Vector2 direction;
            if (!TryGetOffscreenIndicatorPosition(viewportPos, safeHalfWidth, safeHalfHeight, out indicatorPosition, out direction))
                continue;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
            DrawRotatedTriangle(indicatorPosition, angle, offscreenIndicatorSize, indicatorColor);
            drawnCount++;
        }
    }

    private static bool IsTargetVisibleOnScreen(Vector3 viewportPos)
    {
        return viewportPos.z > 0f &&
               viewportPos.x >= 0f && viewportPos.x <= 1f &&
               viewportPos.y >= 0f && viewportPos.y <= 1f;
    }

    private static bool TryGetOffscreenIndicatorPosition(Vector3 viewportPos,
                                                         float safeHalfWidth,
                                                         float safeHalfHeight,
                                                         out Vector2 indicatorPosition,
                                                         out Vector2 direction)
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 screenPoint = new Vector2(viewportPos.x * Screen.width, (1f - viewportPos.y) * Screen.height);

        direction = screenPoint - screenCenter;

        if (viewportPos.z <= 0f)
        {
            direction = -direction;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            indicatorPosition = screenCenter;
            return false;
        }

        float scaleX = Mathf.Abs(direction.x) > 0.001f ? safeHalfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float scaleY = Mathf.Abs(direction.y) > 0.001f ? safeHalfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
        float scale = Mathf.Min(scaleX, scaleY);

        indicatorPosition = screenCenter + direction * scale;
        direction.Normalize();
        return true;
    }

    private static void EnsureOffscreenIndicatorTexture()
    {
        if (offscreenIndicatorTexture != null)
            return;

        const int textureSize = 32;
        offscreenIndicatorTexture = new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        int baseY = textureSize - 3;
        int tipY = 2;
        float centerX = (textureSize - 1) * 0.5f;

        for (int y = 0; y < textureSize; y++)
        {
            float t = Mathf.InverseLerp(baseY, tipY, y);
            float halfWidth = Mathf.Lerp((textureSize - 4) * 0.5f, 0.5f, t);

            for (int x = 0; x < textureSize; x++)
            {
                bool inside = y >= tipY && y <= baseY && Mathf.Abs(x - centerX) <= halfWidth;
                offscreenIndicatorTexture.SetPixel(x, y, inside ? fill : clear);
            }
        }

        offscreenIndicatorTexture.Apply(false, true);
    }

    private static void DrawRotatedTriangle(Vector2 center, float angle, float size, Color color)
    {
        EnsureOffscreenIndicatorTexture();

        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, center);
        GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), offscreenIndicatorTexture);

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
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
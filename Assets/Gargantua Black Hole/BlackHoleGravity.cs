using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Applies an inverse-square gravitational pull to nearby AAMissile and BulletPhysics objects.
/// Add this component to the same GameObject as your black hole shader/renderer.
///
/// AAMissile registers itself in ActivateMissile / DestroyMissile.
/// BulletPhysics registers itself via OnEnable / OnDisable (lifecycle-safe for pooled and non-pooled).
/// </summary>
public class BlackHoleGravity : MonoBehaviour
{
    [Header("Gravity Settings")]
    [Tooltip("Maximum distance (meters) at which the black hole exerts gravitational pull.")]
    public float InfluenceRadius = 500f;

    [Tooltip("Gravitational strength constant G. Acceleration at distance r = G / r^2.")]
    public float GravitationalStrength = 10000f;

    [Header("Event Horizon")]
    [Tooltip("Projectiles closer than this radius are silently consumed (destroyed / returned to pool).")]
    public float EventHorizonRadius = 15f;

    [Header("Gravity Tuning")]
    [Tooltip("Clamps the per-frame acceleration to at least this value (m/s²) for any object inside InfluenceRadius.\n"
           + "0 = pure inverse-square (realistic but often invisible at combat distances).\n"
           + "Rule of thumb: set to ~Speed × tan(desired_deflection_angle).\n"
           + "For 30° deflection/sec on a 600 m/s bullet → ~350 m/s². For dramatic bending → 500-800 m/s².")]
    public float MinAcceleration = 0f;

    [Header("Debug")]
    [Tooltip("Print registration counts, distances and force magnitudes to the Console every DebugLogInterval seconds.")]
    public bool DebugMode = false;
    public float DebugLogInterval = 2f;

    // ---------------------------------------------------------------------------
    // Static registration — projectiles self-register so no FindObjectsOfType needed.
    // Gravity sources self-register too so weapons can compensate against all active
    // fields in the scene without manual references.
    // ---------------------------------------------------------------------------

    private static readonly List<BlackHoleGravity> _activeGravitySources = new List<BlackHoleGravity>(8);
    private static readonly List<AAMissile>        _activeMissiles       = new List<AAMissile>(32);
    private static readonly List<BulletPhysics>    _activeBullets        = new List<BulletPhysics>(64);

    public static IReadOnlyList<BlackHoleGravity> ActiveGravitySources => _activeGravitySources;

    public static void RegisterMissile(AAMissile missile)
    {
        if (missile != null && !_activeMissiles.Contains(missile))
            _activeMissiles.Add(missile);
    }

    public static void UnregisterMissile(AAMissile missile)
    {
        _activeMissiles.Remove(missile);
    }

    public static void RegisterBullet(BulletPhysics bullet)
    {
        if (bullet != null && !_activeBullets.Contains(bullet))
            _activeBullets.Add(bullet);
    }

    public static void UnregisterBullet(BulletPhysics bullet)
    {
        _activeBullets.Remove(bullet);
    }

    // ---------------------------------------------------------------------------

    private Transform _cachedTransform;
    private float _debugTimer;
    private int   _debugBulletsAffected;
    private int   _debugMissilesAffected;
    private int   _debugBulletsConsumed;
    private int   _debugMissilesConsumed;
    private float _debugBulletDistSum;
    private float _debugBulletAccelSum;
    private float _debugMissileDistSum;
    private float _debugMissileAccelSum;

    private void OnEnable()
    {
        if (!_activeGravitySources.Contains(this))
            _activeGravitySources.Add(this);
    }

    private void OnDisable()
    {
        _activeGravitySources.Remove(this);
    }

    private void Awake()
    {
        _cachedTransform = transform;
    }

    private void Update()
    {
        Vector3 bhPos        = _cachedTransform.position;
        float eventHorizonSqr = EventHorizonRadius * EventHorizonRadius;
        float influenceSqr    = InfluenceRadius    * InfluenceRadius;

        // --- Missiles ---
        for (int i = _activeMissiles.Count - 1; i >= 0; i--)
        {
            AAMissile missile = _activeMissiles[i];
            if (missile == null)
            {
                _activeMissiles.RemoveAt(i);
                continue;
            }

            Vector3 toBlackHole = bhPos - missile.transform.position;
            float   distSqr     = toBlackHole.sqrMagnitude;

            if (distSqr > influenceSqr) continue;

            if (distSqr <= eventHorizonSqr)
            {
                if (DebugMode) _debugMissilesConsumed++;
                missile.DestroyMissile(false);
                continue;
            }

            float dist  = Mathf.Sqrt(distSqr);
            float accel = Mathf.Max(GravitationalStrength / distSqr, MinAcceleration);
            missile.AddExternalVelocity(toBlackHole.normalized * accel * Time.deltaTime);
            if (DebugMode) { _debugMissilesAffected++; _debugMissileDistSum += dist; _debugMissileAccelSum += accel; }
        }

        // --- Bullets ---
        for (int i = _activeBullets.Count - 1; i >= 0; i--)
        {
            BulletPhysics bullet = _activeBullets[i];
            if (bullet == null)
            {
                _activeBullets.RemoveAt(i);
                continue;
            }

            Vector3 toBlackHole = bhPos - bullet.transform.position;
            float   distSqr     = toBlackHole.sqrMagnitude;

            if (distSqr > influenceSqr) continue;

            if (distSqr <= eventHorizonSqr)
            {
                if (DebugMode) _debugBulletsConsumed++;
                bullet.ConsumeBullet();
                continue;
            }

            float dist  = Mathf.Sqrt(distSqr);
            float accel = Mathf.Max(GravitationalStrength / distSqr, MinAcceleration);
            bullet.AddExternalVelocity(toBlackHole.normalized * accel * Time.deltaTime);
            if (DebugMode) { _debugBulletsAffected++; _debugBulletDistSum += dist; _debugBulletAccelSum += accel; }
        }

        // --- Debug output ---
        if (DebugMode)
        {
            _debugTimer += Time.deltaTime;
            if (_debugTimer >= DebugLogInterval)
            {
                _debugTimer = 0f;
                float avgBulletDist  = _debugBulletsAffected  > 0 ? _debugBulletDistSum  / _debugBulletsAffected  : 0f;
                float avgBulletAccel = _debugBulletsAffected  > 0 ? _debugBulletAccelSum / _debugBulletsAffected  : 0f;
                float avgMissileDist  = _debugMissilesAffected > 0 ? _debugMissileDistSum  / _debugMissilesAffected : 0f;
                float avgMissileAccel = _debugMissilesAffected > 0 ? _debugMissileAccelSum / _debugMissilesAffected : 0f;

                Debug.Log(
                    $"[BlackHoleGravity] Registered: {_activeBullets.Count} bullets, {_activeMissiles.Count} missiles\n"
                    + $"  Bullets  pulled: {_debugBulletsAffected,5} | avg dist: {avgBulletDist,8:F1} | avg accel: {avgBulletAccel,8:F4} m/s² | consumed: {_debugBulletsConsumed}\n"
                    + $"  Missiles pulled: {_debugMissilesAffected,5} | avg dist: {avgMissileDist,8:F1} | avg accel: {avgMissileAccel,8:F4} m/s² | consumed: {_debugMissilesConsumed}",
                    this
                );
                _debugBulletsAffected  = 0;
                _debugMissilesAffected = 0;
                _debugBulletsConsumed  = 0;
                _debugMissilesConsumed = 0;
                _debugBulletDistSum    = 0f;
                _debugBulletAccelSum   = 0f;
                _debugMissileDistSum   = 0f;
                _debugMissileAccelSum  = 0f;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Event horizon — red
        Gizmos.color = new Color(0.9f, 0.1f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, EventHorizonRadius);

        // Influence radius — orange
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, InfluenceRadius);

#if UNITY_EDITOR
        // Draw lines to all tracked objects during Play mode
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            foreach (var b in _activeBullets)
                if (b != null) Gizmos.DrawLine(transform.position, b.transform.position);

            Gizmos.color = Color.yellow;
            foreach (var m in _activeMissiles)
                if (m != null) Gizmos.DrawLine(transform.position, m.transform.position);
        }
#endif
    }
}

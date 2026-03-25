using UnityEngine;
using System.Collections.Generic;
using static GlobalHelper;

/// <summary>
/// Coordinates multiple BoidsManager flocks as a unified fleet.
/// Provides fleet-wide commands, cross-flock target sharing, and fleet formation anchoring.
/// Opt-in: individual BoidsManagers work independently without a FleetController.
/// </summary>
public class FleetController : MonoBehaviour
{
    [Header("Fleet Members")]
    [SerializeField] private List<BoidsManager> _flocks = new List<BoidsManager>();

    [Header("Fleet Formation")]
    [Tooltip("The fleet anchor point. Flock leaders orient relative to this transform.")]
    [SerializeField] private Transform _fleetAnchor;

    [Header("Cross-Flock Targeting")]
    [Tooltip("Share detected targets across all flocks in the fleet.")]
    [SerializeField] private bool _shareTargets = true;
    [Tooltip("Interval (seconds) between cross-flock target synchronization.")]
    [SerializeField] private float _targetSyncInterval = 0.5f;

    [Header("Fleet Cohesion")]
    [Tooltip("Keep flocks together by steering leaders toward the fleet center.")]
    [SerializeField] private bool _keepFlocksTogether = true;
    [Tooltip("How far a flock leader can drift from the fleet center before being pulled back.")]
    [SerializeField] private float _fleetTetherRadius = 500f;
    [Tooltip("How fast the fleet centroid marker tracks the actual centroid (0=instant, higher=smoother).")]
    [SerializeField] private float _centroidSmoothing = 2f;

    [Header("Coordinated Morale")]
    [Tooltip("Compute morale across the entire fleet rather than per-flock.")]
    [SerializeField] private bool _useFleetWideMorale = false;

    private float _lastTargetSyncTime;
    private List<BoidTargetInfo> _sharedTargetPool = new List<BoidTargetInfo>(32);
    private Transform _centroidMarker;

    // ── Properties ──

    public IReadOnlyList<BoidsManager> Flocks => _flocks;
    public Transform FleetAnchor => _fleetAnchor;

    public int TotalBoidCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _flocks.Count; i++)
            {
                if (_flocks[i] != null)
                    count += _flocks[i].BoidCount;
            }
            return count;
        }
    }

    // ── Registration ──

    public void RegisterFlock(BoidsManager flock)
    {
        if (flock == null || _flocks.Contains(flock)) return;
        _flocks.Add(flock);
        flock.Fleet = this;
    }

    public void UnregisterFlock(BoidsManager flock)
    {
        if (flock == null) return;
        _flocks.Remove(flock);
        flock.Fleet = null;
    }

    // ── Fleet-Wide Commands ──

    /// <summary>
    /// Issue a command to ALL flocks in the fleet.
    /// </summary>
    public void IssueFleetCommand(BoidCommand command)
    {
        for (int i = 0; i < _flocks.Count; i++)
        {
            var cc = GetCommandController(_flocks[i]);
            if (cc != null)
                cc.IssueCommand(command);
        }
    }

    /// <summary>
    /// Issue a command only to flocks that contain the given VehicleType.
    /// </summary>
    public void IssueCommandToType(BoidCommand command, VehicleType type)
    {
        for (int i = 0; i < _flocks.Count; i++)
        {
            if (FlockContainsType(_flocks[i], type))
            {
                var cc = GetCommandController(_flocks[i]);
                if (cc != null)
                    cc.IssueCommand(command);
            }
        }
    }

    /// <summary>
    /// Issue a command only to flocks whose dominant zone matches the given zone.
    /// </summary>
    public void IssueCommandToZone(BoidCommand command, FormationZone zone)
    {
        for (int i = 0; i < _flocks.Count; i++)
        {
            if (FlockMatchesZone(_flocks[i], zone))
            {
                var cc = GetCommandController(_flocks[i]);
                if (cc != null)
                    cc.IssueCommand(command);
            }
        }
    }

    /// <summary>
    /// Fleet attack: Screens break formation and attack; Escorts defend the Core;
    /// Core maintains formation and engages.
    /// </summary>
    public void FleetAttack(Transform target)
    {
        for (int i = 0; i < _flocks.Count; i++)
        {
            var flock = _flocks[i];
            if (flock == null) continue;

            var cc = GetCommandController(flock);
            if (cc == null) continue;

            FormationZone dominantZone = GetDominantZone(flock);

            switch (dominantZone)
            {
                case FormationZone.Screen:
                    // Fighters/bombers break formation and engage freely
                    cc.IssueCommand(BoidCommand.Attack(target));
                    break;
                case FormationZone.Escort:
                    // Escorts defend the fleet anchor or core
                    if (_fleetAnchor != null)
                        cc.IssueCommand(BoidCommand.Defend(_fleetAnchor, 300f));
                    else
                        cc.IssueCommand(BoidCommand.Attack(target));
                    break;
                case FormationZone.Core:
                    // Capitals maintain formation, set priority target
                    cc.AttackTarget(target);
                    break;
            }
        }
    }

    /// <summary>
    /// Fleet form-up: all flocks tighten formation around fleet anchor.
    /// </summary>
    public void FleetFormUp()
    {
        for (int i = 0; i < _flocks.Count; i++)
        {
            var cc = GetCommandController(_flocks[i]);
            if (cc != null)
                cc.FormUp();
        }
    }

    /// <summary>
    /// Fleet retreat: all flocks return to base.
    /// </summary>
    public void FleetRetreat()
    {
        for (int i = 0; i < _flocks.Count; i++)
        {
            var cc = GetCommandController(_flocks[i]);
            if (cc != null)
                cc.ReturnToBase();
        }
    }

    // ── Target Sharing & Fleet Cohesion ──

    void Start()
    {
        // Create the fleet centroid marker (invisible transform that leaders follow)
        if (_centroidMarker == null)
        {
            var go = new GameObject($"{gameObject.name}_FleetCentroid");
            go.transform.SetParent(transform);
            _centroidMarker = go.transform;
        }

        // Wire flocks to this controller
        for (int i = 0; i < _flocks.Count; i++)
        {
            if (_flocks[i] != null)
                _flocks[i].Fleet = this;
        }
    }

    void LateUpdate()
    {
        if (_keepFlocksTogether)
            UpdateFleetCohesion();
    }

    void Update()
    {
        if (_shareTargets && Time.time - _lastTargetSyncTime >= _targetSyncInterval)
        {
            SyncSharedTargets();
            _lastTargetSyncTime = Time.time;
        }
    }

    private void UpdateFleetCohesion()
    {
        // Compute fleet centroid from leaders (or flock centers)
        Vector3 centroid = Vector3.zero;
        int count = 0;
        for (int i = 0; i < _flocks.Count; i++)
        {
            var flock = _flocks[i];
            if (flock == null || flock.BoidCount == 0) continue;

            // Use leader position if available, else flock center
            var leader = flock.Leader;
            centroid += (leader != null) ? leader.transform.position : flock.FlockCenter;
            count++;
        }

        if (count == 0) return;
        centroid /= count;

        // If a fleet anchor is set, use it as the target instead of the centroid
        Vector3 targetPos = (_fleetAnchor != null) ? _fleetAnchor.position : centroid;

        // Smoothly move the centroid marker toward the target position
        if (_centroidSmoothing > 0f)
            _centroidMarker.position = Vector3.Lerp(_centroidMarker.position, targetPos, Time.deltaTime * _centroidSmoothing);
        else
            _centroidMarker.position = targetPos;

        // Assign the centroid marker as each flock's target if they don't already have one
        for (int i = 0; i < _flocks.Count; i++)
        {
            var flock = _flocks[i];
            if (flock == null || flock.BoidCount == 0) continue;

            // Only set target if the flock has no explicit target assigned
            if (flock.target == null)
            {
                flock.SetTarget(_centroidMarker);
            }
        }
    }

    private void SyncSharedTargets()
    {
        _sharedTargetPool.Clear();

        // Collect all known targets from all flocks
        for (int i = 0; i < _flocks.Count; i++)
        {
            var flock = _flocks[i];
            if (flock == null || flock.TargetManager == null) continue;

            var knownTargets = flock.TargetManager.GetKnownTargets();
            foreach (var kvp in knownTargets)
            {
                if (kvp.Value != null && kvp.Value.IsValid)
                    _sharedTargetPool.Add(kvp.Value);
            }
        }

        // Push shared pool to all flock target managers
        for (int i = 0; i < _flocks.Count; i++)
        {
            if (_flocks[i] != null && _flocks[i].TargetManager != null)
                _flocks[i].TargetManager.SetFleetTargetPool(_sharedTargetPool);
        }
    }

    public IReadOnlyList<BoidTargetInfo> SharedTargets => _sharedTargetPool;

    // ── Fleet Morale ──

    public float GetFleetMoraleScore()
    {
        if (_flocks.Count == 0) return 1f;

        float totalScore = 0f;
        int count = 0;
        for (int i = 0; i < _flocks.Count; i++)
        {
            if (_flocks[i] != null)
            {
                totalScore += _flocks[i].CurrentMoraleScore;
                count++;
            }
        }
        return count > 0 ? totalScore / count : 1f;
    }

    // ── Helpers ──

    private BoidCommandController GetCommandController(BoidsManager flock)
    {
        if (flock == null) return null;
        return flock.GetComponent<BoidCommandController>();
    }

    private bool FlockContainsType(BoidsManager flock, VehicleType type)
    {
        if (flock == null) return false;
        var boids = flock.Boids;
        for (int i = 0; i < boids.Count; i++)
        {
            if (boids[i] != null && boids[i].ShipClass == type)
                return true;
        }
        return false;
    }

    private bool FlockMatchesZone(BoidsManager flock, FormationZone zone)
    {
        return GetDominantZone(flock) == zone;
    }

    private FormationZone GetDominantZone(BoidsManager flock)
    {
        if (flock == null || flock.BoidCount == 0) return FormationZone.Screen;

        int screen = 0, escort = 0, core = 0;
        var boids = flock.Boids;
        for (int i = 0; i < boids.Count; i++)
        {
            if (boids[i] == null) continue;
            switch (boids[i].FormationZone)
            {
                case FormationZone.Screen: screen++; break;
                case FormationZone.Escort: escort++; break;
                case FormationZone.Core:   core++;   break;
            }
        }

        if (core >= escort && core >= screen) return FormationZone.Core;
        if (escort >= screen) return FormationZone.Escort;
        return FormationZone.Screen;
    }
}

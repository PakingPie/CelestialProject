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

    [Header("Coordinated Morale")]
    [Tooltip("Compute morale across the entire fleet rather than per-flock.")]
    [SerializeField] private bool _useFleetWideMorale = false;

    private float _lastTargetSyncTime;
    private List<BoidTargetInfo> _sharedTargetPool = new List<BoidTargetInfo>(32);

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

    // ── Target Sharing ──

    void Update()
    {
        if (_shareTargets && Time.time - _lastTargetSyncTime >= _targetSyncInterval)
        {
            SyncSharedTargets();
            _lastTargetSyncTime = Time.time;
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

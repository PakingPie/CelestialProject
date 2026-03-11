using UnityEngine;

public enum BoidTacticalState
{
    Transit,
    ApproachTarget,
    HoldCombatEnvelope,
    BreakAway,
    RegroupToAnchor,
    Retreat,
    EvadeThreat
}

public enum BoidDesiredMoveMode
{
    FollowFormation,
    PursueTarget,
    HoldRange,
    BreakAway,
    RejoinAnchor,
    Evade,
    Retreat
}

[RequireComponent(typeof(Boid))]
public class BoidIndividualTacticalLayer : MonoBehaviour
{
    [Header("Evaluation")]
    [SerializeField] private bool _debugMode = false;
    [SerializeField] private float _minimumStateDuration = 0.35f;

    [Header("Observed State")]
    [SerializeField] private BoidTacticalState _currentTacticalState = BoidTacticalState.Transit;
    [SerializeField] private BoidDesiredMoveMode _desiredMoveMode = BoidDesiredMoveMode.FollowFormation;
    [SerializeField] private Transform _desiredTarget;
    [SerializeField] private Vector3 _desiredAimPoint;
    [SerializeField] private Vector3 _desiredMoveDirection;
    [SerializeField] private float _desiredRangeCenter;
    [SerializeField] private float _desiredRangeTolerance;
    [SerializeField] private float _targetDistance = -1f;
    [SerializeField] private float _distanceToAnchor = -1f;
    [SerializeField] private float _suggestedSpeedMultiplier = 1f;
    [SerializeField] private float _localAggressionBias = 1f;
    [SerializeField] private float _localAnchorBias = 1f;
    [SerializeField] private bool _allowWeaponUse = true;
    [SerializeField] private string _lastStateReason;

    private Boid _boid;
    private BoidAttackBehavior _attackBehavior;
    private VehicleBase _vehicle;
    private float _stateEnteredTime;

    private bool UseHybridProfileSemantics => _vehicle == null || _vehicle.BoidManager == null || _vehicle.BoidManager.UseHybridProfileSemantics;
    private bool UseLiveSquadOutputs => _vehicle != null && _vehicle.BoidManager != null && _vehicle.BoidManager.UseSquadTacticalBrainLiveOutputs;

    public BoidTacticalState CurrentTacticalState => _currentTacticalState;
    public BoidDesiredMoveMode DesiredMoveMode => _desiredMoveMode;
    public Transform DesiredTarget => _desiredTarget;
    public Vector3 DesiredAimPoint => _desiredAimPoint;
    public Vector3 DesiredMoveDirection => _desiredMoveDirection;
    public float DesiredRangeCenter => _desiredRangeCenter;
    public float DesiredRangeTolerance => _desiredRangeTolerance;
    public float TargetDistance => _targetDistance;
    public float DistanceToAnchor => _distanceToAnchor;
    public float SuggestedSpeedMultiplier => _suggestedSpeedMultiplier;
    public float LocalAggressionBias => _localAggressionBias;
    public float LocalAnchorBias => _localAnchorBias;
    public bool AllowWeaponUse => _allowWeaponUse;
    public string LastStateReason => _lastStateReason;
    public bool HasCombatSteeringIntent => _desiredMoveDirection.sqrMagnitude > 0.0001f && _currentTacticalState != BoidTacticalState.Transit;

    private void Awake()
    {
        _boid = GetComponent<Boid>();
        _attackBehavior = GetComponent<BoidAttackBehavior>();
        _vehicle = GetComponent<VehicleBase>();
        _stateEnteredTime = Time.time;
    }

    public void EvaluateState()
    {
        if (_boid == null)
            return;

        if (_vehicle != null && _vehicle.BoidManager != null && !_vehicle.BoidManager.UseIndividualTacticalLayer)
        {
            ResetCompatibilityOutputs();
            return;
        }

        BoidAttackProfile profile = _attackBehavior != null ? _attackBehavior.Profile : null;
        BoidSquadBlackboard blackboard = _vehicle != null && _vehicle.BoidManager != null && _vehicle.BoidManager.UseSquadBlackboard ? _vehicle.BoidManager.SquadBlackboard : null;
        BoidFlockTargetManager targetManager = _boid.TargetManager;

        Transform combatTarget = _boid.IsInCombat && targetManager != null ? targetManager.GetTargetForBoid(_boid) : null;
        if (combatTarget == null && _boid.IsInCombat)
            combatTarget = _boid.CurrentTarget;

        _desiredTarget = combatTarget;
        _desiredAimPoint = combatTarget != null ? combatTarget.position : Vector3.zero;
        _targetDistance = combatTarget != null ? Vector3.Distance(_boid.position, combatTarget.position) : -1f;

        CombatAnchorMode anchorMode = UseLiveSquadOutputs && blackboard != null ? blackboard.DesiredAnchorMode : (_boid.Settings != null ? _boid.Settings.combatAnchorMode : CombatAnchorMode.None);
        Vector3 anchorPosition = Vector3.zero;
        bool hasAnchor = targetManager != null && targetManager.TryGetCombatAnchorPosition(_boid, anchorMode, out anchorPosition);
        _distanceToAnchor = hasAnchor ? Vector3.Distance(_boid.position, anchorPosition) : -1f;

        _desiredRangeCenter = GetDesiredRangeCenter(profile);
        _desiredRangeTolerance = GetDesiredRangeTolerance(profile);

        BoidTacticalState desiredState = DetermineDesiredState(blackboard, profile, hasAnchor, combatTarget);
        string stateReason = BuildStateReason(desiredState, hasAnchor, combatTarget);

        if (ShouldChangeState(desiredState))
        {
            if (_debugMode && desiredState != _currentTacticalState)
            {
                Debug.Log($"[{name}] Tactical state: {_currentTacticalState} -> {desiredState} | {stateReason}");
            }

            _currentTacticalState = desiredState;
            _stateEnteredTime = Time.time;
        }

        _lastStateReason = stateReason;
        UpdateOutputs(profile, blackboard, hasAnchor, anchorPosition, combatTarget);
    }

    public Color GetDebugColor()
    {
        switch (_currentTacticalState)
        {
            case BoidTacticalState.ApproachTarget:
                return new Color(1f, 0.6f, 0.2f, 0.9f);
            case BoidTacticalState.HoldCombatEnvelope:
                return new Color(0.2f, 1f, 1f, 0.9f);
            case BoidTacticalState.BreakAway:
                return new Color(1f, 0.3f, 0.3f, 0.9f);
            case BoidTacticalState.RegroupToAnchor:
                return new Color(0.2f, 0.8f, 1f, 0.9f);
            case BoidTacticalState.Retreat:
                return new Color(1f, 0.1f, 1f, 0.9f);
            default:
                return new Color(0.7f, 0.7f, 0.7f, 0.8f);
        }
    }

    private BoidTacticalState DetermineDesiredState(BoidSquadBlackboard blackboard, BoidAttackProfile profile, bool hasAnchor, Transform combatTarget)
    {
        if (UseLiveSquadOutputs && blackboard != null && (blackboard.RetreatRequested || blackboard.TacticalMode == SquadTacticalMode.ReturnToBase || blackboard.TacticalMode == SquadTacticalMode.Retreat))
            return BoidTacticalState.Retreat;

        if (UseLiveSquadOutputs && blackboard != null && (blackboard.RegroupRequested || blackboard.TacticalMode == SquadTacticalMode.Regroup || blackboard.TacticalMode == SquadTacticalMode.FormUp))
            return BoidTacticalState.RegroupToAnchor;

        if (combatTarget == null)
            return BoidTacticalState.Transit;

        float hardAvoidDistance = GetHardAvoidDistance(profile);
        float desiredMaxDistance = GetDesiredRangeMax(profile);
        float leashRadius = _boid.Settings != null ? Mathf.Max(0f, _boid.Settings.combatLeashRadius) : 0f;

        if (UseHybridProfileSemantics && profile != null && profile.MaxPursuitAnchorDistance > 0f)
        {
            leashRadius = leashRadius > 0f ? Mathf.Min(leashRadius, profile.MaxPursuitAnchorDistance) : profile.MaxPursuitAnchorDistance;
        }

        if (_currentTacticalState == BoidTacticalState.BreakAway && profile != null)
        {
            if (_targetDistance >= 0f && _targetDistance < GetBreakawayDistance(profile))
                return BoidTacticalState.BreakAway;

            if (Time.time - _stateEnteredTime < GetReengageDelay(profile))
                return BoidTacticalState.BreakAway;
        }

        if (hasAnchor && leashRadius > 0f && _distanceToAnchor > leashRadius)
            return BoidTacticalState.BreakAway;

        if (_targetDistance >= 0f && hardAvoidDistance > 0f && _targetDistance < hardAvoidDistance)
            return BoidTacticalState.BreakAway;

        if (_targetDistance >= 0f && _targetDistance <= desiredMaxDistance && _targetDistance >= hardAvoidDistance)
            return BoidTacticalState.HoldCombatEnvelope;

        return BoidTacticalState.ApproachTarget;
    }

    private string BuildStateReason(BoidTacticalState desiredState, bool hasAnchor, Transform combatTarget)
    {
        switch (desiredState)
        {
            case BoidTacticalState.Retreat:
                return "Squad tactical brain requested retreat";
            case BoidTacticalState.RegroupToAnchor:
                return "Squad tactical brain requested regroup or formation recovery";
            case BoidTacticalState.Transit:
                return combatTarget == null ? "No valid combat target" : "Transit posture";
            case BoidTacticalState.BreakAway:
                if (hasAnchor && _boid.Settings != null && _distanceToAnchor > _boid.Settings.combatLeashRadius)
                    return "Exceeded combat leash radius";
                return "Too close to target, break away";
            case BoidTacticalState.HoldCombatEnvelope:
                return "Inside preferred combat envelope";
            case BoidTacticalState.ApproachTarget:
                return "Closing toward preferred combat envelope";
            default:
                return "Tactical fallback";
        }
    }

    private bool ShouldChangeState(BoidTacticalState desiredState)
    {
        if (desiredState == _currentTacticalState)
            return false;

        if (desiredState == BoidTacticalState.Retreat || desiredState == BoidTacticalState.RegroupToAnchor)
            return true;

        return Time.time - _stateEnteredTime >= _minimumStateDuration;
    }

    private void UpdateOutputs(BoidAttackProfile profile, BoidSquadBlackboard blackboard, bool hasAnchor, Vector3 anchorPosition, Transform combatTarget)
    {
        _desiredMoveDirection = Vector3.zero;
        _desiredAimPoint = combatTarget != null ? combatTarget.position : Vector3.zero;
        _desiredMoveMode = BoidDesiredMoveMode.FollowFormation;
        _suggestedSpeedMultiplier = 1f;
        _localAggressionBias = 0.25f;
        _localAnchorBias = 1f;
        _allowWeaponUse = combatTarget != null;

        float rejoinUrgency = UseHybridProfileSemantics && profile != null ? profile.RejoinUrgency : 0.5f;
        float defensiveEvasionBias = UseHybridProfileSemantics && profile != null ? profile.DefensiveEvasionBias : 0.5f;
        float focusFireAffinity = UseHybridProfileSemantics && profile != null ? profile.FocusFireAffinity : 0.5f;

        Vector3 toTarget = combatTarget != null ? (combatTarget.position - _boid.position) : Vector3.zero;
        Vector3 awayFromTarget = combatTarget != null && toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : Vector3.zero;
        Vector3 toAnchor = hasAnchor ? (anchorPosition - _boid.position) : Vector3.zero;

        switch (_currentTacticalState)
        {
            case BoidTacticalState.Transit:
                _desiredMoveMode = BoidDesiredMoveMode.FollowFormation;
                _suggestedSpeedMultiplier = 1f;
                _localAggressionBias = 0.2f;
                _localAnchorBias = 1.15f;
                _allowWeaponUse = false;
                break;

            case BoidTacticalState.ApproachTarget:
                _desiredMoveMode = BoidDesiredMoveMode.PursueTarget;
                _desiredMoveDirection = GetProfileDrivenDirection(combatTarget, toTarget);
                _suggestedSpeedMultiplier = profile != null ? Mathf.Max(1f, profile.approachSpeedMultiplier) : 1f;
                _localAggressionBias = (0.8f + (UseLiveSquadOutputs && blackboard != null ? blackboard.DesiredAggression * 0.4f : 0f)) * Mathf.Lerp(1f, 0.75f, defensiveEvasionBias);
                if (UseLiveSquadOutputs && blackboard != null && blackboard.DesiredFocusTarget != null && combatTarget == blackboard.DesiredFocusTarget)
                    _localAggressionBias += focusFireAffinity * 0.2f;
                _localAnchorBias = 1f;
                _allowWeaponUse = true;
                break;

            case BoidTacticalState.HoldCombatEnvelope:
                _desiredMoveMode = BoidDesiredMoveMode.HoldRange;
                _desiredMoveDirection = GetProfileDrivenDirection(combatTarget, toTarget);
                _suggestedSpeedMultiplier = profile != null ? Mathf.Max(0.75f, profile.engageSpeedMultiplier) : 1f;
                _localAggressionBias = (0.6f + (UseLiveSquadOutputs && blackboard != null ? blackboard.DesiredAggression * 0.35f : 0f)) * Mathf.Lerp(1f, 0.8f, defensiveEvasionBias);
                if (UseLiveSquadOutputs && blackboard != null && blackboard.DesiredFocusTarget != null && combatTarget == blackboard.DesiredFocusTarget)
                    _localAggressionBias += focusFireAffinity * 0.15f;
                _localAnchorBias = Mathf.Lerp(0.75f, 1.2f, rejoinUrgency);
                _allowWeaponUse = true;
                break;

            case BoidTacticalState.BreakAway:
                _desiredMoveMode = BoidDesiredMoveMode.BreakAway;
                _desiredMoveDirection = awayFromTarget;
                if (hasAnchor && toAnchor.sqrMagnitude > 0.001f)
                    _desiredMoveDirection = (awayFromTarget + toAnchor.normalized * 0.75f).normalized;
                _suggestedSpeedMultiplier = profile != null ? Mathf.Max(1f, profile.retreatSpeedMultiplier) : 1.2f;
                _localAggressionBias = Mathf.Lerp(0.35f, 0.15f, defensiveEvasionBias);
                _localAnchorBias = Mathf.Lerp(1.1f, 1.7f, rejoinUrgency);
                _allowWeaponUse = false;
                break;

            case BoidTacticalState.RegroupToAnchor:
                _desiredMoveMode = BoidDesiredMoveMode.RejoinAnchor;
                _desiredMoveDirection = hasAnchor && toAnchor.sqrMagnitude > 0.001f ? toAnchor.normalized : awayFromTarget;
                _desiredAimPoint = hasAnchor ? anchorPosition : _desiredAimPoint;
                _suggestedSpeedMultiplier = profile != null ? Mathf.Max(1f, profile.approachSpeedMultiplier) : 1.1f;
                _localAggressionBias = Mathf.Lerp(0.2f, 0.05f, defensiveEvasionBias);
                _localAnchorBias = Mathf.Lerp(1.2f, 2f, rejoinUrgency);
                _allowWeaponUse = false;
                break;

            case BoidTacticalState.Retreat:
                _desiredMoveMode = BoidDesiredMoveMode.Retreat;
                _desiredMoveDirection = awayFromTarget;
                if (hasAnchor && toAnchor.sqrMagnitude > 0.001f)
                {
                    _desiredMoveDirection = (toAnchor.normalized * 1.25f + awayFromTarget * 0.85f).normalized;
                    _desiredAimPoint = anchorPosition;
                }
                _suggestedSpeedMultiplier = profile != null ? Mathf.Max(1.1f, profile.retreatSpeedMultiplier) : 1.3f;
                _localAggressionBias = Mathf.Lerp(0.1f, 0.01f, defensiveEvasionBias);
                _localAnchorBias = Mathf.Lerp(1.4f, 2.2f, rejoinUrgency);
                _allowWeaponUse = false;
                break;
        }
    }

    private Vector3 GetProfileDrivenDirection(Transform combatTarget, Vector3 toTarget)
    {
        if (combatTarget == null)
            return Vector3.zero;

        if (_attackBehavior != null && _attackBehavior.Profile != null)
            return _attackBehavior.GetDesiredMovementDirection(combatTarget.position, combatTarget.forward);

        return toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : _boid.forward;
    }

    private float GetDesiredRangeCenter(BoidAttackProfile profile)
    {
        if (profile == null)
            return 0f;

        return UseHybridProfileSemantics ? profile.DesiredRangeCenter : profile.engagementDistance;
    }

    private float GetDesiredRangeTolerance(BoidAttackProfile profile)
    {
        if (profile == null)
            return 50f;

        if (UseHybridProfileSemantics)
            return profile.DesiredRangeTolerance;

        return Mathf.Max(25f, profile.maxDistance - profile.minDistance) * 0.5f;
    }

    private float GetHardAvoidDistance(BoidAttackProfile profile)
    {
        if (profile == null)
            return 0f;

        return UseHybridProfileSemantics ? profile.HardAvoidDistance : Mathf.Max(0f, profile.minDistance);
    }

    private float GetDesiredRangeMax(BoidAttackProfile profile)
    {
        if (profile == null)
            return GetHardAvoidDistance(profile) + _desiredRangeTolerance;

        return UseHybridProfileSemantics ? profile.DesiredRangeMax : Mathf.Max(GetHardAvoidDistance(profile), profile.maxDistance);
    }

    private float GetBreakawayDistance(BoidAttackProfile profile)
    {
        if (profile == null)
            return 0f;

        return UseHybridProfileSemantics ? profile.BreakawayDistance : profile.retreatDistance;
    }

    private float GetReengageDelay(BoidAttackProfile profile)
    {
        if (profile == null)
            return 0f;

        return UseHybridProfileSemantics ? profile.ReengageDelay : profile.regroupTime;
    }

    private void ResetCompatibilityOutputs()
    {
        _desiredTarget = null;
        _desiredAimPoint = Vector3.zero;
        _desiredMoveDirection = Vector3.zero;
        _desiredMoveMode = BoidDesiredMoveMode.FollowFormation;
        _desiredRangeCenter = 0f;
        _desiredRangeTolerance = 0f;
        _targetDistance = -1f;
        _distanceToAnchor = -1f;
        _suggestedSpeedMultiplier = 1f;
        _localAggressionBias = 1f;
        _localAnchorBias = 1f;
        _allowWeaponUse = true;
        _lastStateReason = "Individual tactical layer disabled by migration flags";
        _currentTacticalState = BoidTacticalState.Transit;
    }
}
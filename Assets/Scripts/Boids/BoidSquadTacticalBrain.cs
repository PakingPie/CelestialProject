using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BoidSquadTacticalBrain : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoidsManager _boidsManager;
    [SerializeField] private BoidSquadBlackboard _blackboard;

    [Header("Evaluation")]
    [SerializeField] private float _evaluationInterval = 0.2f;
    [SerializeField] private bool _debugMode = false;
    [SerializeField] private bool _logDecisionChangesOnly = true;
    [SerializeField] private bool _drawDebugGizmos = false;
    [SerializeField] private float _debugLabelHeight = 80f;

    [Header("Retreat")]
    [SerializeField] [Range(0f, 1f)] private float _retreatAverageHullThreshold = 0.35f;
    [SerializeField] private int _retreatMinCombatCapableBoids = 2;
    [SerializeField] private float _retreatPressureMultiplier = 1.6f;
    [SerializeField] private float _minimumRetreatDuration = 3f;

    [Header("Regroup")]
    [SerializeField] [Range(0f, 1f)] private float _regroupCohesionEnterThreshold = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float _regroupCohesionExitThreshold = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float _regroupOutOfEnvelopeFractionThreshold = 0.55f;
    [SerializeField] private float _regroupAnchorDistanceMultiplier = 1.35f;

    [Header("Combat")]
    [SerializeField] private float _focusFirePressureThreshold = 4f;

    private float _lastEvaluationTime = float.NegativeInfinity;
    private float _lastRetreatStartTime = float.NegativeInfinity;
    private bool _regroupLatched;
    private TacticalDecision _lastDecision;
    private bool _hasDecision;

    private struct TacticalDecision
    {
        public SquadTacticalMode TacticalMode;
        public bool DesiredFormationUsage;
        public CombatAnchorMode DesiredAnchorMode;
        public Transform DesiredFocusTarget;
        public SquadCombatSpread DesiredCombatSpread;
        public float DesiredAggression;
        public bool RegroupRequested;
        public bool RetreatRequested;
        public string Reason;

    }

    private void Awake()
    {
        if (_boidsManager == null)
            _boidsManager = GetComponent<BoidsManager>();

        if (_blackboard == null && _boidsManager != null)
            _blackboard = _boidsManager.SquadBlackboard;

        if (_blackboard == null)
            _blackboard = GetComponent<BoidSquadBlackboard>();
    }

    private void Update()
    {
        if (_blackboard == null)
            return;

        if (Time.time - _lastEvaluationTime < _evaluationInterval)
            return;

        _lastEvaluationTime = Time.time;
        EvaluateTacticalIntent();
    }

    private void EvaluateTacticalIntent()
    {
        TacticalDecision decision;

        if (TryEvaluateHardCommandOverride(out decision) ||
            TryEvaluateSafetyCritical(out decision) ||
            TryEvaluateCommandBiasedIntent(out decision) ||
            TryEvaluateAutonomousCombat(out decision) ||
            TryEvaluateTravelAndFormation(out decision))
        {
            PublishDecision(decision);
            return;
        }

        decision = CreateDecision(
            SquadTacticalMode.Idle,
            desiredFormationUsage: true,
            desiredAnchorMode: GetRegroupAnchorMode(),
            desiredFocusTarget: null,
            desiredCombatSpread: SquadCombatSpread.Tight,
            desiredAggression: 0.2f,
            regroupRequested: false,
            retreatRequested: false,
            reason: "Idle fallback");

        PublishDecision(decision);
    }

    private bool TryEvaluateHardCommandOverride(out TacticalDecision decision)
    {
        switch (_blackboard.ActiveCommandType)
        {
            case BoidCommandType.ReturnToBase:
                decision = CreateDecision(
                    SquadTacticalMode.ReturnToBase,
                    desiredFormationUsage: false,
                    desiredAnchorMode: _blackboard.CurrentCommandAnchor != null ? CombatAnchorMode.CommandAnchor : CombatAnchorMode.None,
                    desiredFocusTarget: null,
                    desiredCombatSpread: SquadCombatSpread.Tight,
                    desiredAggression: 0f,
                    regroupRequested: false,
                    retreatRequested: true,
                    reason: "Hard command override: ReturnToBase");
                return true;

            case BoidCommandType.Hold:
                decision = CreateDecision(
                    SquadTacticalMode.Hold,
                    desiredFormationUsage: false,
                    desiredAnchorMode: CombatAnchorMode.FlockCenter,
                    desiredFocusTarget: null,
                    desiredCombatSpread: SquadCombatSpread.Tight,
                    desiredAggression: 0.1f,
                    regroupRequested: false,
                    retreatRequested: false,
                    reason: "Hard command override: Hold");
                return true;

            case BoidCommandType.MoveToPosition:
                decision = CreateDecision(
                    SquadTacticalMode.Travel,
                    desiredFormationUsage: true,
                    desiredAnchorMode: _blackboard.CurrentCommandAnchor != null ? CombatAnchorMode.CommandAnchor : GetRegroupAnchorMode(),
                    desiredFocusTarget: null,
                    desiredCombatSpread: SquadCombatSpread.Tight,
                    desiredAggression: 0.2f,
                    regroupRequested: false,
                    retreatRequested: false,
                    reason: "Hard command override: MoveToPosition");
                return true;
        }

        decision = default;
        return false;
    }

    private bool TryEvaluateSafetyCritical(out TacticalDecision decision)
    {
        if (ShouldRetreat())
        {
            _lastRetreatStartTime = Time.time;
            decision = CreateDecision(
                SquadTacticalMode.Retreat,
                desiredFormationUsage: _blackboard.CohesionScore >= _regroupCohesionEnterThreshold,
                desiredAnchorMode: GetRetreatAnchorMode(),
                desiredFocusTarget: null,
                desiredCombatSpread: SquadCombatSpread.Tight,
                desiredAggression: 0.1f,
                regroupRequested: true,
                retreatRequested: true,
                reason: "Safety critical: retreat triggered");
            return true;
        }

        if (ShouldRegroup())
        {
            decision = CreateDecision(
                SquadTacticalMode.Regroup,
                desiredFormationUsage: true,
                desiredAnchorMode: GetRegroupAnchorMode(),
                desiredFocusTarget: null,
                desiredCombatSpread: SquadCombatSpread.Tight,
                desiredAggression: 0.25f,
                regroupRequested: true,
                retreatRequested: false,
                reason: "Safety critical: regroup triggered");
            return true;
        }

        decision = default;
        return false;
    }

    private bool TryEvaluateCommandBiasedIntent(out TacticalDecision decision)
    {
        switch (_blackboard.ActiveCommandType)
        {
            case BoidCommandType.AttackTarget:
                decision = CreateDecision(
                    SquadTacticalMode.Attack,
                    desiredFormationUsage: false,
                    desiredAnchorMode: _blackboard.CurrentCommandAnchor != null ? CombatAnchorMode.CommandAnchor : CombatAnchorMode.FlockCenter,
                    desiredFocusTarget: _blackboard.ActiveCommandTarget,
                    desiredCombatSpread: SquadCombatSpread.Medium,
                    desiredAggression: 0.85f,
                    regroupRequested: false,
                    retreatRequested: false,
                    reason: "Command bias: AttackTarget");
                return true;

            case BoidCommandType.Defend:
                decision = CreateDecision(
                    SquadTacticalMode.Defend,
                    desiredFormationUsage: true,
                    desiredAnchorMode: CombatAnchorMode.CommandAnchor,
                    desiredFocusTarget: _blackboard.DefenseIntrusionDetected ? _blackboard.PrimaryKnownThreat : null,
                    desiredCombatSpread: SquadCombatSpread.Tight,
                    desiredAggression: 0.55f,
                    regroupRequested: false,
                    retreatRequested: false,
                    reason: "Command bias: Defend");
                return true;

            case BoidCommandType.FollowTarget:
                decision = CreateDecision(
                    SquadTacticalMode.Follow,
                    desiredFormationUsage: true,
                    desiredAnchorMode: CombatAnchorMode.CommandAnchor,
                    desiredFocusTarget: null,
                    desiredCombatSpread: SquadCombatSpread.Tight,
                    desiredAggression: _blackboard.KnownTargetCount > 0 ? 0.35f : 0.2f,
                    regroupRequested: false,
                    retreatRequested: false,
                    reason: "Command bias: FollowTarget");
                return true;

            case BoidCommandType.FormUp:
                decision = CreateDecision(
                    SquadTacticalMode.FormUp,
                    desiredFormationUsage: true,
                    desiredAnchorMode: GetRegroupAnchorMode(),
                    desiredFocusTarget: null,
                    desiredCombatSpread: SquadCombatSpread.Tight,
                    desiredAggression: 0.2f,
                    regroupRequested: true,
                    retreatRequested: false,
                    reason: "Command bias: FormUp");
                return true;

            case BoidCommandType.BreakFormation:
                decision = CreateDecision(
                    SquadTacticalMode.Attack,
                    desiredFormationUsage: false,
                    desiredAnchorMode: _blackboard.CurrentCommandAnchor != null ? CombatAnchorMode.CommandAnchor : CombatAnchorMode.FlockCenter,
                    desiredFocusTarget: _blackboard.PriorityTarget != null ? _blackboard.PriorityTarget : _blackboard.PrimaryKnownThreat,
                    desiredCombatSpread: SquadCombatSpread.Wide,
                    desiredAggression: 0.7f,
                    regroupRequested: false,
                    retreatRequested: false,
                    reason: "Command bias: BreakFormation");
                return true;
        }

        decision = default;
        return false;
    }

    private bool TryEvaluateAutonomousCombat(out TacticalDecision decision)
    {
        if (_blackboard.DefenseModeActive && _blackboard.DefenseIntrusionDetected)
        {
            decision = CreateDecision(
                SquadTacticalMode.Defend,
                desiredFormationUsage: true,
                desiredAnchorMode: CombatAnchorMode.CommandAnchor,
                desiredFocusTarget: _blackboard.PrimaryKnownThreat,
                desiredCombatSpread: SquadCombatSpread.Tight,
                desiredAggression: 0.55f,
                regroupRequested: false,
                retreatRequested: false,
                reason: "Autonomous combat: defend anchor intrusion");
            return true;
        }

        if (_blackboard.KnownTargetCount > 0)
        {
            Transform desiredFocusTarget = null;
            SquadCombatSpread desiredCombatSpread = SquadCombatSpread.Medium;
            float desiredAggression = 0.65f;
            string reason = "Autonomous combat: general attack";

            if (_blackboard.PriorityTarget != null)
            {
                desiredFocusTarget = _blackboard.PriorityTarget;
                reason = "Autonomous combat: priority target";
            }
            else if (_blackboard.HostilePressureScore >= _focusFirePressureThreshold || _blackboard.KnownTargetCount <= 2)
            {
                desiredFocusTarget = _blackboard.PrimaryKnownThreat;
                reason = "Autonomous combat: focus fire opportunity";
            }

            decision = CreateDecision(
                SquadTacticalMode.Attack,
                desiredFormationUsage: false,
                desiredAnchorMode: _blackboard.CurrentCommandAnchor != null ? CombatAnchorMode.CommandAnchor : CombatAnchorMode.FlockCenter,
                desiredFocusTarget: desiredFocusTarget,
                desiredCombatSpread: desiredCombatSpread,
                desiredAggression: desiredAggression,
                regroupRequested: false,
                retreatRequested: false,
                reason: reason);
            return true;
        }

        decision = default;
        return false;
    }

    private bool TryEvaluateTravelAndFormation(out TacticalDecision decision)
    {
        if (_blackboard.CurrentCommandAnchor != null)
        {
            decision = CreateDecision(
                _blackboard.ObservedRuntimeMode == SquadTacticalMode.Follow ? SquadTacticalMode.Follow : SquadTacticalMode.Travel,
                desiredFormationUsage: true,
                desiredAnchorMode: CombatAnchorMode.CommandAnchor,
                desiredFocusTarget: null,
                desiredCombatSpread: SquadCombatSpread.Tight,
                desiredAggression: 0.2f,
                regroupRequested: false,
                retreatRequested: false,
                reason: "Travel and formation: maintain anchor");
            return true;
        }

        if (_blackboard.CohesionScore < 0.95f && _blackboard.AliveBoidCount > 1)
        {
            decision = CreateDecision(
                SquadTacticalMode.FormUp,
                desiredFormationUsage: true,
                desiredAnchorMode: GetRegroupAnchorMode(),
                desiredFocusTarget: null,
                desiredCombatSpread: SquadCombatSpread.Tight,
                desiredAggression: 0.2f,
                regroupRequested: true,
                retreatRequested: false,
                reason: "Travel and formation: passive form up");
            return true;
        }

        decision = default;
        return false;
    }

    private bool ShouldRetreat()
    {
        bool retreatLatched = _blackboard.TacticalMode == SquadTacticalMode.Retreat && Time.time - _lastRetreatStartTime < _minimumRetreatDuration;
        if (retreatLatched)
            return true;

        if (_blackboard.CombatCapableBoidCount > 0 && _blackboard.CombatCapableBoidCount <= _retreatMinCombatCapableBoids)
            return true;

        if (_blackboard.AverageHullPercent <= _retreatAverageHullThreshold)
            return true;

        return _blackboard.HostilePressureScore > GetSquadStrengthEstimate() * _retreatPressureMultiplier;
    }

    private bool ShouldRegroup()
    {
        int outOfEnvelopeThreshold = Mathf.Max(1, Mathf.CeilToInt(_blackboard.AliveBoidCount * _regroupOutOfEnvelopeFractionThreshold));
        float leashThreshold = GetLeashThreshold();

        if (_regroupLatched)
        {
            bool recoveredCohesion = _blackboard.CohesionScore >= _regroupCohesionExitThreshold;
            bool recoveredEnvelope = _blackboard.OutOfEnvelopeBoidCount == 0;
            bool recoveredAnchor = leashThreshold <= 0f || _blackboard.AverageAnchorDistance <= leashThreshold * 0.8f;

            if (recoveredCohesion && recoveredEnvelope && recoveredAnchor)
            {
                _regroupLatched = false;
            }
            else
            {
                return true;
            }
        }

        if (_blackboard.CohesionScore < _regroupCohesionEnterThreshold)
        {
            _regroupLatched = true;
            return true;
        }

        if (_blackboard.OutOfEnvelopeBoidCount >= outOfEnvelopeThreshold)
        {
            _regroupLatched = true;
            return true;
        }

        if (leashThreshold > 0f && _blackboard.AverageAnchorDistance > leashThreshold)
        {
            _regroupLatched = true;
            return true;
        }

        return false;
    }

    private float GetSquadStrengthEstimate()
    {
        float hullFactor = Mathf.Max(0.25f, _blackboard.AverageHullPercent);
        return Mathf.Max(1f, _blackboard.CombatCapableBoidCount * hullFactor);
    }

    private float GetLeashThreshold()
    {
        if (_boidsManager == null || _boidsManager.settings == null)
            return 0f;

        return Mathf.Max(0f, _boidsManager.settings.combatLeashRadius * _regroupAnchorDistanceMultiplier);
    }

    private CombatAnchorMode GetRegroupAnchorMode()
    {
        if (_blackboard.CurrentCommandAnchor != null)
            return CombatAnchorMode.CommandAnchor;

        return CombatAnchorMode.FlockCenter;
    }

    private CombatAnchorMode GetRetreatAnchorMode()
    {
        if (_blackboard.CurrentCommandAnchor != null)
            return CombatAnchorMode.CommandAnchor;

        return CombatAnchorMode.FlockCenter;
    }

    private TacticalDecision CreateDecision(
        SquadTacticalMode tacticalMode,
        bool desiredFormationUsage,
        CombatAnchorMode desiredAnchorMode,
        Transform desiredFocusTarget,
        SquadCombatSpread desiredCombatSpread,
        float desiredAggression,
        bool regroupRequested,
        bool retreatRequested,
        string reason)
    {
        return new TacticalDecision
        {
            TacticalMode = tacticalMode,
            DesiredFormationUsage = desiredFormationUsage,
            DesiredAnchorMode = desiredAnchorMode,
            DesiredFocusTarget = desiredFocusTarget,
            DesiredCombatSpread = desiredCombatSpread,
            DesiredAggression = desiredAggression,
            RegroupRequested = regroupRequested,
            RetreatRequested = retreatRequested,
            Reason = reason
        };
    }

    private void PublishDecision(TacticalDecision decision)
    {
        _blackboard.SetTacticalIntent(
            decision.TacticalMode,
            decision.DesiredFormationUsage,
            decision.DesiredAnchorMode,
            decision.DesiredFocusTarget,
            decision.DesiredCombatSpread,
            decision.DesiredAggression,
            decision.RegroupRequested,
            decision.RetreatRequested,
            decision.Reason);

        bool decisionChanged = !_hasDecision || !AreDecisionsEquivalent(decision, _lastDecision);

        _lastDecision = decision;
        _hasDecision = true;

        if (_debugMode && (!_logDecisionChangesOnly || decisionChanged))
        {
            Debug.Log($"[{name}] Tactical decision changed: {BuildDecisionSummary(decision)} | Reason: {decision.Reason}");
        }
    }

    private static bool AreDecisionsEquivalent(TacticalDecision left, TacticalDecision right)
    {
        return left.TacticalMode == right.TacticalMode &&
               left.DesiredFormationUsage == right.DesiredFormationUsage &&
               left.DesiredAnchorMode == right.DesiredAnchorMode &&
               left.DesiredFocusTarget == right.DesiredFocusTarget &&
               left.DesiredCombatSpread == right.DesiredCombatSpread &&
               Mathf.Approximately(left.DesiredAggression, right.DesiredAggression) &&
               left.RegroupRequested == right.RegroupRequested &&
               left.RetreatRequested == right.RetreatRequested &&
               left.Reason == right.Reason;
    }

    private static string BuildDecisionSummary(TacticalDecision decision)
    {
        string focusName = decision.DesiredFocusTarget != null ? decision.DesiredFocusTarget.name : "none";
        return $"Mode={decision.TacticalMode} Anchor={decision.DesiredAnchorMode} Focus={focusName} Spread={decision.DesiredCombatSpread} Aggro={decision.DesiredAggression:0.00} Regroup={decision.RegroupRequested} Retreat={decision.RetreatRequested}";
    }

    private Color GetModeColor(SquadTacticalMode mode)
    {
        switch (mode)
        {
            case SquadTacticalMode.Attack:
                return new Color(1f, 0.35f, 0.35f, 0.9f);
            case SquadTacticalMode.Defend:
                return new Color(1f, 0.8f, 0.2f, 0.9f);
            case SquadTacticalMode.Regroup:
                return new Color(0.2f, 0.9f, 1f, 0.9f);
            case SquadTacticalMode.Retreat:
                return new Color(1f, 0.2f, 1f, 0.9f);
            case SquadTacticalMode.Follow:
            case SquadTacticalMode.Travel:
                return new Color(0.35f, 1f, 0.35f, 0.9f);
            case SquadTacticalMode.FormUp:
                return new Color(0.3f, 0.6f, 1f, 0.9f);
            case SquadTacticalMode.Hold:
                return new Color(1f, 1f, 1f, 0.9f);
            case SquadTacticalMode.ReturnToBase:
                return new Color(0.8f, 0.5f, 1f, 0.9f);
            default:
                return new Color(0.7f, 0.7f, 0.7f, 0.8f);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!_drawDebugGizmos)
            return;

        BoidSquadBlackboard debugBlackboard = _blackboard;
        if (debugBlackboard == null)
            debugBlackboard = GetComponent<BoidSquadBlackboard>();

        if (debugBlackboard == null)
            return;

        Vector3 center = debugBlackboard.FlockCenter != Vector3.zero ? debugBlackboard.FlockCenter : transform.position;
        Color modeColor = GetModeColor(debugBlackboard.TacticalMode);

        Gizmos.color = modeColor;
        Gizmos.DrawWireSphere(center, 45f);

        if (debugBlackboard.CurrentCommandAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(center, debugBlackboard.CurrentCommandAnchor.position);
            Gizmos.DrawWireSphere(debugBlackboard.CurrentCommandAnchor.position, 20f);
        }

        if (debugBlackboard.DesiredFocusTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(center, debugBlackboard.DesiredFocusTarget.position);
            Gizmos.DrawWireSphere(debugBlackboard.DesiredFocusTarget.position, 25f);
        }
        else if (debugBlackboard.PrimaryKnownThreat != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.85f);
            Gizmos.DrawLine(center, debugBlackboard.PrimaryKnownThreat.position);
            Gizmos.DrawWireSphere(debugBlackboard.PrimaryKnownThreat.position, 18f);
        }

        if (debugBlackboard.DefendAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(debugBlackboard.DefendAnchor.position, 30f);
        }

        string focusName = debugBlackboard.DesiredFocusTarget != null ? debugBlackboard.DesiredFocusTarget.name : "none";
        string label =
            $"Mode: {debugBlackboard.TacticalMode}\n" +
            $"Observed: {debugBlackboard.ObservedRuntimeMode}  Cmd: {debugBlackboard.ActiveCommandType}\n" +
            $"Focus: {focusName}  Spread: {debugBlackboard.DesiredCombatSpread}  Aggro: {debugBlackboard.DesiredAggression:0.00}\n" +
            $"Cohesion: {debugBlackboard.CohesionScore:0.00}  Hull: {debugBlackboard.AverageHullPercent:0.00}  Pressure: {debugBlackboard.HostilePressureScore:0.00}\n" +
            $"AnchorDist: {debugBlackboard.AverageAnchorDistance:0.0}  OutOfEnvelope: {debugBlackboard.OutOfEnvelopeBoidCount}\n" +
            $"Regroup: {debugBlackboard.RegroupRequested}  Retreat: {debugBlackboard.RetreatRequested}\n" +
            $"Reason: {debugBlackboard.LastDecisionReason}";

        Handles.color = modeColor;
        Handles.Label(center + Vector3.up * _debugLabelHeight, label);
    }
#endif
}
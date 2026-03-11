using UnityEngine;

public enum SquadTacticalMode
{
    Idle,
    Travel,
    Follow,
    FormUp,
    Attack,
    Defend,
    Regroup,
    Retreat,
    Hold,
    ReturnToBase
}

public enum SquadCombatSpread
{
    Tight,
    Medium,
    Wide
}

public class BoidSquadBlackboard : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string _squadId;
    [SerializeField] private GlobalHelper.Team _team = GlobalHelper.Team.Neutral;

    [Header("Observed Runtime State")]
    [SerializeField] private SquadTacticalMode _observedRuntimeMode = SquadTacticalMode.Idle;
    [SerializeField] private BoidCommandType _activeCommandType = BoidCommandType.None;
    [SerializeField] private Transform _activeCommandTarget;
    [SerializeField] private Vector3 _activeCommandPosition;
    [SerializeField] private float _activeCommandRadius;
    [SerializeField] private float _timeSinceLastCommand = -1f;
    [SerializeField] private Transform _currentCommandAnchor;
    [SerializeField] private Transform _defendAnchor;
    [SerializeField] private int _aliveBoidCount;
    [SerializeField] private int _combatCapableBoidCount;
    [SerializeField] private Vector3 _flockCenter;
    [SerializeField] private int _knownTargetCount;
    [SerializeField] private Transform _primaryKnownThreat;
    [SerializeField] private float _nearestThreatDistance = -1f;
    [SerializeField] private float _hostilePressureScore;
    [SerializeField] private Transform _priorityTarget;
    [SerializeField] private bool _defenseModeActive;
    [SerializeField] private bool _defenseIntrusionDetected;
    [SerializeField] private float _cohesionScore = 1f;
    [SerializeField] private float _averageHullPercent = 1f;
    [SerializeField] private float _averageAnchorDistance;
    [SerializeField] private int _outOfEnvelopeBoidCount;

    [Header("Future Tactical Intent")]
    [SerializeField] private SquadTacticalMode _tacticalMode = SquadTacticalMode.Idle;
    [SerializeField] private float _timeInCurrentTacticalMode;
    [SerializeField] private bool _desiredFormationUsage = true;
    [SerializeField] private CombatAnchorMode _desiredAnchorMode = CombatAnchorMode.Leader;
    [SerializeField] private Transform _desiredFocusTarget;
    [SerializeField] private SquadCombatSpread _desiredCombatSpread = SquadCombatSpread.Medium;
    [SerializeField] private float _desiredAggression = 0.5f;
    [SerializeField] private bool _regroupRequested;
    [SerializeField] private bool _retreatRequested;
    [SerializeField] private string _lastDecisionReason;
    [SerializeField] private float _lastDecisionTime = -1f;

    public string SquadId => _squadId;
    public GlobalHelper.Team Team => _team;
    public SquadTacticalMode ObservedRuntimeMode => _observedRuntimeMode;
    public BoidCommandType ActiveCommandType => _activeCommandType;
    public Transform ActiveCommandTarget => _activeCommandTarget;
    public Vector3 ActiveCommandPosition => _activeCommandPosition;
    public float ActiveCommandRadius => _activeCommandRadius;
    public float TimeSinceLastCommand => _timeSinceLastCommand;
    public Transform CurrentCommandAnchor => _currentCommandAnchor;
    public Transform DefendAnchor => _defendAnchor;
    public int AliveBoidCount => _aliveBoidCount;
    public int CombatCapableBoidCount => _combatCapableBoidCount;
    public Vector3 FlockCenter => _flockCenter;
    public int KnownTargetCount => _knownTargetCount;
    public Transform PrimaryKnownThreat => _primaryKnownThreat;
    public float NearestThreatDistance => _nearestThreatDistance;
    public float HostilePressureScore => _hostilePressureScore;
    public Transform PriorityTarget => _priorityTarget;
    public bool DefenseModeActive => _defenseModeActive;
    public bool DefenseIntrusionDetected => _defenseIntrusionDetected;
    public float CohesionScore => _cohesionScore;
    public float AverageHullPercent => _averageHullPercent;
    public float AverageAnchorDistance => _averageAnchorDistance;
    public int OutOfEnvelopeBoidCount => _outOfEnvelopeBoidCount;
    public SquadTacticalMode TacticalMode => _tacticalMode;
    public float TimeInCurrentTacticalMode => _timeInCurrentTacticalMode;
    public bool DesiredFormationUsage => _desiredFormationUsage;
    public CombatAnchorMode DesiredAnchorMode => _desiredAnchorMode;
    public Transform DesiredFocusTarget => _desiredFocusTarget;
    public SquadCombatSpread DesiredCombatSpread => _desiredCombatSpread;
    public float DesiredAggression => _desiredAggression;
    public bool RegroupRequested => _regroupRequested;
    public bool RetreatRequested => _retreatRequested;
    public string LastDecisionReason => _lastDecisionReason;
    public float LastDecisionTime => _lastDecisionTime;

    public void Initialize(string squadId, GlobalHelper.Team team, bool desiredFormationUsage, CombatAnchorMode desiredAnchorMode)
    {
        _squadId = squadId;
        _team = team;
        _desiredFormationUsage = desiredFormationUsage;
        _desiredAnchorMode = desiredAnchorMode;
    }

    public void PublishCommandState(BoidCommandType commandType, Transform commandTarget, Vector3 commandPosition, float commandRadius, float lastCommandIssuedTime)
    {
        _activeCommandType = commandType;
        _activeCommandTarget = commandTarget;
        _activeCommandPosition = commandPosition;
        _activeCommandRadius = commandRadius;
        _timeSinceLastCommand = lastCommandIssuedTime >= 0f ? Time.time - lastCommandIssuedTime : -1f;
        _observedRuntimeMode = MapObservedRuntimeMode(commandType);
    }

    public void PublishTargetState(Transform commandAnchor, Transform defendAnchor, Transform priorityTarget, bool defenseModeActive, bool defenseIntrusionDetected, int knownTargetCount, Transform primaryKnownThreat, float nearestThreatDistance, float hostilePressureScore, Vector3 flockCenter)
    {
        _currentCommandAnchor = commandAnchor;
        _defendAnchor = defendAnchor;
        _priorityTarget = priorityTarget;
        _defenseModeActive = defenseModeActive;
        _defenseIntrusionDetected = defenseIntrusionDetected;
        _knownTargetCount = knownTargetCount;
        _primaryKnownThreat = primaryKnownThreat;
        _nearestThreatDistance = nearestThreatDistance;
        _hostilePressureScore = hostilePressureScore;
        _flockCenter = flockCenter;
    }

    public void PublishSquadMetrics(int aliveBoidCount, int combatCapableBoidCount, float cohesionScore, float averageHullPercent, float averageAnchorDistance, int outOfEnvelopeBoidCount)
    {
        _aliveBoidCount = aliveBoidCount;
        _combatCapableBoidCount = combatCapableBoidCount;
        _cohesionScore = cohesionScore;
        _averageHullPercent = averageHullPercent;
        _averageAnchorDistance = averageAnchorDistance;
        _outOfEnvelopeBoidCount = outOfEnvelopeBoidCount;
    }

    public void SetTacticalIntent(SquadTacticalMode tacticalMode, bool desiredFormationUsage, CombatAnchorMode desiredAnchorMode, Transform desiredFocusTarget, SquadCombatSpread desiredCombatSpread, float desiredAggression, bool regroupRequested, bool retreatRequested, string reason)
    {
        if (_tacticalMode != tacticalMode)
        {
            _timeInCurrentTacticalMode = 0f;
        }
        else
        {
            _timeInCurrentTacticalMode += Time.deltaTime;
        }

        _tacticalMode = tacticalMode;
        _desiredFormationUsage = desiredFormationUsage;
        _desiredAnchorMode = desiredAnchorMode;
        _desiredFocusTarget = desiredFocusTarget;
        _desiredCombatSpread = desiredCombatSpread;
        _desiredAggression = Mathf.Clamp01(desiredAggression);
        _regroupRequested = regroupRequested;
        _retreatRequested = retreatRequested;
        _lastDecisionReason = reason;
        _lastDecisionTime = Time.time;

        if (_activeCommandType == BoidCommandType.None)
        {
            _observedRuntimeMode = tacticalMode;
        }
    }

    private static SquadTacticalMode MapObservedRuntimeMode(BoidCommandType commandType)
    {
        switch (commandType)
        {
            case BoidCommandType.FollowTarget:
                return SquadTacticalMode.Follow;
            case BoidCommandType.AttackTarget:
            case BoidCommandType.BreakFormation:
                return SquadTacticalMode.Attack;
            case BoidCommandType.MoveToPosition:
                return SquadTacticalMode.Travel;
            case BoidCommandType.ReturnToBase:
                return SquadTacticalMode.ReturnToBase;
            case BoidCommandType.FormUp:
                return SquadTacticalMode.FormUp;
            case BoidCommandType.Defend:
                return SquadTacticalMode.Defend;
            case BoidCommandType.Hold:
                return SquadTacticalMode.Hold;
            default:
                return SquadTacticalMode.Idle;
        }
    }
}
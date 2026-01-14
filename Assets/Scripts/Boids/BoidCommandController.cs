// BoidCommandController.cs - Manages commands for a flock
using UnityEngine;
using System;

public class BoidCommandController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoidsManager _boidsManager;

    [Header("Follow Settings")]
    [SerializeField] private float _followDistance = 150f;
    [SerializeField] private float _followSpread = 100f;
    [SerializeField] private bool _matchTargetSpeed = true;
    [SerializeField] private float _maxFollowSpeed = 200f;

    [Header("Current State")]
    [SerializeField] private BoidCommandType _currentCommandType;
    [SerializeField] private Transform _currentTarget;

    private BoidCommand _currentCommand;

    public BoidCommand CurrentCommand => _currentCommand;
    public BoidCommandType CurrentCommandType => _currentCommand?.Type ?? BoidCommandType.None;

    public event Action<BoidCommand> OnCommandChanged;

    void Awake()
    {
        if (_boidsManager == null)
            _boidsManager = GetComponent<BoidsManager>();
    }

    void Update()
    {
        if (_currentCommand == null) return;

        // Check for expired commands
        if (_currentCommand.IsExpired)
        {
            ClearCommand();
            return;
        }

        // Update debug display
        _currentCommandType = _currentCommand.Type;
        _currentTarget = _currentCommand.Target;

        // Execute current command
        ExecuteCommand();
    }

    private void ExecuteCommand()
    {
        switch (_currentCommand.Type)
        {
            case BoidCommandType.FollowTarget:
                ExecuteFollowTarget();
                break;

            case BoidCommandType.AttackTarget:
                ExecuteAttackTarget();
                break;

            case BoidCommandType.MoveToPosition:
                ExecuteMoveToPosition();
                break;

            case BoidCommandType.Defend:
                ExecuteDefend();
                break;

            case BoidCommandType.FormUp:
                ExecuteFormUp();
                break;

            case BoidCommandType.BreakFormation:
                ExecuteBreakFormation();
                break;

            case BoidCommandType.Hold:
                ExecuteHold();
                break;
        }
    }

    #region Command Execution

    private void ExecuteFollowTarget()
    {
        if (_currentCommand.Target == null)
        {
            ClearCommand();
            return;
        }

        // Set the flock's target to the follow target
        _boidsManager.SetTarget(_currentCommand.Target);

        // Enable formation mode for organized following
        if (!_boidsManager.settings.useFormation)
        {
            _boidsManager.SetUseFormation(true);
        }

        // Optionally match target speed
        if (_matchTargetSpeed)
        {
            var targetRigidbody = _currentCommand.Target.GetComponent<Rigidbody>();
            if (targetRigidbody != null)
            {
                float targetSpeed = targetRigidbody.linearVelocity.magnitude;
                float adjustedSpeed = Mathf.Min(targetSpeed * 1.1f, _maxFollowSpeed);

                // Apply speed adjustment to boids
                foreach (var boid in _boidsManager.Boids)
                {
                    if (boid != null)
                    {
                        boid.SetTargetSpeed(adjustedSpeed);
                    }
                }
            }
        }
    }

    private void ExecuteAttackTarget()
    {
        if (_currentCommand.Target == null)
        {
            ClearCommand();
            return;
        }

        // Set as priority target for the flock
        var targetManager = _boidsManager.TargetManager;
        if (targetManager != null)
        {
            targetManager.SetPriorityTarget(_currentCommand.Target);
        }

        // Break formation for attack
        _boidsManager.SetUseFormation(false);

        // Enter combat mode
        foreach (var boid in _boidsManager.Boids)
        {
            if (boid != null)
            {
                boid.EnterCombat();
                boid.SetPriorityTarget(_currentCommand.Target);
            }
        }
    }

    private void ExecuteMoveToPosition()
    {
        // Create a temporary target at the position or update existing
        // This could use a pooled transform or a dedicated waypoint system

        foreach (var boid in _boidsManager.Boids)
        {
            if (boid == null) continue;

            float distToTarget = Vector3.Distance(boid.position, _currentCommand.Position);

            if (distToTarget < _currentCommand.Radius)
            {
                // Arrived - could switch to patrol or hold
                boid.SetTargetSpeed(0f);
            }
            else
            {
                boid.SetMoveTarget(_currentCommand.Position);
            }
        }
    }

    private void ExecuteDefend()
    {
        if (_currentCommand.Target == null)
        {
            ClearCommand();
            return;
        }

        // Stay near the defended target
        _boidsManager.SetTarget(_currentCommand.Target);
        _boidsManager.SetUseFormation(true);

        // But engage enemies that get too close
        var targetManager = _boidsManager.TargetManager;
        if (targetManager != null)
        {
            targetManager.SetDefenseMode(_currentCommand.Target, _currentCommand.Radius);
        }
    }

    private void ExecuteFormUp()
    {
        _boidsManager.SetUseFormation(true);
        _boidsManager.ForceFormationMode();

        // Tighten formation spacing temporarily
        // Could modify formation settings here
    }

    private void ExecuteBreakFormation()
    {
        _boidsManager.SetUseFormation(false);

        // Clear defense mode so boids can attack any detected enemy
        var targetManager = _boidsManager.TargetManager;
        _boidsManager.SetTarget(null);
        if (targetManager != null)
        {
            targetManager.ClearDefenseMode();
        }

        // Enter combat mode to enable aggressive behavior
        foreach (var boid in _boidsManager.Boids)
        {
            if (boid != null)
            {
                boid.ClearPriorityTarget(); // Let target manager assign
                boid.EnterCombat();
            }
        }
    }

    private void ExecuteHold()
    {
        foreach (var boid in _boidsManager.Boids)
        {
            if (boid != null)
            {
                boid.SetTargetSpeed(0f);
                boid.HoldPosition();
            }
        }
    }

    #endregion

    #region Public Command Interface

    public void IssueCommand(BoidCommand command)
    {
        _currentCommand = command;
        OnCommandChanged?.Invoke(command);

        Debug.Log($"[{_boidsManager.name}] Command issued: {command.Type}");
    }

    public void FollowPlayer()
    {
        var player = FindPlayerShip();
        if (player != null)
        {
            FollowTarget(player);
        }
        else
        {
            Debug.LogWarning("No player ship found!");
        }
    }

    public void FollowTarget(Transform target)
    {
        IssueCommand(BoidCommand.Follow(target));
    }

    public void AttackTarget(Transform target)
    {
        IssueCommand(BoidCommand.Attack(target));
    }

    public void MoveTo(Vector3 position, float arrivalRadius = 50f)
    {
        IssueCommand(BoidCommand.MoveTo(position, arrivalRadius));
    }

    public void DefendTarget(Transform target, float radius = 200f)
    {
        IssueCommand(BoidCommand.Defend(target, radius));
    }

    public void FormUp()
    {
        IssueCommand(new BoidCommand(BoidCommandType.FormUp));
    }

    public void BreakAndEngage()
    {
        IssueCommand(new BoidCommand(BoidCommandType.BreakFormation));
    }

    public void HoldPosition(float duration = 0f)
    {
        IssueCommand(BoidCommand.Hold(duration));
    }

    public void ClearCommand()
    {
        _currentCommand = null;
        _currentCommandType = BoidCommandType.None;
        _currentTarget = null;

        // Reset to default behavior
        foreach (var boid in _boidsManager.Boids)
        {
            if (boid != null)
            {
                boid.ClearTargetSpeed();
                boid.ClearMoveTarget();
            }
        }

        // Reset target manager state
        var targetManager = _boidsManager.TargetManager;
        if (targetManager != null)
        {
            targetManager.ClearPriorityTarget();
            targetManager.ClearDefenseMode();
        }

        OnCommandChanged?.Invoke(null);
    }

    #endregion

    #region Helpers

    private Transform FindPlayerShip()
    {
        // Option 1: Find by tag
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) return playerObj.transform;

        // Option 3: Use a static reference if you have one
        // return GameManager.Instance?.PlayerShip?.transform;

        return null;
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        if (_currentCommand == null) return;

        switch (_currentCommand.Type)
        {
            case BoidCommandType.FollowTarget:
                if (_currentCommand.Target != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(_currentCommand.Target.position, _followDistance);
                    Gizmos.DrawLine(transform.position, _currentCommand.Target.position);
                }
                break;

            case BoidCommandType.MoveToPosition:
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(_currentCommand.Position, _currentCommand.Radius);
                break;

            case BoidCommandType.Defend:
                if (_currentCommand.Target != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(_currentCommand.Target.position, _currentCommand.Radius);
                }
                break;
        }
    }

    #endregion
}
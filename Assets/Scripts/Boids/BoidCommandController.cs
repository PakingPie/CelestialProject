// BoidCommandController.cs - Manages commands for a flock
using UnityEngine;
using System;
using System.Collections.Generic;

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

    private int _returnToBasePendingCount = 0;
    private bool _isReturningToBase = false;

    private BoidCommand _currentCommand;

    public BoidCommand CurrentCommand => _currentCommand;
    public BoidCommandType CurrentCommandType => _currentCommand?.Type ?? BoidCommandType.None;

    public event Action<BoidCommand> OnCommandChanged;

    private Queue<Boid> _pendingDestroy = new Queue<Boid>();

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

        UpdateCommandDebugState();

        // Execute current command
        ExecuteCommand();
    }

    void LateUpdate()
    {
        while (_pendingDestroy.Count > 0)
        {
            Boid boid = _pendingDestroy.Dequeue();
            if (boid != null)
            {
                _boidsManager.RemoveBoid(boid);
                GameObject.Destroy(boid.gameObject);
            }
        }
    }

    private void UpdateCommandDebugState()
    {
        _currentCommandType = _currentCommand?.Type ?? BoidCommandType.None;
        _currentTarget = _currentCommand?.Target;
    }

    private void ForEachBoid(Action<Boid> action)
    {
        var boids = _boidsManager.Boids;
        for (int i = 0; i < boids.Count; i++)
        {
            Boid boid = boids[i];
            if (boid != null)
            {
                action(boid);
            }
        }
    }

    private void ClearBoidOverrides(bool clearPriorityTarget)
    {
        ForEachBoid(boid =>
        {
            boid.ClearTargetSpeed();
            boid.ClearMoveTarget();

            if (clearPriorityTarget)
            {
                boid.ClearPriorityTarget();
            }
        });
    }

    private void ClearTargetManagerOverrides()
    {
        var targetManager = _boidsManager.TargetManager;
        if (targetManager == null)
            return;

        targetManager.ClearPriorityTarget();
        targetManager.ClearDefenseMode();
    }

    private bool TryGetCommandTarget(out Transform target)
    {
        target = _currentCommand?.Target;
        if (target != null)
            return true;

        ClearCommand();
        return false;
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

            case BoidCommandType.ReturnToBase:
                ExecuteReturnToBase();
                break;

            case BoidCommandType.Spawn:
                ExecuteSpawn();
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
        if (!TryGetCommandTarget(out Transform target))
            return;

        _boidsManager.SetTarget(target);

        if (!_boidsManager.settings.useFormation)
        {
            _boidsManager.SetUseFormation(true);
        }

        if (_matchTargetSpeed)
        {
            var targetRigidbody = target.GetComponent<Rigidbody>();
            if (targetRigidbody != null)
            {
                float targetSpeed = targetRigidbody.linearVelocity.magnitude;
                float adjustedSpeed = Mathf.Min(targetSpeed * 1.1f, _maxFollowSpeed);

                ForEachBoid(boid => boid.SetTargetSpeed(adjustedSpeed));
            }
        }
    }

    private void ExecuteAttackTarget()
    {
        if (!TryGetCommandTarget(out Transform target))
            return;

        var targetManager = _boidsManager.TargetManager;
        if (targetManager != null)
        {
            targetManager.SetPriorityTarget(target);
        }

        _boidsManager.SetUseFormation(false);

        ForEachBoid(boid => boid.EnterCombat());
    }

    private void ExecuteMoveToPosition()
    {
        Vector3 destination = _currentCommand.Position;
        float arrivalRadius = _currentCommand.Radius;

        ForEachBoid(boid =>
        {
            float distToTarget = Vector3.Distance(boid.position, destination);

            if (distToTarget < arrivalRadius)
            {
                boid.SetTargetSpeed(0f);
            }
            else
            {
                boid.SetMoveTarget(destination);
            }
        });
    }

    private void ExecuteReturnToBase()
    {
        if (_returnToBasePendingCount > 0) return;

        _isReturningToBase = true;

        // Stop any ongoing spawning
        _boidsManager.PauseSpawning();

        _boidsManager.SetUseFormation(false);

        ForEachBoid(boid =>
        {
            if (boid.IsDespawning)
                return;

            _returnToBasePendingCount++;

            Boid capturedBoid = boid;
            boid.BeginDespawn(() => OnBoidReturnedToBase(capturedBoid));
        });

        if (_returnToBasePendingCount == 0)
        {
            _isReturningToBase = false;
            ClearCommand();
        }
    }

    private void OnBoidReturnedToBase(Boid boid)
    {
        if (boid == null) return;

        _returnToBasePendingCount--;

        _pendingDestroy.Enqueue(boid);

        if (_returnToBasePendingCount <= 0)
        {
            Debug.Log($"[{_boidsManager.name}] All boids returned to base");
            _returnToBasePendingCount = 0;
            _isReturningToBase = false;
            ClearCommand();
        }
    }

    private void CancelReturnToBase()
    {
        _isReturningToBase = false;
        _returnToBasePendingCount = 0;

        // Cancel despawn on any boids still flying back
        ForEachBoid(boid =>
        {
            if (boid.IsDespawning)
            {
                boid.CancelDespawn();
            }
        });

        _boidsManager.ResumeSpawning();
    }

    private void ExecuteSpawn()
    {
        // Spawn command is a one-time action, execute once then clear
        int count = (int)_currentCommand.Radius;

        // If count not specified, use default from spawner
        if (count <= 0)
        {
            count = _boidsManager.GetDefaultSpawnCount();
        }

        // Resume spawning (in case it was paused from RTB)
        _boidsManager.ResumeSpawning();

        // Spawn the boids
        _boidsManager.SpawnBoids(count);

        Debug.Log($"[{_boidsManager.name}] Spawning {count} boids");

        // Clear command immediately - spawning is handled by spawner
        ClearCommand();
    }
    private void ExecuteDefend()
    {
        if (!TryGetCommandTarget(out Transform target))
            return;

        _boidsManager.SetTarget(target);
        _boidsManager.SetUseFormation(true);

        var targetManager = _boidsManager.TargetManager;
        if (targetManager != null)
        {
            targetManager.SetDefenseMode(target, _currentCommand.Radius);
        }
    }

    private void ExecuteFormUp()
    {
        _boidsManager.SetUseFormation(true);
        _boidsManager.ForceFormationMode();
    }

    private void ExecuteBreakFormation()
    {
        _boidsManager.SetUseFormation(false);

        _boidsManager.SetTarget(null);
        ClearTargetManagerOverrides();

        ForEachBoid(boid => boid.EnterCombat());
    }

    private void ExecuteHold()
    {
        ForEachBoid(boid =>
        {
            boid.SetTargetSpeed(0f);
            boid.HoldPosition();
        });
    }

    public bool IsReturningToBase => _isReturningToBase;

    #endregion

    #region Public Command Interface

    public void IssueCommand(BoidCommand command)
    {
        if (command == null)
        {
            ClearCommand();
            return;
        }

        if (_isReturningToBase && command.Type != BoidCommandType.ReturnToBase)
        {
            CancelReturnToBase();
        }

        ClearBoidOverrides(clearPriorityTarget: true);
        ClearTargetManagerOverrides();

        _currentCommand = command;
        UpdateCommandDebugState();
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

    public void ReturnToBase()
    {
        IssueCommand(BoidCommand.ReturnToBase());
    }

    public void Spawn(int count = -1)
    {
        // Cancel any RTB in progress
        if (_isReturningToBase)
        {
            CancelReturnToBase();
        }

        IssueCommand(BoidCommand.Spawn(count));
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
        _isReturningToBase = false;
        _returnToBasePendingCount = 0;
        UpdateCommandDebugState();

        ClearBoidOverrides(clearPriorityTarget: true);
        ClearTargetManagerOverrides();

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
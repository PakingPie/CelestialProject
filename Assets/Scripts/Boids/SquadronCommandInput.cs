// SquadronCommandInput.cs - Example input handler
using UnityEngine;
using UnityEngine.InputSystem;

public class SquadronCommandInput : MonoBehaviour
{
    [SerializeField] private BoidCommandController[] _squadrons;
    [SerializeField] private int _activeSquadron = 0;
    
    void Update()
    {
        if (_squadrons == null || _squadrons.Length == 0) return;
        
        var squadron = _squadrons[_activeSquadron];
        if (squadron == null) return;
        
        // F1 - Follow me
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            squadron.FollowPlayer();
            Debug.Log("Squadron: Follow me!");
        }
        
        // F2 - Attack my target
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            var target = GetPlayerTarget();
            if (target != null)
            {
                squadron.AttackTarget(target);
                Debug.Log($"Squadron: Attack {target.name}!");
            }
        }
        
        // F3 - Form up
        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            squadron.FormUp();
            Debug.Log("Squadron: Form up!");
        }
        
        // F4 - Break and attack
        if (Keyboard.current.f4Key.wasPressedThisFrame)
        {
            squadron.BreakAndEngage();
            Debug.Log("Squadron: Break and engage!");
        }
        
        // F5 - Defend me
        if (Keyboard.current.f5Key.wasPressedThisFrame)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                squadron.DefendTarget(player.transform, 300f);
                Debug.Log("Squadron: Defend me!");
            }
        }
        
        // F6 - Hold position
        if (Keyboard.current.f6Key.wasPressedThisFrame)
        {
            squadron.HoldPosition();
            Debug.Log("Squadron: Hold position!");
        }
        
        // Tab - Switch squadron
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            _activeSquadron = (_activeSquadron + 1) % _squadrons.Length;
            Debug.Log($"Active squadron: {_activeSquadron + 1}");
        }
    }
    
    private Transform GetPlayerTarget()
    {
        // Get player's current target from your targeting system
        // var playerTargeting = FindObjectOfType<PlayerTargetingSystem>();
        // return playerTargeting?.CurrentTarget;
        
        return null; // Replace with your targeting system
    }
}
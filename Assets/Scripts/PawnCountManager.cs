using UnityEngine.UI;
using UnityEngine;
using TMPro;
using System;
using System.Linq;

public class PawnCountManager : MonoBehaviour
{
    public TextMeshProUGUI EnemyDestroyedText;
    public TextMeshProUGUI AllyRemainsText;
    public int EnemyRemainingCount = 0;
    public int AlliesRemainingCount = 0;
    public int EnemiesRemainingCount = 0;

    public static Action UpdateEnemyCountAction;
    public static Action UpdateAllyCountAction;

    public int InitEnemyCount = 0;

    private bool isInitialized = false;

    void Start()
    {
        // Reset counts at start of scene
        EnemyRemainingCount = 0;
        AlliesRemainingCount = 0;
        EnemiesRemainingCount = 0;

        // Calculate initial enemy count FIRST
        var BoidSpawners = FindObjectsByType<BoidSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in BoidSpawners)
        {
            InitEnemyCount += spawner.spawnCount;
        }
        EnemiesRemainingCount = InitEnemyCount;

        // Subscribe to actions
        UpdateEnemyCountAction += UpdateEnemyCount;
        UpdateAllyCountAction += UpdateAllyCount;

        // Now update the UI (after EnemiesRemainingCount is set)
        UpdateEnemyCountUI();
        UpdateAllyCount();

        // Mark as initialized
        isInitialized = true;
    }

    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks and errors on scene reload
        UpdateEnemyCountAction -= UpdateEnemyCount;
        UpdateAllyCountAction -= UpdateAllyCount;
    }

    private void UpdateEnemyCountUI()
    {
        EnemyRemainingCount = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None)
            .Count(ev => ev.VehicleFaction == GlobalHelper.Faction.Foe && !ev.IsDying);
        EnemyDestroyedText.text = EnemyRemainingCount.ToString();
    }

    private void UpdateEnemyCount()
    {
        EnemyRemainingCount = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None)
            .Count(ev => ev.VehicleFaction == GlobalHelper.Faction.Foe && !ev.IsDying);
        EnemyDestroyedText.text = EnemyRemainingCount.ToString();

        // Only check game over after initialization and when count actually reaches zero
        if (isInitialized && EnemyRemainingCount <= 0)
        {
            // All enemies destroyed, trigger game over win condition
            var gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager != null)
            {
                gameManager.GameOver();
            }
        }
    }

    private void UpdateAllyCount()
    {
        AlliesRemainingCount = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None)
            .Count(ev => ev.VehicleFaction == GlobalHelper.Faction.Ally && !ev.IsDying);
        AllyRemainsText.text = AlliesRemainingCount.ToString();
    }
}
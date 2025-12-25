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

    void Start()
    {
        // Reset counts at start of scene
        EnemyRemainingCount = 0;
        AlliesRemainingCount = 0;
        EnemiesRemainingCount = 0;

        UpdateEnemyCount();
        UpdateAllyCount();
        UpdateEnemyCountAction += UpdateEnemyCount; // can use =, but += is safer in case of multiple subscribers
        UpdateAllyCountAction += UpdateAllyCount;

        var BoidSpawners = FindObjectsByType<BoidSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in BoidSpawners)
        {
            InitEnemyCount += spawner.spawnCount;
        }
        EnemiesRemainingCount = InitEnemyCount;
    }

    private void UpdateEnemyCount()
    {
        EnemyRemainingCount = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None).Count(ev => ev.VehicleFaction == GlobalHelper.Faction.Foe && !ev.IsDying);
        EnemyDestroyedText.text = EnemyRemainingCount.ToString();

        if(EnemiesRemainingCount <= 0)
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
        AlliesRemainingCount = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None).Count(ev => ev.VehicleFaction == GlobalHelper.Faction.Ally && !ev.IsDying);
         AllyRemainsText.text = AlliesRemainingCount.ToString();
    }
}
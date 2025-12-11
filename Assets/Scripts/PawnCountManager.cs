using UnityEngine.UI;
using UnityEngine;
using TMPro;
using System;
using System.Linq;
public class PawnCountManager : MonoBehaviour
{
    public TextMeshProUGUI EnemyDestroyedText;
    public TextMeshProUGUI AllyRemainsText;
    public static int EnemyRemainingCount = 0;
    public static int AlliesRemainingCount = 0;
    public static int EnemiesRemainingCount = 0;

    public static Action UpdateEnemyCountAction;
    public static Action UpdateAllyCountAction;

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
    }

    private void UpdateEnemyCount()
    {
        EnemyRemainingCount = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None).Count(ev => ev.VehicleFaction == GlobalHelper.Faction.Foe);
        EnemyDestroyedText.text = EnemyRemainingCount.ToString();
    }

    private void UpdateAllyCount()
    {
        AlliesRemainingCount = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None).Count(ev => ev.VehicleFaction == GlobalHelper.Faction.Ally);
         AllyRemainsText.text = AlliesRemainingCount.ToString();
    }
}
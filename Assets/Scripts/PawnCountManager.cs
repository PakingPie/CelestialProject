using UnityEngine.UI;
using UnityEngine;
using TMPro;
public class PawnCountManager : MonoBehaviour
{
    public TextMeshProUGUI EnemyDestroyedText;
    public TextMeshProUGUI AllyRemainsText;
    public static int EnemyDestroyedCount = 0;
    public static int AlliesRemainingCount = 0;
    public static int EnemiesRemainingCount = 0;

    void Start()
    {
        // Reset counts at start of scene
        EnemyDestroyedCount = 0;
        AlliesRemainingCount = 0;
        EnemiesRemainingCount = 0;
    }

    void Update()
    {
        EnemyDestroyedText.text = EnemyDestroyedCount.ToString();
        AllyRemainsText.text = AlliesRemainingCount.ToString();
    }
}
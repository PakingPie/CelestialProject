using UnityEngine;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;


    public GameObject GameHud;
    public GameObject PlayerHud;
    public GameObject PlayerShip;

    [Header("Debug")]
    public bool FreezeGameOnStart = true;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (FreezeGameOnStart)
                Time.timeScale = 0f; // Pause immediately when singleton is created
        }
        else
        {
            Destroy(gameObject);
        }

    }

    void Start()
    {
        // Pause the game at the start
        Time.timeScale = 0.0f;
        GameHud.SetActive(false);
        PlayerHud.SetActive(false);
        PlayerShip.SetActive(false);
    }
    public void StartGame()
    {
        Time.timeScale = 1f;
        GameHud.SetActive(true);
        PlayerHud.SetActive(true);
        PlayerShip.SetActive(true);
    }

    public void PauseGame()
    {
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public void GameOver()
    {
        // Show Game Over UI
        var gameoverEvents = FindAnyObjectByType<GameOverEvents>();
        if (gameoverEvents != null)
        {
            gameoverEvents.ShowGameoverMenu();
        }
        // Time.timeScale = 0f; // Optionally pause the game on game over
        if(PlayerShip.GetComponent<VehicleBase>().HitPoints <= 0)
            PlayerShip.SetActive(false);
    }

    public void QuitGame()
    {
        // If not in editor, quit the application
#if !UNITY_EDITOR
        Application.Quit();
#endif
    }

    public string GetGameoverSummary()
    {
        // Return a summary of the game over state
        // Get how many enemies were destroyed, allies remaining
        var pawnCountManager = FindAnyObjectByType<PawnCountManager>();

        int enemiesDestroyed = pawnCountManager.InitEnemyCount - pawnCountManager.EnemyRemainingCount; // Placeholder, replace with actual logic
        int alliesRemaining = pawnCountManager.AlliesRemainingCount;  // Placeholder, replace with actual logic
        
        return $"Game Over! You destroyed {enemiesDestroyed} enemies and have {alliesRemaining} allies remaining.";
    }
}
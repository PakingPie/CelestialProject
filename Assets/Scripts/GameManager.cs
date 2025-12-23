using UnityEngine;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameObject GameHud;
    public GameObject PlayerHud;
    public GameObject PlayerShip;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = new GameManager();
            Instance = this;
            DontDestroyOnLoad(gameObject);
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

    public void QuitGame()
    {
        // If not in editor, quit the application
#if !UNITY_EDITOR
        Application.Quit();
#endif
    }
}
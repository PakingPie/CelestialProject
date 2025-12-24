using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class MainMenuEvents : MonoBehaviour
{
    private UIDocument uIDocument;
    private Button startGameButton;
    private Button quitGameButton;
    private Button settingsButton;

    private GameManager gameManager;

    private void Awake()
    {
        uIDocument = GetComponent<UIDocument>();
        var root = uIDocument.rootVisualElement;
        startGameButton = root.Q<Button>("StartGameButton");
        startGameButton.RegisterCallback<ClickEvent>(OnStartGameButtonClicked);

        quitGameButton = root.Q<Button>("QuitGameButton");
        quitGameButton.RegisterCallback<ClickEvent>(OnQuitGameButtonClicked);

        settingsButton = root.Q<Button>("SettingsButton");
    }

    private void Start()
    {
        // Get the instance in Start() to ensure GameManager.Awake() has run
        gameManager = GameManager.Instance;

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
            Debug.LogWarning("GameManager.Instance was null, found via FindFirstObjectByType");
        }
    }

    private void OnStartGameButtonClicked(ClickEvent evt)
    {
        Debug.Log("Start Game button clicked!");
        gameManager?.StartGame();
        HideMainMenu();
    }

    private void OnQuitGameButtonClicked(ClickEvent evt)
    {
        Debug.Log("Quit Game button clicked!");
        gameManager?.QuitGame();
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ShowMainMenu();
            gameManager?.PauseGame();
        }
    }

    public void ShowMainMenu()
    {
        uIDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        uIDocument.sortingOrder = 100;
    }

    public void HideMainMenu()
    {
        uIDocument.rootVisualElement.style.display = DisplayStyle.None;
        uIDocument.sortingOrder = -100;
    }
}
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class GameOverEvents : MonoBehaviour
{
    private UIDocument _uIDocument;
    private Button _restartGameButton;
    private Button _quitGameButton;

    private GameManager _gameManager;

    private void Awake()
    {
        _uIDocument = GetComponent<UIDocument>();
        var root = _uIDocument.rootVisualElement;

        _restartGameButton = root.Q<Button>("RestartGameButton");
        _restartGameButton.RegisterCallback<ClickEvent>(OnRestartGameButtonClicked);

        _quitGameButton = root.Q<Button>("QuitGameButton");
        _quitGameButton.RegisterCallback<ClickEvent>(OnQuitGameButtonClicked);
    }

    private void Start()
    {
        // Get the instance in Start() to ensure GameManager.Awake() has run
        _gameManager = GameManager.Instance;

        if (_gameManager == null)
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            Debug.LogWarning("GameManager.Instance was null, found via FindFirstObjectByType");
        }

        // Hide the gameover menu at start if it's visible
        HideGameoverMenu();
    }

    private void OnRestartGameButtonClicked(ClickEvent evt)
    {
        Debug.Log("Restart Game button clicked!");
        _gameManager?.RestartGame();
        HideGameoverMenu();
    }

    private void OnQuitGameButtonClicked(ClickEvent evt)
    {
        Debug.Log("Quit Game button clicked!");
        _gameManager?.QuitGame();
    }

    public void ShowGameoverMenu()
    {
        _uIDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        // Optionally, pause the game
        _gameManager?.PauseGame();
    }

    public void HideGameoverMenu()
    {
        _uIDocument.rootVisualElement.style.display = DisplayStyle.None;
    }
}
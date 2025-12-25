using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class MainMenuEvents : MonoBehaviour
{
    private UIDocument _uIDocument;
    private Button _startGameButton;
    private Button _quitGameButton;
    private Button _settingsButton;
    private Label _instructionLabel;
    private GameManager _gameManager;

    private void Awake()
    {
        _uIDocument = GetComponent<UIDocument>();
        var root = _uIDocument.rootVisualElement;
        _startGameButton = root.Q<Button>("StartGameButton");
        _startGameButton.RegisterCallback<ClickEvent>(OnStartGameButtonClicked);

        _quitGameButton = root.Q<Button>("QuitGameButton");
        _quitGameButton.RegisterCallback<ClickEvent>(OnQuitGameButtonClicked);

        _settingsButton = root.Q<Button>("SettingsButton");

        _instructionLabel = root.Q<Label>("InstructionsLabel");
        _instructionLabel.text = "Instructions:  \n" + 
                "W/S: Pitch \n" + 
                "A/D: Roll \n" + 
                "Q/E: Yaw \n" + 
                "Shift: Accelerates  \n" + 
                "Left Ctil: Deccerlates   \n" + 
                "T: Maunal Control Main Gun \n" + 
                "Switch Ammunition: Click the 'AP'/'HE' Button \n"+
                "Lock Target: Click an enemy on the screen \n" + 
                "Esc: Main Menu";
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
    }

    private void OnStartGameButtonClicked(ClickEvent evt)
    {
        Debug.Log("Start Game button clicked!");
        _gameManager?.StartGame();
        HideMainMenu();
    }

    private void OnQuitGameButtonClicked(ClickEvent evt)
    {
        Debug.Log("Quit Game button clicked!");
        _gameManager?.QuitGame();
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ShowMainMenu();
            _gameManager?.PauseGame();
        }
    }

    public void ShowMainMenu()
    {
        _uIDocument.rootVisualElement.style.display = DisplayStyle.Flex;
    }

    public void HideMainMenu()
    {
        _uIDocument.rootVisualElement.style.display = DisplayStyle.None;
    }
}
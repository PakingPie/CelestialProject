using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;


public class MainMenu : MonoBehaviour
{
    private UIDocument _uIDocument;
    [SerializeField] private GameManager _gameManager;
    private Button _startBn;
    private Button _optionsBn;  
    private Button _exitBn;

    private void Awake()
    {
        _uIDocument = GetComponent<UIDocument>();
        var root = _uIDocument.rootVisualElement;

        _startBn = _uIDocument.rootVisualElement.Q<Button>("StartBn");
        _startBn.RegisterCallback<ClickEvent>(OnStartButtonClicked);

        _optionsBn = _uIDocument.rootVisualElement.Q<Button>("OptionBn");
        _optionsBn.RegisterCallback<ClickEvent>(OnOptionsButtonClicked);

        _exitBn = _uIDocument.rootVisualElement.Q<Button>("ExitBn");
        _exitBn.RegisterCallback<ClickEvent>(OnExitButtonClicked);
    }

    // Update is called once per frame
    private void Start()
    {
        _gameManager = GameManager.Instance;

        while (!_gameManager)
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            Debug.LogWarning("GameManager.Instance was null, found via FindFirstObjectByType");
        }
    }

    private void OnStartButtonClicked(ClickEvent evt)
    {
        Debug.Log("Start Game button clicked!");
        _gameManager?.StartGame();
        HideMainMenu();
    }

    private void OnOptionsButtonClicked(ClickEvent evt)
    {
        Debug.Log("Options button clicked!");
        // Implement options functionality here
    }

    private void OnExitButtonClicked(ClickEvent evt)
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

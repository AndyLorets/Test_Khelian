using UnityEngine;

public enum InputMode
{
    AutoDetect,
    KeyboardPC, 
    TouchMobile 
}

public sealed class GameInputManager : MonoBehaviour, IInputProvider
{
    [SerializeField] private InputMode _inputMode = InputMode.AutoDetect;

    [Header("Providers")]
    [SerializeField] private KeyboardInputProvider _keyboardProvider;
    [SerializeField] private TouchInputProvider _touchProvider;
    
    [SerializeField] private GameObject _touchUIContainer;

    private IInputProvider _activeProvider;

    private void Awake()
    {
        SetupInputMode();
    }

    private void SetupInputMode()
    {
        InputMode modeToUse = _inputMode;

        if (modeToUse == InputMode.AutoDetect)
        {
            if (Application.isMobilePlatform)
                modeToUse = InputMode.TouchMobile;
            else
                modeToUse = InputMode.KeyboardPC;
        }

        if (modeToUse == InputMode.KeyboardPC)
        {
            _activeProvider = _keyboardProvider;
            
            if (_keyboardProvider != null) _keyboardProvider.enabled = true;
            if (_touchProvider != null) _touchProvider.enabled = false;
            
            if (_touchUIContainer != null) _touchUIContainer.SetActive(false); 
        }
        else if (modeToUse == InputMode.TouchMobile)
        {
            _activeProvider = _touchProvider;
            
            if (_keyboardProvider != null) _keyboardProvider.enabled = false;
            if (_touchProvider != null) _touchProvider.enabled = true;
            if (_touchUIContainer != null) _touchUIContainer.SetActive(true); 
        }
    }

    public bool IsJumpPressed => _activeProvider != null && _activeProvider.IsJumpPressed;
    public float GetHorizontalAxis() => _activeProvider != null ? _activeProvider.GetHorizontalAxis() : 0f;
}
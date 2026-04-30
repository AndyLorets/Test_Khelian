using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TouchInputProvider : MonoBehaviour, IInputProvider
{
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;
    [SerializeField] private Button _jumpButton;

    private bool _leftHeld;
    private bool _rightHeld;
    private bool _jumpHeld;

    public bool IsJumpPressed => _jumpHeld;

    public float GetHorizontalAxis()
    {
        float axis = 0f;
        if (_rightHeld) axis += 1f;
        if (_leftHeld) axis -= 1f;
        return axis;
    }

    private void Awake()
    {
        BindHold(_leftButton,  v => _leftHeld  = v);
        BindHold(_rightButton, v => _rightHeld = v);
        BindHold(_jumpButton,  v => _jumpHeld  = v);
    }

    private static void BindHold(Button button, System.Action<bool> setHeld)
    {
        if (button == null) return;

        var trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        AddEntry(trigger, EventTriggerType.PointerDown, _ => setHeld(true));
        AddEntry(trigger, EventTriggerType.PointerUp,   _ => setHeld(false));
        AddEntry(trigger, EventTriggerType.PointerExit, _ => setHeld(false));
    }

    private static void AddEntry(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(callback));
        trigger.triggers.Add(entry);
    }
}

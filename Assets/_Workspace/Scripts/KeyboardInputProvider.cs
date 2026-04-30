using UnityEngine;

public sealed class KeyboardInputProvider : MonoBehaviour, IInputProvider
{
    [SerializeField] private KeyCode _rightKey = KeyCode.D;
    [SerializeField] private KeyCode _leftKey = KeyCode.A;
    [SerializeField] private KeyCode _jumpKey = KeyCode.Space;

    public bool IsJumpPressed => Input.GetKey(_jumpKey);

    public float GetHorizontalAxis()
    {
        float axis = 0f;
        if (Input.GetKey(_rightKey)) axis += 1f;
        if (Input.GetKey(_leftKey)) axis -= 1f;
        return axis;
    }
}

public interface IInputProvider
{
    bool IsJumpPressed { get; }
    float GetHorizontalAxis();
}

using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Configs/PlayerConfig")]
public class PlayerConfigSO : ScriptableObject
{
    [Header("Jump Settings")]
    public float JumpHeight = 2f;
    public float JumpTime = 0.35f;
    public float MaxLinearVelocityInJump = 5f;
    public float TimeToMaxLinearVelocityInJump = 0.4f;
    public float MaxAngularVelocityInJump = 8f;
    public float TimeToMaxAngularVelocityInJump = 0.4f;
    public float VelocityAffectionFactorOnJump = 1f;

    [Header("Movement Settings")]
    [Min(0f)] public float MaxLinearVelocity = 6f;
    [Min(0.01f)] public float TimeToMaxLinearVelocity = 0.2f;
    public float MinAngularVelocity = 2.8f;

    [Header("Ground/Physics")]
    public LayerMask GroundLayer;
    public float CheckGroundTimer = 0.12f;
    public float GroundSlope = 15f;
    public float EdgeStickThreshold = 0.6f;
}
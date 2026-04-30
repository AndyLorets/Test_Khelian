using UnityEngine;
using System.Collections;

public sealed class PlayerMover : MonoBehaviour
{
    private const float inspector_space = 5f;
    private const float default_normal_angle = 90f;
    // Angular velocity of the ball in radians when it stops completely (linear velocity = 0)
    // Needed in order to simulate rolling with slipping when braking from max linear velocity
    private const float min_angular_velocity = 2.8f;

    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private CircleCollider2D _collider;
    
    [Space(inspector_space)]
    [Header("Jump Settings")]
    [Tooltip("Height in units")]
    [Min(0f)]
    [SerializeField] private float _jumpHeight;

    [Tooltip("Time to rich the highest point of jump/to fall from highest point to the ground. In seconds")]
    [Min(0f)]
    [SerializeField] private float _jumpTime;

    [Tooltip("Max linear velocity you can achieve when jumping with initial linear velocity of 0 units/s. In units/s")]
    [Min(0f)]
    [SerializeField] private float _maxLinearVelocityInJump;
    
    [Tooltip("Time to reach max linear velocity in jump when jumping with initial linear velocity of 0 units/s. In seconds")]
    [Min(0f)]
    [SerializeField] private float _timeToMaxLinearVelocityInJump;
    
    [Tooltip("Max angular velocity you can achieve when jumping with initial angular velocity of 0 rad/s. In rad/s")]
    [Min(0f)]
    [SerializeField] private float _maxAngularVelocityInJump;
    
    [Tooltip("Time to reach max angular velocity in jump when jumping with initial linear velocity of 0 units/s. In seconds")]
    [Min(0f)]
    [SerializeField] private float _timeToMaxAngularVelocityInJump;
    
    [Tooltip("How much current POSITIVE velocity on Y axis should dampen default jump impulse velocity. 0 - current POSITIVE velocity isn't taken into account, 1 - default jump impulse velocity is dampen on value of current velocity")]
    [Range(0f, 1f)]
    [SerializeField] private float _velocityAffectionFactorOnJump = 1f;

    [Space(inspector_space)]
    [Header("Horizontal Movement Settings")] 
    [Tooltip("Max linear velocity you can achieve when moving on a horizontal flat surface with initial linear velocity of 0 units/s. In units/s")]
    [Min(0f)]
    [SerializeField] private float _maxLinearVelocity;
    [Min(0f)]
    [SerializeField] private float _maxVerticalVelocity;

    [Tooltip("Time to reach max linear velocity when moving on horizontal flat surface with initial linear velocity of 0 units/s. In seconds")]
    [Min(0f)]
    [SerializeField] private float _timeToMaxLinearVelocity;

    [Space(inspector_space)]
    [Header("Ground Checking Settings")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField, Min(.1f)] private float _checkGroundTimer = .2f;

    [Tooltip("Angle to define surface namely as ground. Surface with incline of 90 degrees is wall parallel to world Y axis. Surface with incline 0 degrees is parallel to world X axis")]
    [Min(0f)]
    [SerializeField] private float _groundSlope;

    private PlayerCollisionManager _playerCollision;
    private IInputListener _inputListener;
    private float _axisInput;
    private float _groundCheckerTime; 

    private float _startGravityScale; 
    private float _linearAccelerationInJump;
    private float _angularAccelerationInJump;
    private float _jumpImpulseVelocity;
    private Vector2 _jumpImpulse;
    private bool _jumpButtonPressed;
    private bool _isReadyToJump = true;
    private float _linearAcceleration;
    private float _verticalAcceleration;
    private Vector2 _linearActiveForce = new Vector2();
    private Vector2 _verticalActiveForce = new Vector2();
    private float _maxAngularVelocity;
    private float _angularAcceleration;

    private float _changerPosX;
    private bool _horizontalMove = true;

    private Collider2D[] _groundCollidersInContact = new Collider2D[1];
    private ContactFilter2D _groundContactFilter = new ContactFilter2D();
    private bool CanJump => _jumpButtonPressed && IsGrounded() && _isReadyToJump && !isStickyMode && !isGrapplingMode;
    private bool VerticaMove => !_horizontalMove && _axisInput > 0 &&_changerPosX > transform.position.x
    || !_horizontalMove && _axisInput < 0 && _changerPosX < transform.position.x;

    public bool isStickyMode { get; set; }
    public bool isGrapplingMode { get; set; }
    private bool OnRayGround => Physics2D.OverlapCircle(transform.position + _checkGroundOffset, check_ground_radius, _groundLayer);
    public Rigidbody2D rb { get { return _rigidbody; } private set { } }

    private const float check_ground_radius = .2f;
    private Vector3 _checkGroundOffset = new Vector2(0, -.2f);
    private void Construct()
    {
        _inputListener = ServiceLocator.Instance.
            GetService<InputListenerProvider>().
            GetInputListener();
        _playerCollision = ServiceLocator.Instance.
            GetService<Player>().GetComponent<PlayerCollisionManager>(); 
    }
    private void Awake()
    {
        Construct();
        CalculateJumpSettings();
        CalculateMovementSettings();
        CalculateGroundCheckingSettings();
    }

    private void Update()
    {
        _jumpButtonPressed = _inputListener.IsJumpButtonPressed;
        _axisInput = _inputListener.PlayerGetHorizontalAxis();
    }

    private void FixedUpdate()
    {
        PrepareToJump();
        
        Move();
        
        if (CanJump)
            Jump();
    }
    private void CalculateJumpSettings()
    {
        float gravity = Mathf.Abs(Physics2D.gravity.y);
        float requiredGravity = (2f * _jumpHeight) / Mathf.Pow(_jumpTime, 2f);
        _rigidbody.gravityScale = requiredGravity / gravity;

        _linearAccelerationInJump = _maxLinearVelocityInJump / _timeToMaxLinearVelocityInJump;
        _angularAccelerationInJump = _maxAngularVelocityInJump / _timeToMaxAngularVelocityInJump;
        _jumpImpulseVelocity = requiredGravity * _jumpTime;
        _startGravityScale = _rigidbody.gravityScale; 
    }

    private void CalculateMovementSettings()
    {
        _linearAcceleration = _maxLinearVelocity / _timeToMaxLinearVelocity;
        _verticalAcceleration = _maxVerticalVelocity / _timeToMaxLinearVelocity; 
        _maxAngularVelocity = _maxLinearVelocity / _collider.radius;
        // We use MinAngularVelocity as initial angular velocity and _timeToMaxLINEARVelocity as
        // Time to reach max ANGULAR velocity in order to desynchronize achievement of max angular and linear velocities
        // This is done in order to add slipping when braking
        _angularAcceleration = (_maxAngularVelocity - min_angular_velocity) / _timeToMaxLinearVelocity;
    }

    private void CalculateGroundCheckingSettings()
    {
        _groundContactFilter.minNormalAngle = default_normal_angle - _groundSlope;
        _groundContactFilter.maxNormalAngle = default_normal_angle + _groundSlope;
        _groundContactFilter.useNormalAngle = true;
        _groundContactFilter.layerMask = _groundLayer;
        _groundContactFilter.useLayerMask = true;
    }
    public void SetVerticalMove(bool horizontalMove, float changerPosX = 0)
    {
        _horizontalMove = horizontalMove;
        _changerPosX = changerPosX;
    }
    public void SetGravityScale(bool state) => _rigidbody.gravityScale = state ? 0 : _startGravityScale;
    private void Move()
    {
        if (isStickyMode && _playerCollision.isTouchingGroundOnStickyMode) 
        {
            MoveOnStickyMode(); 
            return;
        }
        if (IsGrounded())
            MoveOnGround();
        else
            MoveInJump();
    }
    private void MoveInJump()
    {
        if (_axisInput == 0f)
            return;

        if (VerticaMove)
            MoveVertically(_maxVerticalVelocity,
                    _verticalAcceleration,
                    _maxAngularVelocityInJump,
                    _angularAccelerationInJump, 0f);
        else
            MoveHorizontally(_maxLinearVelocityInJump,
                _linearAccelerationInJump,
                _maxAngularVelocityInJump,
                _angularAccelerationInJump, 0f);

    }
    private void MoveOnGround()
    {
        if (_axisInput == 0f)
            return;

        _rigidbody.GetContacts(_groundContactFilter, _groundCollidersInContact);
        float slippingCoefficient = Mathf.Sqrt(_rigidbody.sharedMaterial.friction * _groundCollidersInContact[0].friction);
        float slippingForce = slippingCoefficient * _rigidbody.mass * (_rigidbody.gravityScale * Mathf.Abs(Physics2D.gravity.y));

        if (VerticaMove)
            MoveVertically(_maxVerticalVelocity,
                    _verticalAcceleration,
                    _maxAngularVelocity,
                    _angularAcceleration,
                    slippingForce);
        else
            MoveHorizontally(_maxLinearVelocity,
                _linearAcceleration,
                _maxAngularVelocity,
                _angularAcceleration,
                slippingForce);
    }

    private const float _stickyModeSpeed = 3f;
    private void MoveOnStickyMode()
    {
        if (_jumpButtonPressed && _isReadyToJump) JumpOnStickyMode();

        if (!_isReadyToJump) return; 

        // вычисляем направление движения вдоль поверхности
        Vector2 moveDirection = Vector2.Perpendicular(_playerCollision.groundNormal).normalized;
        moveDirection *= -_axisInput;

        // перемещаем шар вдоль поверхности
        _rigidbody.velocity = (Vector3)moveDirection * _stickyModeSpeed;

        // вращаем шар 
        float rotation = -_axisInput * (_stickyModeSpeed * 200f); 
        _rigidbody.angularVelocity = rotation;
    }
    private void JumpOnStickyMode()
    {
        _isReadyToJump = false;

        float power = _playerCollision.groundNormal.y >= .8f ? _jumpHeight * 3f : _jumpHeight;
        Vector2 dir = Vector2.ClampMagnitude(_playerCollision.groundNormal, 1);
        _rigidbody.AddForce(dir * power, ForceMode2D.Impulse);
    }
    private void MoveHorizontally(float maxLinearVelocity, float defaultLinearAcceleration, float maxAngularVelocity, float defaultAngularAcceleration, float slippingForce)
    {
        ChangeLinearVelocity(maxLinearVelocity, defaultLinearAcceleration, slippingForce);
        ChangeAngularVelocity(maxAngularVelocity, defaultAngularAcceleration, slippingForce);
    }
    private void MoveVertically (float maxVerticalVelocity, float defaultVerticalAcceleration, float maxAngularVelocity, float defaultAngularAcceleration, float slippingForce)
    {
        ChangeVerticalVelocity(maxVerticalVelocity, defaultVerticalAcceleration, slippingForce);
        ChangeAngularVelocity(maxAngularVelocity, defaultAngularAcceleration, slippingForce);
    }
    private void ChangeLinearVelocity(float maxLinearVelocity, float defaultLinearAcceleration, float slippingForce)
    {
        float currentLinearVelocity = _rigidbody.velocity.x;
        
        if (Mathf.Abs(currentLinearVelocity) < maxLinearVelocity ||
            !Mathf.Approximately(Mathf.Sign(currentLinearVelocity), Mathf.Sign(_axisInput)))
        {
            float actualLinearAcceleration = defaultLinearAcceleration;
            
            float predictedLinearVelocity = currentLinearVelocity + (defaultLinearAcceleration * _axisInput) * Time.fixedDeltaTime;
            
            if (Mathf.Abs(predictedLinearVelocity) > maxLinearVelocity && Mathf.Abs(currentLinearVelocity) < maxLinearVelocity)
                actualLinearAcceleration = (maxLinearVelocity - Mathf.Abs(currentLinearVelocity)) / Time.fixedDeltaTime;

            float pureActiveForce = _rigidbody.mass * actualLinearAcceleration;
            float currentActiveForce = isGrapplingMode ? pureActiveForce / 2f : pureActiveForce;
            _linearActiveForce.x = currentActiveForce + slippingForce;
            float axisInput = _axisInput; 
            _linearActiveForce.x *= axisInput;
        
            _rigidbody.AddForce(_linearActiveForce, ForceMode2D.Force);
        }
    }
    private void ChangeVerticalVelocity(float maxVerticalVelocity, float defaultVerticalAcceleration, float slippingForce)
    {
        float currentVerticalVelocity = _rigidbody.velocity.y;

        if (Mathf.Abs(currentVerticalVelocity) < maxVerticalVelocity ||
            !Mathf.Approximately(Mathf.Sign(currentVerticalVelocity), Mathf.Sign(_axisInput)))
        {
            float actualVerticalAcceleration = defaultVerticalAcceleration;

            float predictedVerticalVelocity = currentVerticalVelocity + (defaultVerticalAcceleration * _axisInput) * Time.fixedDeltaTime;

            if (Mathf.Abs(predictedVerticalVelocity) > maxVerticalVelocity && Mathf.Abs(currentVerticalVelocity) < maxVerticalVelocity)
                actualVerticalAcceleration = (maxVerticalVelocity - Mathf.Abs(currentVerticalVelocity)) / Time.fixedDeltaTime;

            float pureActiveForce = _rigidbody.mass * actualVerticalAcceleration;
            float currentActiveForce = isGrapplingMode ? pureActiveForce / 2f : pureActiveForce; 
            _verticalActiveForce.y = currentActiveForce + slippingForce;
            _verticalActiveForce.y *= 1;

            _rigidbody.AddForce(_verticalActiveForce, ForceMode2D.Force);
        }
    }
    private void ChangeAngularVelocity(float maxAngularVelocity, float defaultAngularAcceleration, float slippingForce)
    {
        float currentAngularVelocity = _rigidbody.angularVelocity * Mathf.Deg2Rad;
        // When input equals 1, angular velocity will have clockwise direction with sign -1
        // When input equals -1, angular velocity wil have counterclock-wise direction with sign +1
        // But by default we assume that rotation in clockwise direction comes with sign +1, so we should inverse it:
        float invertedCurrentAngularVelocity = -currentAngularVelocity;
        
        // (Angular acceleration * ballRadius) differs from linear acceleration
        // (angularAcceleration * ballRadius = linear acceleration - condition of rolling without slipping),
        // So we have to track achievement of max values of
        // Both angular and linear velocities separately
        if (Mathf.Abs(currentAngularVelocity) < maxAngularVelocity ||
            !Mathf.Approximately(Mathf.Sign(invertedCurrentAngularVelocity), Mathf.Sign(_axisInput)))
        {
            float actualAngularAcceleration = defaultAngularAcceleration;
            float invertedAngularAcceleration = -defaultAngularAcceleration;
            
            float predictedAngularVelocity = currentAngularVelocity + (invertedAngularAcceleration * _axisInput) * Time.fixedDeltaTime;
            
            if (Mathf.Abs(predictedAngularVelocity) > maxAngularVelocity && Mathf.Abs(currentAngularVelocity) < maxAngularVelocity)
                actualAngularAcceleration = (maxAngularVelocity - Mathf.Abs(currentAngularVelocity)) / Time.fixedDeltaTime;

            float slippingTorque = slippingForce * _collider.radius;
            float pureActiveTorque = _rigidbody.inertia * actualAngularAcceleration;
            float currentActiveForce = isGrapplingMode ? pureActiveTorque / 2f : pureActiveTorque;
            float activeTorque = -(currentActiveForce - slippingTorque);
            activeTorque *= _axisInput;
            _rigidbody.AddTorque(activeTorque, ForceMode2D.Force);
        }
    }
    private bool IsGrounded()
    {
        bool onGround = _rigidbody.IsTouching(_groundContactFilter);
        if (!onGround)
        {
            _groundCheckerTime -= Time.deltaTime;
            if (_groundCheckerTime <= 0)
                return false;
        }
        else _groundCheckerTime = _checkGroundTimer;

        return true; 
    }
    private void PrepareToJump()
    {
        if(!IsGrounded() && !_isReadyToJump)
            _isReadyToJump = true;
    }
    private void Jump()
    {
        float currentVelocityY = _rigidbody.velocity.y;
        float actualJumpImpulseVelocity = _jumpImpulseVelocity;

        if (currentVelocityY > 0 && _jumpImpulseVelocity > currentVelocityY)
        {
            actualJumpImpulseVelocity = _jumpImpulseVelocity - (currentVelocityY * _velocityAffectionFactorOnJump);
        }
        else if (_jumpImpulseVelocity <= currentVelocityY)
        {
            // In case currentVelocityY > _jumpImpulseVelocity we can no longer use the above calculations for actualJumpImpulseVelocity,
            // Because actualJumpImpulseVelocity will always be negative which means _jumpImpulse.y will also be negative,
            // And that's not what we want for the jump, so we should clamp currentVelocityY like this: Mathf.Clamp(currentVelocityY, 0f, _jumpImpulseVelocity)
            // Since we know, that currentVelocityY >= _jumpImpulseVelocity, the result of clamping will always be equal to _jumpImpulseVelocity and
            // _jumpImpulseVelocity - (_jumpImpulseVelocity * _velocityAffectionFactorOnJump) = _jumpImpulseVelocity * (1f - _velocityAffectionFactorOnJump)
            actualJumpImpulseVelocity = _jumpImpulseVelocity * (1f - _velocityAffectionFactorOnJump);
        }

        _jumpImpulse.y = actualJumpImpulseVelocity * _rigidbody.mass; 

        _rigidbody.AddForce(_jumpImpulse, ForceMode2D.Impulse);
        _isReadyToJump = false;

        if (SoundEffectsManager.instance != null)
            SoundEffectsManager.instance.PlaySoundEffect(RepositoryPrefs.Repository.playerJump_sound);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + _checkGroundOffset, check_ground_radius);
    }
}








// [SerializeField] private PlayerMovement _movement; 
// private InputController _inputController;
// public Vector3 respawnPos { private get; set; }
// public float moveValue { get; set; }
//
// private void Awake()
// {
//     _inputController = FindObjectOfType<InputController>();
//     _movement = Instantiate(_movement); 
//     _movement.Init(rb);
// }
//
// private void Start()
// {
//     respawnPos = transform.position;
// }
//
// private void Update()
// {
//     _movement.RunUpdate(); 
// }
//
// private void FixedUpdate()
// {
//     _movement.RunFixedUpdate(); 
// }
//
// public void Movement(float moveHorizontalValue) => _movement.Movement(moveHorizontalValue);
//
// public void Jump() => _movement.Jump();
//
// public IEnumerator AutoRolling(bool state)
// {
//     float t = 0;
//     float maxT = state ? 1 : 2;
//     _inputController.enabled = false; 
//     while (t < maxT)
//     {
//         _movement.Movement(1);
//         t += Time.deltaTime;
//         yield return null;
//     }
//     _movement.Movement(0);
//     gameObject.SetActive(state);
//     _inputController.enabled = state;
// }
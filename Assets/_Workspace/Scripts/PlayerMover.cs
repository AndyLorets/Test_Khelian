using UnityEngine;

public sealed class PlayerMover : MonoBehaviour
{
    private const float min_angular_velocity = 2.8f;

    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private CircleCollider2D _collider;
    [SerializeField] private Collider2D _platform;

    [SerializeField] private MonoBehaviour _inputSource;

    [Space(5)]    
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _jumpTime = 0.35f;
    [SerializeField] private float _maxLinearVelocityInJump = 5f;
    [SerializeField] private float _timeToMaxLinearVelocityInJump = 0.4f;
    [SerializeField] private float _maxAngularVelocityInJump = 8f;
    [SerializeField] private float _timeToMaxAngularVelocityInJump = 0.4f;
    [SerializeField] private float _velocityAffectionFactorOnJump = 1f;

    [Space(5)]
    [Min(0f),SerializeField] private float _maxLinearVelocity = 6f;
    [Min(0.01f), SerializeField] private float _timeToMaxLinearVelocity = 0.2f;

    [Space(5)]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _checkGroundTimer = 0.12f; 
    [SerializeField] private float _groundSlope = 15f;
    [SerializeField] private float _edgeStickThreshold = 0.6f;

    private IInputProvider _inputListener;
    private float _axisInput;
   [SerializeField]  private bool _jumpButtonPressed;
    [SerializeField] private bool _isReadyToJump = true;
    private float _groundCheckerTime;
   [SerializeField]  private bool _isGroundedThisStep;

    private float _gravityForce;
    private float _jumpImpulseVelocity;
    private float _linearAcceleration;
    private float _linearAccelerationInJump;
    private float _maxAngularVelocity;
    private float _angularAcceleration;
    private float _angularAccelerationInJump;

    private Vector2 _gravityVector;
    private Vector2 _linearActiveForce = new Vector2();
    private Vector2 _jumpImpulse;

    private Vector2 _localUp = Vector2.up;
    private Vector2 _localRight = Vector2.right;
    private Edge _currentEdge = Edge.Top;

    private Collider2D[] _groundCollidersInContact = new Collider2D[1];
    private ContactFilter2D _groundContactFilter = new ContactFilter2D();

    private enum Edge { Top, Bottom, Left, Right }

    private bool CanJump => _jumpButtonPressed && _isGroundedThisStep && _isReadyToJump;

    private void Awake()
    {
        _inputListener = _inputSource as IInputProvider;          
        _rigidbody.gravityScale = 0f;

        CalculateJumpSettings();
        CalculateMovementSettings();
        CalculateGroundCheckingSettings();
    }

    private void Update()
    {
        if (_inputListener == null) return;
        _jumpButtonPressed = _inputListener.IsJumpPressed;
        _axisInput = _inputListener.GetHorizontalAxis();
    }

    private void FixedUpdate()
    {
        _isGroundedThisStep = IsGrounded();

        UpdateLocalFrame();
        ApplyGravity();
        PrepareToJump();
        Move();
        if (CanJump) Jump();
    }

    private void CalculateJumpSettings()
    {
        float requiredAccel = (2f * _jumpHeight) / Mathf.Pow(_jumpTime, 2f);
        _gravityForce = requiredAccel * _rigidbody.mass;
        _jumpImpulseVelocity = requiredAccel * _jumpTime;

        _linearAccelerationInJump = _maxLinearVelocityInJump / _timeToMaxLinearVelocityInJump;
        _angularAccelerationInJump = _maxAngularVelocityInJump / _timeToMaxAngularVelocityInJump;
    }

    private void CalculateMovementSettings()
    {
        _linearAcceleration = _maxLinearVelocity / _timeToMaxLinearVelocity;
        _maxAngularVelocity = _maxLinearVelocity / _collider.radius;
        _angularAcceleration = (_maxAngularVelocity - min_angular_velocity) / _timeToMaxLinearVelocity;
    }

    private void CalculateGroundCheckingSettings()
    {
        _groundContactFilter.useNormalAngle = true;
        _groundContactFilter.useLayerMask = true;
        _groundContactFilter.layerMask = _groundLayer;
    }

    private void UpdateLocalFrame()
    {
        if (_platform == null) return;

        Bounds b = _platform.bounds;
        Vector2 pos = _rigidbody.position;

        if (!_isGroundedThisStep)
        {
            float distanceFromSurface = SignedDistanceFromCurrentEdge(pos, b);
            if (distanceFromSurface > _edgeStickThreshold) return;
        }

        Vector2 center = b.center;
        float dx = pos.x - center.x;
        float dy = pos.y - center.y;
        float nx = Mathf.Abs(dx) / Mathf.Max(b.extents.x, 0.0001f);
        float ny = Mathf.Abs(dy) / Mathf.Max(b.extents.y, 0.0001f);

        Edge edge = nx > ny
            ? (dx >= 0f ? Edge.Right : Edge.Left)
            : (dy >= 0f ? Edge.Top : Edge.Bottom);

        switch (edge)
        {
            case Edge.Top:    _localUp = Vector2.up;    _localRight = Vector2.right; break;
            case Edge.Right:  _localUp = Vector2.right; _localRight = Vector2.down;  break;
            case Edge.Bottom: _localUp = Vector2.down;  _localRight = Vector2.left;  break;
            case Edge.Left:   _localUp = Vector2.left;  _localRight = Vector2.up;    break;
        }

        if (edge != _currentEdge)
        {
            _currentEdge = edge;
            RefreshGroundFilterForEdge();
        }
    }

    private float SignedDistanceFromCurrentEdge(Vector2 pos, Bounds platformBounds)
    {
        Vector2 center = platformBounds.center;
        float halfExtentAlongUp = Mathf.Abs(_localUp.x) * platformBounds.extents.x
                                + Mathf.Abs(_localUp.y) * platformBounds.extents.y;
        float projection = Vector2.Dot(pos - center, _localUp);
        return projection - halfExtentAlongUp - _collider.radius;
    }

    private void RefreshGroundFilterForEdge()
    {
        float angle = Mathf.Atan2(_localUp.y, _localUp.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        _groundContactFilter.minNormalAngle = Mathf.Repeat(angle - _groundSlope, 360f);
        _groundContactFilter.maxNormalAngle = Mathf.Repeat(angle + _groundSlope, 360f);
    }

    private void ApplyGravity()
    {
        _gravityVector.x = -_localUp.x * _gravityForce;
        _gravityVector.y = -_localUp.y * _gravityForce;
        _rigidbody.AddForce(_gravityVector, ForceMode2D.Force);
    }

    private void PrepareToJump()
    {
        if (_isGroundedThisStep && !_jumpButtonPressed)
            _isReadyToJump = true;
    }

    private void Move()
    {
        if (Mathf.Approximately(_axisInput, 0f))
            return;

        if (_isGroundedThisStep)
            MoveOnGround();
        else
            MoveInJump();
    }

    private void MoveOnGround()
    {
        _rigidbody.GetContacts(_groundContactFilter, _groundCollidersInContact);
        Collider2D groundCollider = _groundCollidersInContact[0];
        float groundFriction = groundCollider != null ? groundCollider.friction : 0f;
        float ballFriction = _rigidbody.sharedMaterial != null ? _rigidbody.sharedMaterial.friction : 0f;
        float slippingCoefficient = Mathf.Sqrt(ballFriction * groundFriction);
        float slippingForce = slippingCoefficient * _gravityForce;

        ChangeLinearVelocity(_maxLinearVelocity, _linearAcceleration, slippingForce);
        ChangeAngularVelocity(_maxAngularVelocity, _angularAcceleration, slippingForce);
    }

    private void MoveInJump()
    {
        ChangeLinearVelocity(_maxLinearVelocityInJump, _linearAccelerationInJump, 0f);
        ChangeAngularVelocity(_maxAngularVelocityInJump, _angularAccelerationInJump, 0f);
    }

    private void ChangeLinearVelocity(float maxLinearVelocity, float defaultLinearAcceleration, float slippingForce)
    {
        Vector2 velocity = _rigidbody.linearVelocity;
        float currentLinearVelocity = Vector2.Dot(velocity, _localRight);

        if (Mathf.Abs(currentLinearVelocity) < maxLinearVelocity ||
            !Mathf.Approximately(Mathf.Sign(currentLinearVelocity), Mathf.Sign(_axisInput)))
        {
            float actualLinearAcceleration = defaultLinearAcceleration;
            float predictedLinearVelocity = currentLinearVelocity + (defaultLinearAcceleration * _axisInput) * Time.fixedDeltaTime;
            if (Mathf.Abs(predictedLinearVelocity) > maxLinearVelocity && Mathf.Abs(currentLinearVelocity) < maxLinearVelocity)
                actualLinearAcceleration = (maxLinearVelocity - Mathf.Abs(currentLinearVelocity)) / Time.fixedDeltaTime;

            float pureActiveForce = _rigidbody.mass * actualLinearAcceleration;
            float scalarForce = (pureActiveForce + slippingForce) * _axisInput;

            _linearActiveForce.x = _localRight.x * scalarForce;
            _linearActiveForce.y = _localRight.y * scalarForce;
            _rigidbody.AddForce(_linearActiveForce, ForceMode2D.Force);
        }
    }

    private void ChangeAngularVelocity(float maxAngularVelocity, float defaultAngularAcceleration, float slippingForce)
    {
        float currentAngularVelocity = _rigidbody.angularVelocity * Mathf.Deg2Rad;
        float invertedCurrentAngularVelocity = -currentAngularVelocity;

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
            float activeTorque = -(pureActiveTorque - slippingTorque) * _axisInput;
            _rigidbody.AddTorque(activeTorque, ForceMode2D.Force);
        }
    }

    private void Jump()
    {
        Vector2 velocity = _rigidbody.linearVelocity;
        float currentVelocityUp = Vector2.Dot(velocity, _localUp);
        float actualJumpImpulseVelocity = _jumpImpulseVelocity;

        if (currentVelocityUp > 0 && _jumpImpulseVelocity > currentVelocityUp)
        {
            actualJumpImpulseVelocity = _jumpImpulseVelocity - (currentVelocityUp * _velocityAffectionFactorOnJump);
        }
        else if (_jumpImpulseVelocity <= currentVelocityUp)
        {
            actualJumpImpulseVelocity = _jumpImpulseVelocity * (1f - _velocityAffectionFactorOnJump);
        }

        float impulseScalar = actualJumpImpulseVelocity * _rigidbody.mass;
        _jumpImpulse.x = _localUp.x * impulseScalar;
        _jumpImpulse.y = _localUp.y * impulseScalar;
        _rigidbody.AddForce(_jumpImpulse, ForceMode2D.Impulse);

        _isReadyToJump = false;
    }

    private bool IsGrounded()
    {
        bool onGround = _rigidbody.IsTouching(_groundContactFilter);
        if (onGround)
        {
            _groundCheckerTime = _checkGroundTimer;
            return true;
        }

        _groundCheckerTime -= Time.fixedDeltaTime;
        return _groundCheckerTime > 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (_platform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, _platform.bounds.center);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, _localUp * 1.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, _localRight * 1.5f);
    }
}
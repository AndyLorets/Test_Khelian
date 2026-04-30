using UnityEngine;

public sealed class PlayerMover : MonoBehaviour
{
    [SerializeField] private PlayerConfigSO _config;
    [SerializeField] private MonoBehaviour _inputSource;
    private Rigidbody2D _rigidbody;
    private CircleCollider2D _collider;
    private IInputProvider _inputListener;
    private float _axisInput;
    private bool _jumpButtonPressed;
    private bool _isReadyToJump = true;
    private float _groundCheckerTime;
    private bool _isGroundedThisStep;

    private Collider2D _currentPlatform;

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

    private ContactPoint2D[] _contacts = new ContactPoint2D[10];
    private ContactFilter2D _groundContactFilter = new ContactFilter2D();

    private enum Edge { Top, Bottom, Left, Right }

    private bool CanJump => _jumpButtonPressed && _isGroundedThisStep && _isReadyToJump;

    private void Awake()
    {
        _inputListener = _inputSource as IInputProvider;
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<CircleCollider2D>();
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
        UpdateCurrentPlatform(); 
        _isGroundedThisStep = IsGrounded();

        UpdateLocalFrame();
        ApplyGravity();
        PrepareToJump();
        Move();
        if (CanJump) Jump();
    }

    private void CalculateJumpSettings()
    {
        float requiredAccel = (2f * _config.JumpHeight) / Mathf.Pow(_config.JumpTime, 2f);
        _gravityForce = requiredAccel * _rigidbody.mass;
        _jumpImpulseVelocity = requiredAccel * _config.JumpTime;

        _linearAccelerationInJump = _config.MaxLinearVelocityInJump / _config.TimeToMaxLinearVelocityInJump;
        _angularAccelerationInJump = _config.MaxAngularVelocityInJump / _config.TimeToMaxAngularVelocityInJump;
    }

    private void CalculateMovementSettings()
    {
        _linearAcceleration = _config.MaxLinearVelocity / _config.TimeToMaxLinearVelocity;
        _maxAngularVelocity = _config.MaxLinearVelocity / _collider.radius;
        _angularAcceleration = (_maxAngularVelocity - _config.MinAngularVelocity) / _config.TimeToMaxLinearVelocity;
    }

    private void CalculateGroundCheckingSettings()
    {
        _groundContactFilter.useNormalAngle = false;
        _groundContactFilter.useLayerMask = true;
        _groundContactFilter.layerMask = _config.GroundLayer;
    }

    private void UpdateCurrentPlatform()
    {
        int count = _rigidbody.GetContacts(_groundContactFilter, _contacts);
        for (int i = 0; i < count; i++)
        {
            if (_contacts[i].collider != null)
            {
                _currentPlatform = _contacts[i].collider;
                break;
            }
        }
    }

    private void UpdateLocalFrame()
    {
        if (_currentPlatform == null) return; 

        Bounds b = _currentPlatform.bounds;
        Vector2 pos = _rigidbody.position;

        if (!_isGroundedThisStep)
        {
            float distanceFromSurface = SignedDistanceFromCurrentEdge(pos, b);
            if (distanceFromSurface > _config.EdgeStickThreshold) return;
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
            case Edge.Top: _localUp = Vector2.up; _localRight = Vector2.right; break;
            case Edge.Right: _localUp = Vector2.right; _localRight = Vector2.down; break;
            case Edge.Bottom: _localUp = Vector2.down; _localRight = Vector2.left; break;
            case Edge.Left: _localUp = Vector2.left; _localRight = Vector2.up; break;
        }

        if (edge != _currentEdge)
        {
            _currentEdge = edge;
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
        int count = _rigidbody.GetContacts(_groundContactFilter, _contacts);
        Collider2D groundCollider = null;
        float minDot = Mathf.Cos(_config.GroundSlope * Mathf.Deg2Rad);

        for (int i = 0; i < count; i++)
        {
            if (Vector2.Dot(_contacts[i].normal, _localUp) >= minDot)
            {
                groundCollider = _contacts[i].collider;
                break;
            }
        }

        float groundFriction = groundCollider != null ? groundCollider.friction : 0f;
        float ballFriction = _rigidbody.sharedMaterial != null ? _rigidbody.sharedMaterial.friction : 0f;
        float slippingCoefficient = Mathf.Sqrt(ballFriction * groundFriction);
        float slippingForce = slippingCoefficient * _gravityForce;

        ChangeLinearVelocity(_config.MaxLinearVelocity, _linearAcceleration, slippingForce);
        ChangeAngularVelocity(_maxAngularVelocity, _angularAcceleration, slippingForce);
    }

    private void MoveInJump()
    {
        ChangeLinearVelocity(_config.MaxLinearVelocityInJump, _linearAccelerationInJump, 0f);
        ChangeAngularVelocity(_config.MaxAngularVelocityInJump, _angularAccelerationInJump, 0f);
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
            actualJumpImpulseVelocity = _jumpImpulseVelocity - (currentVelocityUp * _config.VelocityAffectionFactorOnJump);
        }
        else if (_jumpImpulseVelocity <= currentVelocityUp)
        {
            actualJumpImpulseVelocity = _jumpImpulseVelocity * (1f - _config.VelocityAffectionFactorOnJump);
        }

        float impulseScalar = actualJumpImpulseVelocity * _rigidbody.mass;
        _jumpImpulse.x = _localUp.x * impulseScalar;
        _jumpImpulse.y = _localUp.y * impulseScalar;
        _rigidbody.AddForce(_jumpImpulse, ForceMode2D.Impulse);

        _isReadyToJump = false;
    }

    private bool IsGrounded()
    {
        int count = _rigidbody.GetContacts(_groundContactFilter, _contacts);
        float minDot = Mathf.Cos(_config.GroundSlope * Mathf.Deg2Rad);
        bool onGround = false;

        for (int i = 0; i < count; i++)
        {
            if (Vector2.Dot(_contacts[i].normal, _localUp) >= minDot)
            {
                onGround = true;
                break;
            }
        }

        if (onGround)
        {
            _groundCheckerTime = _config.CheckGroundTimer;
            return true;
        }

        _groundCheckerTime -= Time.fixedDeltaTime;
        return _groundCheckerTime > 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (_currentPlatform == null) return; 
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, _currentPlatform.bounds.center);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, _localUp * 1.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, _localRight * 1.5f);
    }
}
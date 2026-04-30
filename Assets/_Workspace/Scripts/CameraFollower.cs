using UnityEngine;

public sealed class CameraFollower : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _smoothTime = 0.1f; 
    [SerializeField] private float _zOffset = -10f;

    [Header("Camera Bounds")]
    [SerializeField] private bool _useBounds = true; 
    [SerializeField] private Vector2 _minBounds;   
    [SerializeField] private Vector2 _maxBounds;   

    private Vector3 _currentVelocity;

    private void LateUpdate()
    {
        if (_target == null) return;

        float targetX = _target.position.x;
        float targetY = _target.position.y;

        if (_useBounds)
        {
            targetX = Mathf.Clamp(targetX, _minBounds.x, _maxBounds.x);
            targetY = Mathf.Clamp(targetY, _minBounds.y, _maxBounds.y);
        }

        Vector3 targetPos = new Vector3(targetX, targetY, _zOffset);
        
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            targetPos, 
            ref _currentVelocity, 
            _smoothTime
        );
    }

    private void OnDrawGizmos()
    {
        if (!_useBounds) return;

        Gizmos.color = Color.yellow;

        Vector2 center = (_minBounds + _maxBounds) / 2f;
        Vector2 size = _maxBounds - _minBounds;
        
        Gizmos.DrawWireCube(center, size);
    }
}
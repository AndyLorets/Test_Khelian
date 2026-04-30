using UnityEngine;

public sealed class CameraFollower : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _smoothTime = 0.1f; 
    [SerializeField] private float _zOffset = -10f;

    private Vector3 _currentVelocity;

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 targetPos = new Vector3(_target.position.x, _target.position.y, _zOffset);
        
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            targetPos, 
            ref _currentVelocity, 
            _smoothTime
        );
    }
}
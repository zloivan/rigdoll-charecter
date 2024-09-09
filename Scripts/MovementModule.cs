using _RagDollBaseCharecter.Scripts.Helpers;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts
{
    public class MovementModule : MonoBehaviour
    {
        [SerializeField] private float _rotationSpeed = 10f;
        [SerializeField] private float _gravity = -9.81f;

        [SerializeField]
        private bool _logsEnable;

        
        private CharacterController _characterController;
        private Vector3 _currentVelocity;
        private readonly ILogger _logger = new RagdollLogger();

        public void Init(CharacterController characterController)
        {
            Debug.Assert(characterController != null, "Character controller is null", this);
            
            _characterController = characterController;
        }

        public void Move(Vector2 moveDir, float maxSpeed, float accelerationCoef)
        {
            if (_characterController == null || !_characterController.enabled) return;

            var targetVelocity = new Vector3(moveDir.x, 0, moveDir.y) * maxSpeed;
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, accelerationCoef * Time.deltaTime);

            // Apply gravity
            if (!_characterController.isGrounded)
            {
                _currentVelocity.y += _gravity * Time.deltaTime;
            }
            else if (_currentVelocity.y < 0)
            {
                _currentVelocity.y = -2f;
            }

            _currentVelocity = Vector3.ClampMagnitude(_currentVelocity, maxSpeed);
            
            if (_logsEnable) _logger.Log("MOVEMENT_MODULE", $"Current velocity: {_currentVelocity}");
            
            _characterController.Move(_currentVelocity * Time.deltaTime);
            
            if (!(_currentVelocity.sqrMagnitude > 0.1f)) return;
            
            var lookDirection = new Vector3(_currentVelocity.x, 0, _currentVelocity.z).normalized;
            if (lookDirection == Vector3.zero) return;
            
            if(_logsEnable) _logger.Log("MOVEMENT_MODULE", $"Look direction: {lookDirection}");
            
            var targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }

        public Vector3 GetCurrentVelocity()
        {
            return _currentVelocity;
        }
        
        public float GetCurrentMovementSpeed(float maxSpeed)
        {
            var speed = new Vector2(_currentVelocity.x, _currentVelocity.z).magnitude / maxSpeed;
            
            var absSpeed = Mathf.Abs(speed);
            
            if (absSpeed < 0.01f)
            {
                absSpeed = 0;
            }

            return absSpeed;
        }

        public void StopMovement()
        {
            if (_logsEnable) _logger.Log("MOVEMENT_MODULE", "Stop movement");
            _currentVelocity = Vector3.zero;
        }
    }
}
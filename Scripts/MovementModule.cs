using UnityEngine;

namespace _RagDollBaseCharecter.Scripts
{
    public class MovementModule : MonoBehaviour
    {
        [SerializeField] private float _rotationSpeed = 10f;
        [SerializeField] private float _gravity = -9.81f;

        private CharacterController _characterController;
        private Vector3 _currentVelocity;

        public void Init(CharacterController characterController)
        {
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
            _characterController.Move(_currentVelocity * Time.deltaTime);
            
            if (!(_currentVelocity.sqrMagnitude > 0.1f)) return;
            
            var lookDirection = new Vector3(_currentVelocity.x, 0, _currentVelocity.z).normalized;
            if (lookDirection == Vector3.zero) return;
            
            var targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }

        public Vector3 GetCurrentVelocity()
        {
            return _currentVelocity;
        }

        public void StopMovement()
        {
            _currentVelocity = Vector3.zero;
        }
    }
}
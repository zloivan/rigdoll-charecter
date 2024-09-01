using System;
using System.Collections;
using System.Threading.Tasks;
using _RagDollBaseCharecter.Scripts.External.abstractions;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts
{
    public class RagdollCharacter : CharacterBase
    {
        public event Action<IHit> OnHit;

        [Header("Movement Parameters")]
        [SerializeField]
        private float _rotationSpeed = 10f;

        [SerializeField]
        private float _gravity = -9.81f;

        [Header("Ragdoll Parameters")]
        [SerializeField]
        private float _minImpactForceToRagdoll = 5f;

        [SerializeField]
        private float _minVelocityToRagdoll = 5f;

        [SerializeField]
        private float _recoveryDelay = 3f;

        [SerializeField]
        private float _blendToAnimationTime = 1f;

        [Header("Visual Feedback")]
        [SerializeField]
        private GameObject _impactEffectPrefab;

        [SerializeField]
        private float _impactEffectDuration = 1f;

        [SerializeField]
        private CharacterConfig _characterConfig;

        private static readonly int _animationSpeedProp = Animator.StringToHash("Speed");
        private static readonly int _animationBlendProp = Animator.StringToHash("BlendToAnimation");
        private Animator _animator;
        private Rigidbody[] _ragdollRigidbodies;
        private Collider[] _ragdollColliders;
        private Vector3 _currentVelocity;
        private Coroutine _recoveryCoroutine;
        private CharacterController _characterController;

        
        //Unity Methods
        private void OnCollisionEnter(Collision collision)
        {
            Debug.Log("On Collision Enter");
            if (IsRagDollActive) return;

            if (!ShouldEnterRagdoll(collision)) return;

            SetRagdollState(true);
            OnHit?.Invoke(new RagdollHit(collision));

            // Visual feedback for impact
            SpawnImpactEffect(collision.contacts[0].point);

            // Stop any ongoing recovery
            if (_recoveryCoroutine != null)
            {
                StopCoroutine(_recoveryCoroutine);
            }

            // Start a new recovery process
            _recoveryCoroutine = StartCoroutine(RecoverFromRagdoll());
        }

        private void Update()
        {
            if (_characterController != null && !_characterController.enabled)
            {
                Debug.LogWarning($"Update - CharacterController is disabled. IsRagDollActive: {IsRagDollActive}");
                _characterController.enabled = true;
            }
            
            if (IsRagDollActive) return;
            
            HandleMovement();
            UpdateAnimation();
        }

        private void OnDisable()
        {
            // Ensure we stop the recovery coroutine if the character is disabled
            if (_recoveryCoroutine == null) return;


            StopCoroutine(_recoveryCoroutine);
            _recoveryCoroutine = null;
        }

        //Public API    
        public override async Task Init()
        {
            _animator = GetComponentInChildren<Animator>();
            _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
            _ragdollColliders = GetComponentsInChildren<Collider>();
            _characterController = GetComponent<CharacterController>();
            
            if (_characterController == null)
            {
                Debug.LogError("CharacterController component not found. Please add a CharacterController.");
            }
            else
            {
                _characterController.enabled = true;
                Debug.Log($"Init - CharacterController enabled: {_characterController.enabled}");
            }

            // Initialize other components as needed
            SetRagdollState(false);
            Debug.Log($"CharacterController initialized. Enabled: {_characterController.enabled}");

            await Task.CompletedTask;
        }

        public override void Activate()
        {
            // Enable character functionality
            enabled = true;
        }

        public override void Deactivate()
        {
            // Disable character functionality
            enabled = false;
        }

        //Private Methods
        private void HandleMovement()
        {
            if (_characterController == null || !_characterController.enabled) return;

            var targetVelocity = new Vector3(MoveDir.x, 0, MoveDir.y) * _characterConfig.MaxSpeed;
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, _characterConfig.AccelerationCoef * Time.deltaTime);

            // Apply gravity
            if (!_characterController.isGrounded)
            {
                _currentVelocity.y += _gravity * Time.deltaTime;
            }
            else if (_currentVelocity.y < 0)
            {
                _currentVelocity.y = -2f; // Small downward force when grounded
            }

            _currentVelocity = Vector3.ClampMagnitude(_currentVelocity, _characterConfig.MaxSpeed);
            _characterController.Move(_currentVelocity * Time.deltaTime);

            // Rotate the character
            if (!(_currentVelocity.sqrMagnitude > 0.1f)) 
                return;
            
            var lookDirection = new Vector3(_currentVelocity.x, 0, _currentVelocity.z).normalized;
            
            
            if (lookDirection == Vector3.zero) 
                return;
            
            var targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }

        private void UpdateAnimation()
        {
            if (_animator is null) return;

            // Calculate the speed based on the horizontal velocity
            var speed = new Vector2(_currentVelocity.x, _currentVelocity.z).magnitude / _characterConfig.MaxSpeed;

            var absSpeed = Mathf.Abs(speed);
            
            if (absSpeed < 0.01f)
            {
                absSpeed = 0;
            }
            
            // Update the Speed parameter in the Animator
            _animator.SetFloat(_animationSpeedProp, absSpeed);
        }

        private void SetRagdollState(bool active)
        {
            Debug.Log($"SetRagdollState - Active: {active}, CharacterController enabled: {_characterController.enabled}");
            
            IsRagDollActive = active;

            foreach (var rb in _ragdollRigidbodies)
            {
                rb.isKinematic = !active;
            }

            foreach (var rgCollider in _ragdollColliders)
            {
                rgCollider.enabled = active;
            }

            _animator.enabled = true; // Keep animator enabled
            _animator.SetFloat(_animationBlendProp, active ? -1f : 1f);
        }

        private IEnumerator RecoverFromRagdoll()
        {
            yield return new WaitForSeconds(_recoveryDelay);

            var hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            var hipPosition = hips.position;
            var hipRotation = hips.rotation;

            transform.position = new Vector3(hipPosition.x, transform.position.y, hipPosition.z);
            transform.rotation = Quaternion.Euler(0, hipRotation.eulerAngles.y, 0);

            // Start the blend back to animation
            _animator.SetFloat(_animationBlendProp, 1f);

            // Wait for the blend to complete
            yield return new WaitForSeconds(_blendToAnimationTime);

            SetRagdollState(false);
            _recoveryCoroutine = null;

            // Ensure CharacterController is re-enabled
            if (_characterController != null)
            {
                _characterController.enabled = true;
            }
        }

        private bool ShouldEnterRagdoll(Collision collision)
        {
            var impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
            var characterSpeed = _currentVelocity.magnitude;

            return impactForce > _minImpactForceToRagdoll || characterSpeed > _minVelocityToRagdoll;
        }

        private void SpawnImpactEffect(Vector3 position)
        {
            if (_impactEffectPrefab == null) return;
            
            
            var effect = Instantiate(_impactEffectPrefab, position, Quaternion.identity);
            Destroy(effect, _impactEffectDuration);
        }
    }
}
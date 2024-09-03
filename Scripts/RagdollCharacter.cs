using System;
using System.Collections;
using System.Linq;
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
        
        [SerializeField] private LayerMask _groundLayer;

        private static readonly int _animationSpeedProp = Animator.StringToHash("Speed");
        private static readonly int _isRagdollProp = Animator.StringToHash("IsRagdoll");
        private static readonly int _getUpDirectionProp = Animator.StringToHash("GetUpDirection");
        
        private Animator _animator;
        private Rigidbody[] _ragdollRigidbodies;
        private Vector3 _currentVelocity;
        private Coroutine _recoveryCoroutine;
        private CharacterController _characterController;


        private void Start()
        {
            Init();
        }

        //Unity Methods
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (IsRagDollActive) return;

            if (hit.gameObject.layer == _groundLayer)
            {
                Debug.Log("Hit the ground");
                return;
            }
            
            // // Ignore collisions with the floor
            // if (hit.gameObject.CompareTag("Floor"))
            // {
            //     return;
            // }
            
            Debug.Log($"Controller Collider Hit with {hit.gameObject.name}");
            if (!ShouldEnterRagdoll(hit)) return;

            SetRagdollState(true);
            OnHit?.Invoke(new RagdollHit(hit));

            // Visual feedback for impact
            SpawnImpactEffect(hit.point);

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
            _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>()
                .Where(rb => rb.gameObject != gameObject)
                .ToArray();
            
            _characterController = GetComponent<CharacterController>();
            _animator.enabled = true;
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
            if (_animator is null || !_animator.enabled) return;

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

        [ContextMenu("Enable Ragdoll")]
        public void EnableRagdoll()
        {
            SetRagdollState(true);
        }
     
        
        private void SetRagdollState(bool active)
        {
            IsRagDollActive = active;
            if (active)
            {
                // Disable character controller
                _characterController.enabled = false;
                _animator.enabled = false;
                
                // Enable ragdoll physics
                foreach (var rb in _ragdollRigidbodies)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.velocity = _currentVelocity; // Transfer current velocity to ragdoll parts
                }

                _currentVelocity = Vector3.zero;
            }
            else
            {
                // Re-enable character controller
                _characterController.enabled = true;
                _animator.enabled = true;
                // Disable ragdoll physics
                foreach (var rb in _ragdollRigidbodies)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.velocity = Vector3.zero;
                }
            }
        }

        private IEnumerator RecoverFromRagdoll()
        {
            yield return new WaitForSeconds(_recoveryDelay);

            // Determine get-up direction based on character's orientation
            var hipsForward = _animator.GetBoneTransform(HumanBodyBones.Hips).forward;
            var dotProduct = Vector3.Dot(hipsForward, Vector3.up);
            var getUpDirection = dotProduct > 0 ? 0 : 1; // 0 for front, 1 for back

            SetRagdollState(false);
            
            // Set the get-up direction and exit ragdoll state
            _animator.SetInteger(_getUpDirectionProp, getUpDirection);
            _animator.SetBool(_isRagdollProp, false);
        }

        private bool ShouldEnterRagdoll(ControllerColliderHit hit)
        {
            // We'll only use the character's velocity now
            var impactForce = _characterController.velocity.magnitude;
            var characterSpeed = impactForce; // They're the same in this case

            // Debug log to see the values
            Debug.Log($"Impact Force/Character Speed: {impactForce}");

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
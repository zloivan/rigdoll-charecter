using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using _RagDollBaseCharecter.Scripts.External.abstractions;
using _RagDollBaseCharecter.Scripts.Helpers;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts
{
    internal enum CharacterStates
    {
        Locomotion,
        Ragdoll,
        StandingUp,
        ResettingBones,
    }

    public struct BoneTransform
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
    }

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

        [SerializeField]
        private LayerMask _groundLayer;

        [SerializeField]
        private string _standUpAnimationState;

        [SerializeField]
        private string _standUpClipName;

        [SerializeField]
        private float _timeToResetBones = 5f;

        public override bool IsRagDollActive => _currentState == CharacterStates.Ragdoll;

        private static readonly int _animationSpeedProp = Animator.StringToHash("Speed");
        private static readonly int _getUpDirectionProp = Animator.StringToHash("GetUpDirection");
        private readonly ILogger _logger = new RagdollLogger();

        private Animator _animator;
        private Rigidbody[] _ragdollRigidbodies;
        private Vector3 _currentVelocity;
        private CharacterController _characterController;
        private Transform _hipsBone;
        private Vector3 _trackedHipPosition;
        private CharacterStates _currentState;
        private float _recoverTimeElapsed;
        private float _resetBonesTimeElapsed;
        private BoneTransform[] _standUpBoneTransforms;
        private BoneTransform[] _ragdollBoneTransforms;
        private Transform[] _bones;

        private async void Start()
        {
            await Init();
        }

        //Unity Methods
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (_currentState != CharacterStates.Locomotion) return;

            //Check if not the floor
            if (((1 << hit.gameObject.layer) & _groundLayer) != 0)
            {
                return;
            }

            _logger.Log("RagdollCharacter", $"Controller Collider Hit with {hit.gameObject.name}");
            if (!ShouldEnterRagdoll(hit)) return;

            OnHit?.Invoke(new RagdollHit(hit));

            TriggerRagdollState(hit.point);
        }

        private void Update()
        {
            switch (_currentState)
            {
                case CharacterStates.Locomotion:
                    LocomotionUpdate();
                    break;
                case CharacterStates.Ragdoll:
                    RagdollUpdate();
                    break;
                case CharacterStates.StandingUp:
                    StandUpUpdate();
                    break;
                case CharacterStates.ResettingBones:
                    ResettingBoneUpdate();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        //Public API    
        public override async Task Init()
        {
            _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();

            _animator = GetComponentInChildren<Animator>();
            Debug.Assert(_animator != null, "Animator component not found. Please add an Animator.");

            _characterController = GetComponent<CharacterController>();
            Debug.Assert(_characterController != null,
                "CharacterController component not found. Please add a CharacterController.");

            _hipsBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Debug.Assert(_hipsBone != null, "Hip bone not found in the character hierarchy.");

            _bones = _hipsBone.GetComponentsInChildren<Transform>();

            _ragdollBoneTransforms = new BoneTransform[_bones.Length];
            _standUpBoneTransforms = new BoneTransform[_bones.Length];

            PopulateStartAnimationBoneTransforms(_standUpClipName, _standUpBoneTransforms);

            TriggerLocomotionState();

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

        private void HandleMovement()
        {
            if (_characterController == null || !_characterController.enabled) return;

            var targetVelocity = new Vector3(MoveDir.x, 0, MoveDir.y) * _characterConfig.MaxSpeed;
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity,
                _characterConfig.AccelerationCoef * Time.deltaTime);

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

        private void UpdateMovementAnimationSpeed()
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
        public void TriggerRagdoll()
        {
            TriggerRagdollState();
        }

        private void TriggerRagdollState(Vector3 hitPoint = default)
        {
            _logger.Log("RagdollCharacter", "Entering Ragdoll State");
            // Disable character controller

            EnableRagdoll();

            if (hitPoint != default)
            {
                SpawnImpactEffect(hitPoint);
            }

            _currentState = CharacterStates.Ragdoll;
        }

        private void EnableRagdoll()
        {
            _characterController.enabled = false;
            _animator.SetFloat(_animationSpeedProp, 0);
            _animator.enabled = false;
            // Enable ragdoll physics
            foreach (var rb in _ragdollRigidbodies)
            {
                rb.isKinematic = false;
                rb.velocity = _currentVelocity; // Transfer current velocity to ragdoll parts
            }

            _currentVelocity = Vector3.zero;
        }

        private void TriggerLocomotionState()
        {
            _logger.Log("RagdollCharacter", "Entering Locomotion State");

            DisableRagdoll();

            _currentState = CharacterStates.Locomotion;
        }

        private void TriggerStandUpState()
        {
            _logger.Log("RagdollCharacter", "Standing Up");
            // Re-enable character controller

            DisableRagdoll();

            _animator.Play(_standUpAnimationState);

            _currentState = CharacterStates.StandingUp;
        }

        private void DisableRagdoll()
        {
            // Disable ragdoll physics
            foreach (var rb in _ragdollRigidbodies)
            {
                rb.isKinematic = true;
            }

            _characterController.enabled = true;
            _animator.enabled = true;
        }

        private void TriggerResetBonesState()
        {
            _logger.Log("RagdollCharacter", "Resetting Bones");

            AlignRotationToHips();
            AlignPositionToHips();
            PopulateBoneTransforms(_ragdollBoneTransforms);

            _currentState = CharacterStates.ResettingBones;
        }

        private void StandUpUpdate()
        {
            if (_animator.GetCurrentAnimatorStateInfo(0).IsName(_standUpAnimationState) == false)
            {
                TriggerLocomotionState();
            }
        }

        private void RagdollUpdate()
        {
            _recoverTimeElapsed += Time.deltaTime;
            if (!(_recoverTimeElapsed >= _recoveryDelay)) return;

            TriggerResetBonesState();
            _recoverTimeElapsed = 0;
        }

        private void LocomotionUpdate()
        {
            HandleMovement();
            UpdateMovementAnimationSpeed();
        }

        private void ResettingBoneUpdate()
        {
            _resetBonesTimeElapsed += Time.deltaTime;
            var percentage = _resetBonesTimeElapsed / _timeToResetBones;

            _logger.Log("RagdollCharacter", $"Resetting Bones: {percentage} [{_resetBonesTimeElapsed}]");
            for (var i = 0; i < _bones.Length; i++)
            {
                _bones[i].localPosition = Vector3.Lerp(_ragdollBoneTransforms[i].Position,
                    _standUpBoneTransforms[i].Position, percentage);
                _bones[i].localRotation = Quaternion.Slerp(_ragdollBoneTransforms[i].Rotation,
                    _standUpBoneTransforms[i].Rotation, percentage);
            }

            if (percentage < 1) return;

            TriggerStandUpState();
            _resetBonesTimeElapsed = 0;
        }

        private bool ShouldEnterRagdoll(ControllerColliderHit hit)
        {
            // We'll only use the character's velocity now
            var impactForce = _characterController.velocity.magnitude;
            var characterSpeed = impactForce; // They're the same in this case

            // Debug log to see the values
            _logger.Log("RagdollCharacter", $"Impact Force/Character Speed: {impactForce}");

            return impactForce > _minImpactForceToRagdoll || characterSpeed > _minVelocityToRagdoll;
        }

        private void SpawnImpactEffect(Vector3 position)
        {
            if (_impactEffectPrefab == null) return;


            var effect = Instantiate(_impactEffectPrefab, position, Quaternion.identity);
            Destroy(effect, _impactEffectDuration);
        }

        private void AlignRotationToHips()
        {
            var originalRotation = _hipsBone.rotation;
            var originalHipsPosition = _hipsBone.position;


            var desiredDirection = _hipsBone.up * -1;
            desiredDirection.y = 0;
            desiredDirection.Normalize();

            var fromToRotation = Quaternion.FromToRotation(transform.forward, desiredDirection);

            transform.rotation *= fromToRotation;
            _hipsBone.rotation = originalRotation;
            _hipsBone.position = originalHipsPosition;
        }

        private void AlignPositionToHips()
        {
            var originalPosition = _hipsBone.position;

            transform.position = new Vector3(_hipsBone.position.x, transform.position.y, _hipsBone.position.z);

            var positionOffset = _standUpBoneTransforms[0].Position;
            positionOffset.y = 0;
            positionOffset = transform.rotation * positionOffset;
            transform.position -= positionOffset;
            
            var layerToIgnore = LayerMask.NameToLayer("Ragdoll");
            var layerMask = ~(1 << layerToIgnore);

            if (Physics.Raycast(transform.position, Vector3.down, out var hit, Mathf.Infinity, layerMask))
            {
                _logger.Log("RagdollCharacter", $"Hit {hit.transform.name}");
                _logger.Log(hit.transform.position.y);
                transform.position = new Vector3(transform.position.x, hit.point.y,
                    transform.position.z);
            }

            _hipsBone.position = originalPosition;
        }

        private void PopulateBoneTransforms(BoneTransform[] boneTransforms)
        {
            for (var i = 0; i < _bones.Length; i++)
            {
                boneTransforms[i] = new BoneTransform
                {
                    Position = _bones[i].localPosition,
                    Rotation = _bones[i].localRotation
                };
            }
        }

        private void PopulateStartAnimationBoneTransforms(string clipName, BoneTransform[] boneTransforms)
        {
            var posBeforeSampling = transform.position;
            var rotBeforeSampling = transform.rotation;

            foreach (var animationClip in _animator.runtimeAnimatorController.animationClips)
            {
                if (animationClip.name == clipName)
                {
                    animationClip.SampleAnimation(gameObject, 0);
                    PopulateBoneTransforms(boneTransforms);
                }
            }

            transform.position = posBeforeSampling;
            transform.rotation = rotBeforeSampling;
        }
    }
}
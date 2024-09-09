using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using _RagDollBaseCharecter.Scripts.External.abstractions;
using _RagDollBaseCharecter.Scripts.Helpers;
using UnityEngine;
using UnityEngine.Serialization;

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
        private float _recoveryDelay = 3f;

        [Header("Visual Feedback")]
        [SerializeField]
        private GameObject _impactEffectPrefab;

        [SerializeField]
        private float _impactEffectDuration = 1f;

        [SerializeField]
        private CharacterConfig _characterConfig;

        [SerializeField]
        private LayerMask _groundLayer;

        [FormerlySerializedAs("_standUpFrontAnimationState")]
        [FormerlySerializedAs("_standUpAnimationState")]
        [Header("Animation Names")]
        [SerializeField]
        private string _standUpFaceDownAnimationState;

        [FormerlySerializedAs("_standUpFrontClipName")]
        [FormerlySerializedAs("_standUpClipName")]
        [SerializeField]
        private string _standUpFaceDownClipName;
        
        [FormerlySerializedAs("_standUpBackAnimationState")]
        [SerializeField]
        private string _standUpFaceUpAnimationState;

        [FormerlySerializedAs("_standUpBackClipName")]
        [SerializeField]
        private string _standUpFaceUpClipName;

        [SerializeField]
        private float _timeToResetBones = 5f;

        public override bool IsRagDollActive => _currentState == CharacterStates.Ragdoll;

        private static readonly int _animationSpeedProp = Animator.StringToHash("Speed");
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
        private BoneTransform[] _standUpFaceDownBoneTransforms;
        private BoneTransform[] _standUpFaceUpBoneTransforms;
        private BoneTransform[] _ragdollBoneTransforms;
        private Transform[] _bones;
        private bool _isFacingUp;

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
            if (!ShouldEnterRagdoll()) return;

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
            _standUpFaceDownBoneTransforms = new BoneTransform[_bones.Length];
            _standUpFaceUpBoneTransforms = new BoneTransform[_bones.Length];

            PopulateStartAnimationBoneTransforms(_standUpFaceDownClipName, _standUpFaceDownBoneTransforms);
            PopulateStartAnimationBoneTransforms(_standUpFaceUpClipName, _standUpFaceUpBoneTransforms);

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
                _currentVelocity.y = -2f;
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
                rb.velocity = _currentVelocity * _characterConfig.HitMassCoef; // Transfer current velocity to ragdoll parts
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

            _animator.Play(GetStandingUpAnimationState(), 0, 0);

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

            _isFacingUp = _hipsBone.forward.y > 0;
            AlignRotationToHips();
            AlignPositionToHips();
            PopulateBoneTransforms(_ragdollBoneTransforms);

            _currentState = CharacterStates.ResettingBones;
        }

        private void StandUpUpdate()
        {
            if (_animator.GetCurrentAnimatorStateInfo(0).IsName(GetStandingUpAnimationState()) == false)
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
            var standUpBoneTransforms = GetStandUpBoneTransforms();
            for (var i = 0; i < _bones.Length; i++)
            {
                _bones[i].localPosition = Vector3.Lerp(_ragdollBoneTransforms[i].Position,
                    standUpBoneTransforms[i].Position, percentage);
                _bones[i].localRotation = Quaternion.Slerp(_ragdollBoneTransforms[i].Rotation,
                    standUpBoneTransforms[i].Rotation, percentage);
            }

            if (percentage < 1) return;

            TriggerStandUpState();
            _resetBonesTimeElapsed = 0;
        }

        private bool ShouldEnterRagdoll()
        {
            var impactForce = _characterController.velocity.magnitude;

            // Debug log to see the values
            _logger.Log("RagdollCharacter", $"Impact Force/Character Speed: {impactForce}");

            return impactForce * _characterConfig.HitMassCoef > _minImpactForceToRagdoll;
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


            var desiredDirection = _hipsBone.up;

            if (_isFacingUp)
            {
                desiredDirection *= -1;
            }
            
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

            transform.position = originalPosition;

            var positionOffset = GetStandUpBoneTransforms()[0].Position;
            positionOffset.y = 0;
            positionOffset = transform.rotation * positionOffset;
            transform.position -= positionOffset;
            
            var layerToIgnore = LayerMask.NameToLayer("Ragdoll");
            var layerMask = ~(1 << layerToIgnore);

            if (Physics.Raycast(transform.position, Vector3.down, out var hit, Mathf.Infinity, layerMask))
            {
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
                if (animationClip.name != clipName) continue;
                
                animationClip.SampleAnimation(gameObject, 0);
                PopulateBoneTransforms(boneTransforms);
            }

            transform.position = posBeforeSampling;
            transform.rotation = rotBeforeSampling;
        }
        
        
        private string GetStandingUpAnimationState()
        {
            var standUpFaceDownAnimationState = _isFacingUp ? _standUpFaceUpAnimationState : _standUpFaceDownAnimationState;
            
            _logger.Log("RagdollCharacter", $"Stand Up Animation State: {standUpFaceDownAnimationState}");
            return standUpFaceDownAnimationState;
        }

        private BoneTransform[] GetStandUpBoneTransforms()
        {
            return _isFacingUp ? _standUpFaceUpBoneTransforms : _standUpFaceDownBoneTransforms;
        }
    }
}
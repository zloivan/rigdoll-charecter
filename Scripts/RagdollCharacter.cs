using System;
using System.Threading.Tasks;
using _RagDollBaseCharecter.Scripts.External_Contracts.abstractions;
using _RagDollBaseCharecter.Scripts.Helpers;
using Unity.VisualScripting;
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
        public override Vector2 MoveDir => _inputModule.GetMoveDirection();
        private readonly ILogger _logger = new RagdollLogger();

        //private Animator _animator;
        private Rigidbody[] _ragdollRigidbodies;
        private InputModule _inputModule;
        private MovementModule _movementModule;
        private CharacterController _characterController;
        private AnimationModule _animationModule;
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

            var animator = GetComponentInChildren<Animator>();
            Debug.Assert(animator != null, "Animator component not found. Please add an Animator.");

            _characterController = gameObject.GetOrAddComponent<CharacterController>();
            _inputModule = gameObject.GetOrAddComponent<InputModule>();
            
            _animationModule = gameObject.GetOrAddComponent<AnimationModule>();
            _animationModule.Init(animator);

            _movementModule = gameObject.GetOrAddComponent<MovementModule>();
            _movementModule.Init(_characterController);

            _hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
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
            var currentVelocity = _movementModule.GetCurrentVelocity();
            
            _characterController.enabled = false;
            _animationModule.UpdateMovementAnimation(0);
            _animationModule.SetAnimatorEnabled(false);
            foreach (var rb in _ragdollRigidbodies)
            {
                rb.isKinematic = false;
                rb.velocity = currentVelocity * _characterConfig.HitMassCoef; // Transfer current velocity to ragdoll parts
            }

            _movementModule.StopMovement();
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
           

            DisableRagdoll();

            _animationModule.PlayAnimation(_isFacingUp ? _standUpFaceUpAnimationState : _standUpFaceDownAnimationState);

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
            _animationModule.SetAnimatorEnabled(true);
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
            if (_animationModule.IsInState(GetStandingUpAnimationState()) == false)
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
            _movementModule.Move(MoveDir, _characterConfig.MaxSpeed, _characterConfig.AccelerationCoef);
            var speed = _movementModule.GetCurrentMovementSpeed(_characterConfig.MaxSpeed);
            _animationModule.UpdateMovementAnimation(speed);
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

            foreach (var animationClip in _animationModule.GetAnimationClips())
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
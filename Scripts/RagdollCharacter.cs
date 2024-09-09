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

        [Header("Animation Names")]
        [SerializeField]
        private string _standUpFaceDownAnimationState;

        [SerializeField]
        private string _standUpFaceDownClipName;

        [SerializeField]
        private string _standUpFaceUpAnimationState;

        [SerializeField]
        private string _standUpFaceUpClipName;

        [SerializeField]
        private float _timeToResetBones = 5f;

        public override bool IsRagDollActive => _currentState == CharacterStates.Ragdoll;
        public override Vector2 MoveDir => _inputModule.GetMoveDirection();
        private readonly ILogger _logger = new RagdollLogger();

        private InputModule _inputModule;
        private MovementModule _movementModule;
        private CharacterController _characterController;
        private AnimationModule _animationModule;
        private RagdollModule _ragdollModule;
        
        private CharacterStates _currentState;
        private float _recoverTimeElapsed;
        private float _resetBonesTimeElapsed;
        
        private BoneTransform[] _standUpFaceDownBoneTransforms;
        private BoneTransform[] _standUpFaceUpBoneTransforms;
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

            _logger.Log("RAGDOLL_CHARACTER", $"Controller Collider Hit with {hit.gameObject.name}");
            if (!_ragdollModule.ShouldEnterRagdoll(_characterConfig.HitMassCoef)) return;

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
            var animator = GetComponentInChildren<Animator>();
            Debug.Assert(animator != null, "Animator component not found. Please add an Animator.");

            _characterController = gameObject.GetOrAddComponent<CharacterController>();
            _inputModule = gameObject.GetOrAddComponent<InputModule>();

            _animationModule = gameObject.GetOrAddComponent<AnimationModule>();
            _animationModule.Init(animator);

            _movementModule = gameObject.GetOrAddComponent<MovementModule>();
            _movementModule.Init(_characterController);

            _ragdollModule = gameObject.GetOrAddComponent<RagdollModule>();
            _ragdollModule.Init(_characterController, animator);

            var hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            Debug.Assert(hipsBone != null, "Hip bone not found in the character hierarchy.");

            _bones = hipsBone.GetComponentsInChildren<Transform>();

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
            _logger.Log("RAGDOLL_CHARACTER", "Entering Ragdoll State");

            _ragdollModule.EnterRagdoll(_movementModule.GetCurrentVelocity(), _characterConfig.HitMassCoef);
            _animationModule.UpdateMovementAnimation(0);
            _animationModule.SetAnimatorEnabled(false);
            _movementModule.StopMovement();

            if (hitPoint != default)
            {
                SpawnImpactEffect(hitPoint);
            }

            _currentState = CharacterStates.Ragdoll;
        }

        private void TriggerLocomotionState()
        {
            _logger.Log("RAGDOLL_CHARACTER", "Entering Locomotion State");
            _ragdollModule.ExitRagdoll();
            _animationModule.SetAnimatorEnabled(true);

            _currentState = CharacterStates.Locomotion;
        }

        private void TriggerStandUpState()
        {
            _logger.Log("RAGDOLL_CHARACTER", "Standing Up");

            _animationModule.SetAnimatorEnabled(true);
            _animationModule.PlayAnimation(_ragdollModule.IsFacingUp()
                ? _standUpFaceUpAnimationState
                : _standUpFaceDownAnimationState);

            _currentState = CharacterStates.StandingUp;
        }

        private void TriggerResetBonesState()
        {
            _logger.Log("RAGDOLL_CHARACTER", "Resetting Bones");

            _ragdollModule.AlignRotationToHips(transform);
            _ragdollModule.AlignPositionToHips(transform, GetStandUpBoneTransforms()[0].Position);
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

            _logger.Log("RAGDOLL_CHARACTER", $"Resetting Bones: {percentage} [{_resetBonesTimeElapsed}]");
            
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

        private void SpawnImpactEffect(Vector3 position)
        {
            if (_impactEffectPrefab == null) return;

            var effect = Instantiate(_impactEffectPrefab, position, Quaternion.identity);
            Destroy(effect, _impactEffectDuration);
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
            var standUpFaceDownAnimationState = _ragdollModule.IsFacingUp()
                ? _standUpFaceUpAnimationState
                : _standUpFaceDownAnimationState;

            _logger.Log("RAGDOLL_CHARACTER", $"Stand Up Animation State: {standUpFaceDownAnimationState}");
            return standUpFaceDownAnimationState;
        }

        private BoneTransform[] GetStandUpBoneTransforms()
        {
            return _ragdollModule.IsFacingUp() ? _standUpFaceUpBoneTransforms : _standUpFaceDownBoneTransforms;
        }
    }
}
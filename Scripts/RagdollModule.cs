using System;
using _RagDollBaseCharecter.Scripts.Helpers;
using Unity.Collections;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts
{
    public class RagdollModule : MonoBehaviour
    {
        public event Action OnEnterRagdoll;
        public event Action OnExitRagdoll;
        
        [SerializeField]
        private float _minImpactForceToRagdoll = 5f;

        [SerializeField]
        private bool _logsEnabled = false;
        
        public bool IsRagdollActive => _isRagdollActive;
        
        private Rigidbody[] _ragdollRigidbodies;
        private CharacterController _characterController;
        private Transform _hipsBone;
        private bool _isRagdollActive;
        private readonly ILogger _logger = new RagdollLogger();

        public void Init(CharacterController characterController, Animator animator)
        {
            Debug.Assert(characterController != null, "Character controller is null", this);
            Debug.Assert(animator != null, "Animator is null", this);

            _characterController = characterController;
            _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
            _hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            Debug.Assert(_hipsBone != null, "Hip bone not found in the character hierarchy.", this);
        }

        public bool ShouldEnterRagdoll(float hitMassCoef)
        {
            var impactForce = _characterController.velocity.magnitude;
            if (_logsEnabled) _logger.Log("RAGDOLL_MODULE", $"Impact Force: {impactForce}");
            return impactForce * hitMassCoef > _minImpactForceToRagdoll;
        }

        public void EnterRagdoll(Vector3 currentVelocity, float hitMassCoef)
        {
            if (_logsEnabled) _logger.Log("RAGDOLL_MODULE", "Entering Ragdoll State");
            _isRagdollActive = true;
            _characterController.enabled = false;

            foreach (var rb in _ragdollRigidbodies)
            {
                rb.isKinematic = false;
                rb.velocity = currentVelocity * hitMassCoef;
            }

            OnEnterRagdoll?.Invoke();
        }

        public void ExitRagdoll()
        {
            if (_logsEnabled) _logger.Log("RAGDOLL_MODULE", "Exiting Ragdoll State");
            _isRagdollActive = false;
            _characterController.enabled = true;

            foreach (var rb in _ragdollRigidbodies)
            {
                rb.isKinematic = true;
            }

            OnExitRagdoll?.Invoke();
        }

        public bool IsFacingUp()
        {
            return _hipsBone.forward.y > 0;
        }

        public void AlignRotationToHips(Transform characterTransform)
        {
            var originalRotation = _hipsBone.rotation;
            var originalHipsPosition = _hipsBone.position;

            var desiredDirection = _hipsBone.up;
            if (IsFacingUp())
            {
                desiredDirection *= -1;
            }

            desiredDirection.y = 0;
            desiredDirection.Normalize();

            var fromToRotation = Quaternion.FromToRotation(characterTransform.forward, desiredDirection);

            characterTransform.rotation *= fromToRotation;
            _hipsBone.rotation = originalRotation;
            _hipsBone.position = originalHipsPosition;
        }

        public void AlignPositionToHips(Transform characterTransform, Vector3 standUpPositionOffset)
        {
            var originalPosition = _hipsBone.position;

            characterTransform.position = originalPosition;

            var positionOffset = standUpPositionOffset;
            positionOffset.y = 0;
            positionOffset = characterTransform.rotation * positionOffset;
            characterTransform.position -= positionOffset;

            var layerToIgnore = LayerMask.NameToLayer("Ragdoll");
            var layerMask = ~(1 << layerToIgnore);

            if (Physics.Raycast(characterTransform.position, Vector3.down, out var hit, Mathf.Infinity, layerMask))
            {
                characterTransform.position = new Vector3(characterTransform.position.x, hit.point.y,
                    characterTransform.position.z);
            }

            _hipsBone.position = originalPosition;
        }
    }
}
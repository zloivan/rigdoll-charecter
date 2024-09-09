using System;
using _RagDollBaseCharecter.Scripts.External_Contracts.abstractions;
using _RagDollBaseCharecter.Scripts.Helpers;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts
{
    public class CollisionModule : MonoBehaviour
    {
        public event Action<IHit> OnCollisionDetected;

        [SerializeField]
        private bool _logsEnabled;

        [SerializeField]
        private float _groundAngleThreshold = 45;
                
        private CharacterController _characterController;
        private readonly ILogger _logger = new RagdollLogger();
      

        public void Init(CharacterController characterController)
        {
            Debug.Assert(characterController != null, "Character controller is null", this);

            _characterController = characterController;
        }

        public void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!_characterController.enabled) return;
            
            var surfaceAngle = Vector3.Angle(hit.normal, Vector3.up);
            var isGround = surfaceAngle < _groundAngleThreshold;

            if (isGround)
            {
                return; 
            }
            
            if (_logsEnabled)
            {
                VisualizeHit(hit);
                _logger.Log("COLLISION_MODULE", $"Controller Collider Hit with {hit.gameObject.name}");
            }

            var ragdollHit = new RagdollHit(hit);
            OnCollisionDetected?.Invoke(ragdollHit);
                
            if (_logsEnabled)
            {
                _logger.Log("COLLISION_MODULE", $"Ragdoll should be activated, with {ragdollHit.HitBone} and {ragdollHit.HitDir}");
            }
        }
        
        private void VisualizeHit(ControllerColliderHit hit)
        {
            // Draw hit direction
            Debug.DrawRay(hit.point, hit.moveDirection.normalized * 1f, Color.blue, 5f);

            // Draw surface normal
            Debug.DrawRay(hit.point, hit.normal * 1f, Color.green, 5f);
            // Draw line to hit bone
            if (hit.transform != null)
            {
                Debug.DrawLine(hit.point, hit.transform.position, Color.yellow, 5f);
            }

            // Draw hit point
            Debug.DrawLine(hit.point, hit.point + Vector3.up * 0.1f, Color.red, 5f);
        }

    }
}
using _RagDollBaseCharecter.Scripts.External_Contracts.abstractions;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts
{
    public class RagdollHit : IHit
    {
        public Vector3 HitDir { get; }
        public Transform HitBone { get; }

        public RagdollHit(ControllerColliderHit hit)
        {
            HitDir = hit.moveDirection;
            HitBone = hit.transform;
        }
    }
}
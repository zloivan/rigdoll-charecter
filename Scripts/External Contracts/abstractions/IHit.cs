using UnityEngine;

namespace _RagdollCharacterMechanic.Scripts.External_Contracts.abstractions
{
    // IHit interface
    public interface IHit{
        public Vector3 HitDir { get; }
        public Transform HitBone { get; }
    }

}
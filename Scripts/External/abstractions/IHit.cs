using UnityEngine;

namespace _RagDollBaseCharecter.Scripts.External.abstractions
{
    // IHit interface
    public interface IHit{
        public Vector3 HitDir { get; }
        public Transform HitBone { get; }
    }

}
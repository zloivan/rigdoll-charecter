using _RagDollBaseCharecter.Scripts.External.abstractions;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts
{
    public class RagdollHit : IHit
    {
        public Vector3 HitDir { get; }
        public Transform HitBone { get; }

        public RagdollHit(Collision collision)
        {
            HitDir = collision.relativeVelocity.normalized;
            HitBone = collision.contacts[0].thisCollider.transform;
        }
    }
}
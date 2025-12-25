using System;
using System.Threading.Tasks;
using UnityEngine;

namespace _RagdollCharacterMechanic.Scripts.External_Contracts.abstractions
{
    public interface ICharacter{
        public event Action<IHit> OnHit;
        public Vector2 MoveDir { get; }
        public bool IsRagDollActive { get; }
        public Task Init();
        public void Activate();
        public void Deactivate();
    }

}
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts.External_Contracts.abstractions
{
    public abstract class CharacterBase : MonoBehaviour, ICharacter
    {
        public event Action<IHit> OnHit;
        public abstract Vector2 MoveDir { get; }
        public abstract bool IsRagDollActive { get; }
        public abstract Task Init();
        public abstract void Activate();
        public abstract void Deactivate();
    }
}
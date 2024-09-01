using System;
using System.Threading.Tasks;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts.External.abstractions
{
    public abstract class CharacterBase : MonoBehaviour, ICharacter
    {
        public event Action<IHit> OnHit;
        public Vector2 MoveDir { get; set; }
        public bool IsRagDollActive { get; set; }
        public abstract Task Init();
        public abstract void Activate();
        public abstract void Deactivate();
    }
}
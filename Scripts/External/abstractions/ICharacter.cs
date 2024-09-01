using System;
using System.Threading.Tasks;
using UnityEngine;

namespace _RagDollBaseCharecter.Scripts.External.abstractions
{
    public interface ICharacter{
        public event Action<IHit> OnHit;
        public Vector2 MoveDir { get; set; }
        public bool IsRagDollActive { get; }
        public Task Init();
        public void Activate();
        public void Deactivate();
    }

}
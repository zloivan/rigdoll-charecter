using UnityEngine;

namespace _RagDollBaseCharecter.Scripts
{
    public class InputModule : MonoBehaviour
    {
        private Vector2 _moveDir;

        private void Update()
        {
            var horizontal = Input.GetAxis("Horizontal");
            var vertical = Input.GetAxis("Vertical");
        
            _moveDir = new Vector2(horizontal, vertical).normalized;
        }

        public Vector2 GetMoveDirection()
        {
            return _moveDir;
        }
    }
}

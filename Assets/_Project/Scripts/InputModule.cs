using _RagdollCharacterMechanic.Scripts.Helpers;
using UnityEngine;

namespace _RagdollCharacterMechanic.Scripts
{
    public class InputModule : MonoBehaviour
    {
        [SerializeField]
        private bool _logsEnabled;
        private Vector2 _moveDir;

        private readonly ILogger _logger = new RagdollLogger();
        private void Update()
        {
            var horizontal = Input.GetAxis("Horizontal");
            var vertical = Input.GetAxis("Vertical");
        
            _moveDir = new Vector2(horizontal, vertical).normalized;
            if(_logsEnabled) _logger.Log("INPUT_MODULE", $"Move direction: {_moveDir}");
        }

        public Vector2 GetMoveDirection()
        {
            return _moveDir;
        }
    }
}

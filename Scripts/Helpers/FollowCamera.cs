using UnityEngine;

namespace _RagDollBaseCharecter.Scripts.Helpers
{
    public class FollowCamera : MonoBehaviour
    {
    
        [SerializeField]
        private Transform _player;

        [SerializeField]
        private float _lerpSpeed = 5f;

        // Update is called once per frame
        void Update()
        {
            var newPosition = Vector3.Lerp(transform.position, _player.position, _lerpSpeed * Time.deltaTime);
            transform.position = newPosition;
        }
    }
}

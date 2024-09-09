using _RagdollCharacterMechanic.Scripts.Helpers;
using UnityEngine;

namespace _RagdollCharacterMechanic.Scripts
{
    public class AnimationModule : MonoBehaviour
    {
        [SerializeField] bool _logsEnabled = false;
        private static readonly int AnimationSpeedProp = Animator.StringToHash("Speed");
        private Animator _animator;

        private readonly ILogger _logger = new RagdollLogger();

        public void Init(Animator animator)
        {
            Debug.Assert(animator != null, "Animator is null", this);
            _animator = animator;
        }

        public void UpdateMovementAnimation(float speed)
        {
            if (_animator.enabled)
            {
                _animator.SetFloat(AnimationSpeedProp, speed);
                
               if(_logsEnabled) _logger.Log("ANIMATION_MODULE", $"Speed: {speed}");
            }
        }

        public void PlayAnimation(string stateName)
        {
            if (_animator.enabled)
            {
                if(_logsEnabled) _logger.Log("ANIMATION_MODULE", $"Play animation: {stateName}");
                _animator.Play(stateName);
            }
        }

        public void SetAnimatorEnabled(bool isEnabled)
        {
            if(_logsEnabled) _logger.Log("ANIMATION_MODULE", $"Set animator enabled: {isEnabled}");
            _animator.enabled = isEnabled;
        }

        public bool IsInState(string stateName)
        {
            var isInState = _animator.enabled && _animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
            
            if(_logsEnabled) _logger.Log("ANIMATION_MODULE", $"Is in state {stateName}: {isInState}");
            return isInState;
        }

        public AnimationClip[] GetAnimationClips()
        {
            var animationClips = _animator.runtimeAnimatorController.animationClips;
            if(_logsEnabled) _logger.Log("ANIMATION_MODULE", $"Get animation clips called... Clips count: {animationClips.Length}");
            return animationClips;
        }
    }
}
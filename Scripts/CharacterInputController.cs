using _RagDollBaseCharecter.Scripts;
using UnityEngine;


public class CharacterInputController : MonoBehaviour
{
    private RagdollCharacter _character;

    private void Awake()
    {
        _character = GetComponent<RagdollCharacter>();
        //_character.Init();
    }

    private void Update()
    {
        var horizontal = Input.GetAxis("Horizontal");
        var vertical = Input.GetAxis("Vertical");
        
        _character.MoveDir = new Vector2(horizontal, vertical).normalized;
    }
}

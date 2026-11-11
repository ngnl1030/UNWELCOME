using UnityEngine;
using StarterAssets;
namespace MakeTrap.Multiplayer
{
    [RequireComponent(typeof(Animator), typeof(CharacterController), typeof(FirstPersonController))]
    public class FirstPersonAnimation : MonoBehaviour
    {
        Animator animator; CharacterController controller; FirstPersonController movement;
        void Awake(){animator=GetComponent<Animator>();controller=GetComponent<CharacterController>();movement=GetComponent<FirstPersonController>();}
        void LateUpdate()
        {
            var velocity=controller.velocity;var planar=new Vector2(velocity.x,velocity.z).magnitude;
            animator.SetFloat("Speed",planar,.1f,Time.deltaTime);animator.SetFloat("MotionSpeed",1);
            animator.SetBool("Grounded",movement.Grounded);
            animator.SetBool("Jump",!movement.Grounded && velocity.y>.1f);
            animator.SetBool("FreeFall",!movement.Grounded && velocity.y<-.1f);
        }
        // Shared humanoid clips contain these events; first-person audio is separate.
        void OnFootstep(AnimationEvent e) { }
        void OnLand(AnimationEvent e) { }
    }
}

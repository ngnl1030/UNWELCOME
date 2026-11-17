using UnityEngine;
using StarterAssets;

namespace MakeTrap.Multiplayer
{
    [RequireComponent(typeof(Animator))]
    public class ThiefTrapFall : MonoBehaviour
    {
        public float fallDuration = 3f;
        Animator animator;
        BotPatrol patrol;
        ThirdPersonController movement;
        NetworkPlayer player;
        bool restorePatrol, restoreMovement;
        float recoverAt, nextFallTime;
        public bool IsFalling { get; private set; }
        static readonly int FallFlat = Animator.StringToHash("FallFlat");

        void Awake()
        {
            animator = GetComponent<Animator>();
            patrol = GetComponent<BotPatrol>();
            movement = GetComponent<ThirdPersonController>();
            player = GetComponent<NetworkPlayer>();
        }

        void OnControllerColliderHit(ControllerColliderHit hit) => TouchTrap(hit.collider);
        void OnTriggerEnter(Collider other) => TouchTrap(other);

        void TouchTrap(Collider other)
        {
            if (IsFalling || Time.time < nextFallTime || other.GetComponentInParent<TripTrap>() == null) return;
            if (player != null && player.IsSpawned && !player.IsOwner) return;
            IsFalling = true;
            recoverAt = Time.time + fallDuration;
            restorePatrol = patrol != null && patrol.enabled;
            restoreMovement = movement != null && movement.enabled;
            if (restorePatrol) patrol.enabled = false;
            if (restoreMovement) movement.enabled = false;
            animator.SetFloat("Speed", 0);
            var networkAnimator = GetComponent<OwnerNetworkAnimator>();
            if (player != null && player.IsSpawned && networkAnimator != null) networkAnimator.SetTrigger(FallFlat);
            else animator.SetTrigger(FallFlat);
        }

        void Update()
        {
            if (!IsFalling || Time.time < recoverAt) return;
            IsFalling = false;
            nextFallTime = Time.time + 1f;
            if (restorePatrol) patrol.enabled = true;
            if (restoreMovement) movement.enabled = true;
        }
    }
}

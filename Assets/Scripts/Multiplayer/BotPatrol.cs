using UnityEngine;

namespace MakeTrap.Multiplayer
{
    [RequireComponent(typeof(CharacterController))]
    public class BotPatrol : MonoBehaviour
    {
        [Min(0)] public float speed = 1.4f;
        [Min(0.5f)] public float patrolRadius = 2.5f;
        public float gravity = -15f;
        public float turnSpeed = 240f;
        CharacterController controller;
        Animator animator;
        Vector3 origin, target;
        float verticalSpeed;
        int waypoint;
        float blockedTime;
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int MotionSpeed = Animator.StringToHash("MotionSpeed");
        static readonly int Grounded = Animator.StringToHash("Grounded");

        void OnEnable()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            origin = transform.position;
            waypoint = 0; verticalSpeed = 0; blockedTime = 0;
            NextTarget();
        }
        void NextTarget()
        {
            // Simple server-side patrol for the framework's test map, not tactical game AI.
            float angle = waypoint++ * 90f * Mathf.Deg2Rad;
            target = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * patrolRadius;
            blockedTime = 0;
        }
        void Update()
        {
            Vector3 direction = target - transform.position; direction.y = 0;
            if (direction.sqrMagnitude < .15f || blockedTime > 1.5f) { NextTarget(); direction = target - transform.position; direction.y = 0; }
            direction.Normalize();
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * Time.deltaTime, -30);
            Vector3 before = transform.position;
            controller.Move((direction * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
            Vector3 moved = transform.position - before; moved.y = 0;
            if (moved.sqrMagnitude < .000001f) blockedTime += Time.deltaTime; else blockedTime = 0;
            if (animator != null)
            {
                animator.SetFloat(Speed, moved.magnitude / Mathf.Max(Time.deltaTime, .001f));
                animator.SetFloat(MotionSpeed, 1);
                animator.SetBool(Grounded, controller.isGrounded);
            }
        }
    }
}

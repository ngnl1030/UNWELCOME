using UnityEngine;

namespace AtriaHouse
{
    /// <summary>
    /// A hinged room door that opens from a CharacterController collision.
    /// The spring returns it gently to the jamb after the player passes through.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(HingeJoint))]
    public sealed class PushOpenDoor : MonoBehaviour
    {
        [SerializeField] private float pushImpulse = 2.8f;
        [SerializeField] private float minimumSpeed = 0.5f;

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public void Push(Vector3 contactPoint, Vector3 movement)
        {
            Vector3 horizontalMovement = Vector3.ProjectOnPlane(movement, Vector3.up);
            if (horizontalMovement.sqrMagnitude < minimumSpeed * minimumSpeed) return;

            body.WakeUp();
            body.AddForceAtPosition(horizontalMovement.normalized * pushImpulse, contactPoint, ForceMode.Impulse);
        }
    }
}

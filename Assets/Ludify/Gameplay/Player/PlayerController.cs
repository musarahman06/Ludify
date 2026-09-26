using Ludify.Gameplay.Core;
using UnityEngine;

namespace Ludify.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 8f;
        public float sprintSpeed = 16f;
        public float turnSpeed = 12f;
        public float gravity = -20f;
        public float jumpHeight = 1.5f;
        public Transform cameraTransform;

        [Header("Animation")]
        public Transform visualRoot;
        public Transform leftLeg;
        public Transform rightLeg;
        public Transform leftArm;
        public Transform rightArm;
        public float walkCycleSpeed = 8f;
        public float limbSwingAngle = 35f;
        public float bobHeight = 0.05f;

        private CharacterController controller;
        private float verticalVelocity;
        private float walkCyclePhase;
        private bool controlEnabled = true;

        public bool ControlEnabled => controlEnabled;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        /// <summary>Stop reading movement input (menus, cutscenes, while driving). Gravity still applies.</summary>
        public void SetControlEnabled(bool enabled) => controlEnabled = enabled;

        /// <summary>
        /// Move the player instantly. The CharacterController must be disabled while the transform is
        /// set, otherwise it snaps the player back to its cached position.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            verticalVelocity = 0f;
            controller.enabled = true;
        }

        void Update()
        {
            var input = GameModeManager.Instance != null ? GameModeManager.Instance.Input : null;
            bool canMove = controlEnabled && input != null && input.OnFoot.enabled;

            Vector2 move = canMove ? input.Move.ReadValue<Vector2>() : Vector2.zero;
            Vector3 inputDir = Vector3.ClampMagnitude(new Vector3(move.x, 0f, move.y), 1f);

            Vector3 forward = cameraTransform ? cameraTransform.forward : transform.forward;
            Vector3 right = cameraTransform ? cameraTransform.right : transform.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();

            Vector3 moveDir = forward * inputDir.z + right * inputDir.x;
            float speed = canMove && input.Sprint.IsPressed() ? sprintSpeed : moveSpeed;

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;

            if (canMove && controller.isGrounded && input.Jump.WasPressedThisFrame())
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = moveDir * speed;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            AnimateVisual(moveDir.magnitude, speed);
        }

        void AnimateVisual(float moveAmount, float speed)
        {
            bool moving = moveAmount > 0.05f && controller.isGrounded;

            if (moving)
            {
                walkCyclePhase += Time.deltaTime * walkCycleSpeed * (speed / moveSpeed);
            }
            else
            {
                walkCyclePhase = Mathf.Lerp(walkCyclePhase, 0f, Time.deltaTime * 6f);
            }

            float swing = Mathf.Sin(walkCyclePhase) * limbSwingAngle * (moving ? 1f : 0f);

            if (leftLeg) leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (rightLeg) rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (leftArm) leftArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (rightArm) rightArm.localRotation = Quaternion.Euler(swing, 0f, 0f);

            if (visualRoot)
            {
                float bob = moving ? Mathf.Abs(Mathf.Sin(walkCyclePhase * 2f)) * bobHeight : 0f;
                visualRoot.localPosition = new Vector3(0f, bob, 0f);
            }
        }
    }
}

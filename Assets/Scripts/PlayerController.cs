using UnityEngine;
using UnityEngine.InputSystem;

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

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = 0f, v = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;

        Vector3 inputDir = new Vector3(h, 0f, v);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        Vector3 forward = cameraTransform ? cameraTransform.forward : transform.forward;
        Vector3 right = cameraTransform ? cameraTransform.right : transform.right;
        forward.y = 0f; right.y = 0f;
        forward.Normalize(); right.Normalize();

        Vector3 moveDir = forward * inputDir.z + right * inputDir.x;
        float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : moveSpeed;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;

        if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
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

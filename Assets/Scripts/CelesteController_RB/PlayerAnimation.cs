using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private PlayerController playerController;

    private void Start()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        SetIdleMoveAnimation();
        SetVerticalMovementAndGroundCheckValues();
        SetJumpAnimation();
    }

    private void SetIdleMoveAnimation()
    {
        int inputX = (int)playerController.inputX;
        animator.SetInteger("HorizontalInput", inputX);
    }

    private void SetJumpAnimation()
    {
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            animator.SetTrigger("Jump");
        }
    }

    private void SetVerticalMovementAndGroundCheckValues()
    {
        int verticalMovement = (int)playerController.veriticalVelcity;
        bool groundCheckValue = playerController.groundCheckRealtime;

        animator.SetBool("IsGrounded", groundCheckValue);
        animator.SetInteger("VerticalMovement", verticalMovement);
    }

    public void RotatePlayer()
    {

    }
}

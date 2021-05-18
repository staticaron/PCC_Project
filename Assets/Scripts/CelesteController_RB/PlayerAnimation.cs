using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private PlayerController playerController;
    [SerializeField] private GameObject dashParticleGO;

    private void Start()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();

        PlayerController.EDashed += ToggleDashAnimation;
    }

    private void OnDisable()
    {
        PlayerController.EDashed -= ToggleDashAnimation;
    }

    private void Update()
    {
        SetIdleMoveAnimation();
        SetVerticalMovementAndGroundCheckValues();
        SetJumpAnimation();
        RotatePlayer();
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

    public void RotatePlayer(bool shouldRotateLeft)
    {
        //If not in simple state then avoid rotation
        if (playerController.currentMovementState != MovementState.SIMPLE) return;

        if (shouldRotateLeft) playerController.gameObject.transform.rotation = Quaternion.Euler(0, 180, 0);
        else playerController.gameObject.transform.rotation = Quaternion.Euler(0, 0, 0);
    }

    public void RotatePlayer()
    {
        //If not in simple state then avoid rotation
        if (playerController.currentMovementState != MovementState.SIMPLE) return;

        if (this.playerController.inputX > 0)
        {
            playerController.gameObject.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else if (playerController.inputX < 0)
        {
            playerController.gameObject.transform.rotation = Quaternion.Euler(0, 180, 0);
        }
    }

    private void ToggleDashAnimation(bool start)
    {
        if (start == true)
        {
            dashParticleGO.SetActive(true);
        }
        else
        {
            dashParticleGO.SetActive(false);
        }
    }
}

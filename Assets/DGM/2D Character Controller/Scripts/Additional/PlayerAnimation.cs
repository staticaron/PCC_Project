using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerController))]
public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private PlayerController playerController;
    [SerializeField] private ParticleSystem dashParticleGO;
    [SerializeField] private List<string> _stateNames;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();

        PlayerController.EDashed += ToggleDashAnimation;
        PlayerController.EGrabbed += ToggleGrabAnimation;
        PlayerController.EJumped += ToggleJumpAnimation;
        PlayerController.EMovement += ToggleMovementState;
    }

    private void OnDisable()
    {
        PlayerController.EDashed -= ToggleDashAnimation;
        PlayerController.EGrabbed -= ToggleGrabAnimation;
        PlayerController.EJumped -= ToggleJumpAnimation;
        PlayerController.EMovement -= ToggleMovementState;
    }

    private void ToggleJumpAnimation(bool jumpStarted)
    {
        if (jumpStarted) { animator.Play("Launch"); }
        else { animator.Play("Fall"); }
    }

    private void ToggleDashAnimation(bool start)
    {
        if (start == true)
        {
            dashParticleGO.Play();

        }
        else
        {
            dashParticleGO.Stop();

        }
    }

    private void ToggleMovementState(bool isMoving)
    {
        if (isMoving)
        {
            animator.Play("Run");

        }
        else
        {
            animator.Play("Idle");
        }
    }

    private void ToggleGrabAnimation(bool start)
    {
        if (start == true) { }
        else { }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    [SerializeField] private ParticleSystem dashParticleGO;

    private void Awake()
    {
        animator = GetComponent<Animator>();

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
        if (jumpStarted) { animator.Play("Launch");  }
        else { animator.Play("Fall");}
    }

    private void ToggleDashAnimation(bool start)
    {
        if (start == true)
        {
            dashParticleGO.Play();
            animator.Play("Fall");
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
        if (start == true) { animator.Play("Grab"); }
        else {  }
    }
}

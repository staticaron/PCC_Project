using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    private const string LauchStateName = "Launch";
    private const string FallStateName = "Fall";
    private const string RunStateName = "Run";
    private const string IdleStateName = "Idle";
    private const string GrabStateName = "Grab";

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
        if (jumpStarted) { animator.Play(LauchStateName); }
        else { animator.Play(FallStateName); }
    }

    private void ToggleDashAnimation(bool start)
    {
        if (start == true)
        {
            dashParticleGO.Play();
            animator.Play(FallStateName);
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
            animator.Play(RunStateName);
        }
        else
        {
            animator.Play(IdleStateName);
        }
    }

    private void ToggleGrabAnimation(bool start)
    {
        if (start == true) { animator.Play(GrabStateName); }
    }
}

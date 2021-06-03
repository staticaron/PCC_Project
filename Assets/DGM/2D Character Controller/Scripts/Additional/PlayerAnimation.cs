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
        if (jumpStarted) { animator.Play("Launch"); Debug.Log("Jump Started"); }
        else { animator.Play("Fall"); Debug.Log("Jump Ended"); }
    }

    private void ToggleDashAnimation(bool start)
    {
        if (start == true)
        {
            dashParticleGO.Play();
            Debug.Log("Dash Started");
        }
        else
        {
            dashParticleGO.Stop();
            Debug.Log("Dash Ended");
        }
    }

    private void ToggleMovementState(bool isMoving)
    {
        if (isMoving)
        {
            animator.Play("Run");
            Debug.Log("Movement Started");
        }
        else
        {
            animator.Play("Idle");
            Debug.Log("Movement Ended");
        }
    }

    private void ToggleGrabAnimation(bool start)
    {
        if (start == true) { Debug.Log("Grab Started"); }
        else { Debug.Log("Grab Ended"); }
    }
}

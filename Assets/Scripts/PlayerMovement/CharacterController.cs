using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed;
    [SerializeField]
    private float jumpForce;

    private Rigidbody2D rb;

    private PlayerMovementAction playerMovementActionMap;

    private void Awake() {
        //Get the references
        playerMovementActionMap = new PlayerMovementAction();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable() {
        playerMovementActionMap.Enable();
    }

    private void OnDisable() {
        playerMovementActionMap.Disable();
    }

    private void Update() {

        //Grab the movement input from the action map
        float movementInput = playerMovementActionMap.General.Movement.ReadValue<float>();

        rb.velocity = new Vector2(movementInput * moveSpeed * Time.deltaTime, rb.velocity.y);
        
    }
}

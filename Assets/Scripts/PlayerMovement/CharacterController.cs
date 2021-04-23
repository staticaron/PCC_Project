using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed;
    [SerializeField]
    private float maxFallSpeed;
    private float verticalVelocity;

    public bool useTimeDelta;

    //Ground Check
    [SerializeField]
    private bool isGrounded;
    [SerializeField]
    private Transform foot;     //The transform of the character's foot
    [SerializeField]
    private Vector2 footSize;   //Size of the character's foot
    [SerializeField]
    private LayerMask groundMask;   //Mask of the object where character is considered grounded
    [SerializeField]
    private bool footVisualisation;

    //Jump DataItems 
    [SerializeField]
    private float jumpForce;    
    [SerializeField]
    private float localGravity;     //Local Gravity experienced by the character and not by any other object 
    private float timeElapsed;

    private float movementInput_InputSys;
    private float movementInput_OldSys;
    private Rigidbody2D rb;

    private PlayerMovementAction playerMovementActionMap;

    //Event that exposes the charactercontroller values
    public delegate void ExposeValues(bool timeDeltaTime, float horizontalSpeed, float jumpForce, float localGravity);
    public static event ExposeValues eExposeValues;

    private void Awake() {
        //Get the references
        playerMovementActionMap = new PlayerMovementAction();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start() {
        //Debug Subscribing
        DebugMenu.eSetDebugValues += SetDebugValue;

        //Expose the character controller values
        StartCoroutine(exposeValues(0.5f));
    }

    private IEnumerator<WaitForSeconds> exposeValues(float time){
        yield return new WaitForSeconds(time);

        if(eExposeValues != null) eExposeValues(useTimeDelta, moveSpeed, jumpForce, localGravity);
    }

    private void OnEnable() {
        playerMovementActionMap.Enable();
    }

    private void OnDisable() {
        playerMovementActionMap.Disable();

        //Debug UnSubscribing
        DebugMenu.eSetDebugValues -= SetDebugValue;
    }

    private void Update() {

        //Grab the horizontal movement input from the action map
        movementInput_InputSys = playerMovementActionMap.General.Movement.ReadValue<float>();

        if(useTimeDelta == true)
            rb.velocity = new Vector2(movementInput_InputSys * moveSpeed * Time.deltaTime * 100, rb.velocity.y);
        else
            rb.velocity = new Vector2(movementInput_InputSys * moveSpeed, rb.velocity.y);

        //Check for ground
        isGrounded = Physics2D.OverlapBox(foot.position, footSize, 0, groundMask);

        //Apply gravity to the character
        if (!isGrounded)
        {
            timeElapsed += Time.deltaTime;
            verticalVelocity = rb.velocity.y + Physics2D.gravity.y * timeElapsed * localGravity;
            verticalVelocity = Mathf.Clamp(verticalVelocity, -maxFallSpeed, jumpForce);
        }
        else{

            timeElapsed = 0;
        }

        //Apply the calculated vertical velocity to the character
        rb.velocity = new Vector2(rb.velocity.x, verticalVelocity);
        
        //Jump
        if (playerMovementActionMap.General.Jump.triggered)
        {
            Jump();
        }
    }

    private void Jump()
    {
        if(!isGrounded) return;

        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }

    private void OnDrawGizmos() {

        //If the foot visualisation is turned on then show a visualisation gizmo
        if (footVisualisation == true)
        {
            Gizmos.DrawCube(foot.position, footSize);
        }
    }

    void SetDebugValue(bool timeDeltaTime, float horizontalSpeed, float jumpForce, float localGravity){
        useTimeDelta = timeDeltaTime;
        this.moveSpeed = horizontalSpeed;
        this.jumpForce = jumpForce;
        this.localGravity = localGravity;
    }
}

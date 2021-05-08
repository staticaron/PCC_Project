using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController : MonoBehaviour
{
    [Header("Movement Values")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float maxFallSpeed;
    private float verticalVelocity;

    [SerializeField] public bool useTimeDelta;                    //Should use time.deltaTime or not

    //Checkers
    [Header("Checkers")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isCoyoted;                      //Store the value whether the character is in coyote range
    [SerializeField] private bool isJumpable;                     //Combines the results of ground check and coyote check


    //MovementDelta measurement
    private Vector2 currPos, prevPos, movementDelta;
    private Vector2 CurrPos
    {
        get { return currPos; }
        set { currPos = new Vector2((int)value.x, (int)value.y); }
    }

    private Vector2 PrevPos
    {
        get { return prevPos; }
        set { prevPos = new Vector2((int)value.x, (int)value.y); }
    }

    [Header("Ground Check Values")]
    [SerializeField] private Transform foot;                      //The transform of the character's foot
    [SerializeField] private Vector2 footSize;                    //Size of the character's foot
    [SerializeField] private LayerMask groundMask;                //Mask of the object where character is considered grounded
    [SerializeField] private bool footVisualisation;

    //Coyote Check
    [Header("Coyote check Values")]
    [SerializeField] private Vector2 defaultCoyoteFootSize;       //Foot size of the coyote foot
    private Vector2 coyoteFootSize;
    [SerializeField] private float maxCoyoteOffsetX;              //Max width of the coyote foot while moving
    [SerializeField] private bool coyoteVisualisation;


    [Header("Jump Values")]
    [SerializeField] private float jumpForce;
    [SerializeField] private float localGravity;                   //Local Gravity experienced by the character and not by any other object 
    private float timeElapsed;


    private float movementInput_InputSys;
    private Rigidbody2D rb;

    private PlayerMovementAction playerMovementActionMap;

    //Event that exposes the charactercontroller values
    public delegate void ExposeValues(bool timeDeltaTime, float horizontalSpeed, float jumpForce, float localGravity);
    public static event ExposeValues eExposeValues;

    private void Awake()
    {
        //Get the references
        playerMovementActionMap = new PlayerMovementAction();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        //Debug Subscribing
        DebugMenu.eSetDebugValues += SetDebugValue;

        //Expose the character controller values
        StartCoroutine(exposeValues(0.5f));

        //Init
        currPos = prevPos = transform.position;

        playerMovementActionMap.General.Jump.performed += _ => Jump();
    }

    //Used to expose some of the values of the controller to other scripts
    private IEnumerator<WaitForSeconds> exposeValues(float time)
    {
        yield return new WaitForSeconds(time);

        if (eExposeValues != null) eExposeValues(useTimeDelta, moveSpeed, jumpForce, localGravity);
    }

    private void OnEnable()
    {
        playerMovementActionMap.Enable();
    }

    private void OnDisable()
    {
        playerMovementActionMap.Disable();

        //Debug UnSubscribing
        DebugMenu.eSetDebugValues -= SetDebugValue;
    }

    private void Update()
    {

        //Grab the horizontal movement input from the action map
        movementInput_InputSys = playerMovementActionMap.General.Movement.ReadValue<float>();

        //Move the character based on the movement input recieved from the player input
        HorizontalMovement();

        GetMovemetDelta();

        //Implement all these groundCheck and coyoteCheck and JumpCheck stuff
        ImplementCheckers();

        //Apply gravity to the player
        ApplyGravity();

        //Jump
        if (playerMovementActionMap.General.Jump.triggered)
        {
            Jump();
        }
    }

    private void GetMovemetDelta()
    {
        currPos = transform.position;

        if (currPos.x - prevPos.x > 0) movementDelta = new Vector2(1, movementDelta.y);
        else if (currPos.x - prevPos.x == 0) movementDelta = new Vector2(0, movementDelta.y);
        else movementDelta = new Vector2(-1, movementDelta.y);

        if (currPos.y - prevPos.y > 0) movementDelta = new Vector2(movementDelta.x, 1);
        else if (currPos.y - prevPos.y == 0) movementDelta = new Vector2(movementDelta.x, 0);
        else movementDelta = new Vector2(movementDelta.x, -1);

        prevPos = currPos;
    }

    private void HorizontalMovement()
    {
        if (useTimeDelta == true)
            rb.velocity = new Vector2(movementInput_InputSys * moveSpeed * Time.deltaTime * 100, rb.velocity.y);
        else
            rb.velocity = new Vector2(movementInput_InputSys * moveSpeed, rb.velocity.y);
    }

    private void ApplyGravity()
    {
        //Apply gravity to the character
        if (!isGrounded)
        {
            timeElapsed += Time.deltaTime;
            verticalVelocity = rb.velocity.y + Physics2D.gravity.y * timeElapsed * localGravity;
            verticalVelocity = Mathf.Clamp(verticalVelocity, -maxFallSpeed, jumpForce);
        }
        else
        {
            timeElapsed = 0;
        }

        rb.velocity = new Vector2(rb.velocity.x, verticalVelocity);
    }

    private void ImplementCheckers()
    {
        //Check for ground
        isGrounded = Physics2D.OverlapBox(foot.position, footSize, 0, groundMask);

        //Check for coyote
        var realVelocity = moveSpeed * movementDelta.x; //Real velocity based on change in position

        coyoteFootSize = new Vector2(defaultCoyoteFootSize.x + Mathf.Clamp(Mathf.Abs(realVelocity), 0, maxCoyoteOffsetX), defaultCoyoteFootSize.y);
        isCoyoted = Physics2D.OverlapBox(foot.position, coyoteFootSize, 0, groundMask);

        isJumpable = isCoyoted || isGrounded;
    }

    private void Jump()
    {
        if (!isJumpable) return;

        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }

    private void OnDrawGizmos()
    {

        //If the foot visualisation is turned on then show a visualisation gizmo
        if (footVisualisation == true) Gizmos.DrawCube(foot.position, footSize);

        //If the coyote foot visualisation is turned on then show a visualisation gizmo
        if (coyoteVisualisation == true) Gizmos.DrawCube(foot.position, coyoteFootSize);
    }

    void SetDebugValue(bool timeDeltaTime, float horizontalSpeed, float jumpForce, float localGravity)
    {
        useTimeDelta = timeDeltaTime;
        this.moveSpeed = horizontalSpeed;
        this.jumpForce = jumpForce;
        this.localGravity = localGravity;
    }
}

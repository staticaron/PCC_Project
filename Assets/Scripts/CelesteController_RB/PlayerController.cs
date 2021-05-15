using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MovementState { SIMPLE, DASH, GRAB };
public enum GrabStates { NONE, HOLD, CLIMB, CLIMBJUMP };

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    private PlayerMovementAction playerMovementActionMap;
    private Rigidbody2D thisBody;

    public MovementState currentMovementState = MovementState.SIMPLE;
    public GrabStates currentGrabState = GrabStates.NONE;

    //The keyboard inputs for the specified axis
    [HideInInspector] public float inputX;
    [HideInInspector] public float inputY;

    //Basically needed for animations to work
    [HideInInspector] public float veriticalVelcity;

    //General Properties
    [Header("General Properties")]
    [SerializeField] private bool isControllable;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;

    [Header("Checkers")]
    [SerializeField] private bool groundCheck;
    //GroundCheck = GroundCheckRealtime + coyotiness
    [HideInInspector] public bool groundCheckRealtime;

    [Header("Gravity Values")]
    [SerializeField] private float overallGravityModifier;
    [SerializeField] private float fallGravityModifier;
    [SerializeField] private float jumpGravityModifier;
    [SerializeField] private float airDrag, landDrag, dashDrag, grabDrag;

    [Header("Coyote Values")]
    [SerializeField] private Transform foot;
    [SerializeField] private Vector2 footSize;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float coyoteTime;
    [SerializeField] private float groundCheckTimer;
    [SerializeField] private bool coyoteEnabled;
    [SerializeField] private bool footVisualization;

    [Header("Dash Values")]
    [SerializeField] private float dashForce;
    [SerializeField] private Vector2 dashVector;
    [SerializeField] private float dashRecoverTime;
    [SerializeField] private float dashTimeOffset;
    private float dashTimer;
    [SerializeField] private bool canDash;

    [Header("Grab Values")]
    [SerializeField] private int maxStamina;
    [SerializeField] private int currentStamina;
    [SerializeField] private float grabMovementModifier;
    [SerializeField] private bool isGrabbing;
    [SerializeField] private bool canGrab;
    [SerializeField] private Transform hand;
    [SerializeField] private Vector2 handSize;
    [SerializeField] private LayerMask grabMask;
    [SerializeField] private bool handVisualization;
    [SerializeField] private int staminaConsumptionOnHold;          //Stamina that is consumed per frame while climb holding
    [SerializeField] private int staminaConsumptionOnClimb;         //Stamina that is consumed per frame while climb climbing
    [SerializeField] private int staminaConsumptionOnClimbJump;     //Stamina that is consumed per frame while climb jumping

    #region Keys
    /* ckey = Jump
       zkey = Dash
       xkey = Grab */
    #endregion

    private void Awake()
    {
        //Initialize variables
        playerMovementActionMap = new PlayerMovementAction();
        thisBody = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        playerMovementActionMap.Enable();
    }

    private void OnDisable()
    {
        playerMovementActionMap.Disable();
    }

    private void Update()
    {
        SetValues();

        GrabInput();
    }


    private void FixedUpdate()
    {

        SetHorizontalVelocity();

        GroundCheck();

        ApplyDrag();

        JumpMechanism();

        RemoveFloatyness();

        SetDash(); //No value mean set the dash according to the groundCheckRealtime

        Dash();

        CalculateStamina(currentGrabState);

        GrabMechanism();
    }


    private void SetValues()
    {
        veriticalVelcity = thisBody.velocity.y;

        dashVector = new Vector2(inputX, inputY).normalized * dashForce;
    }

    private void GrabInput()
    {
        inputX = playerMovementActionMap.General.Movement.ReadValue<float>();
        inputY = playerMovementActionMap.General.VerticalMovement.ReadValue<float>();
    }

    private void SetHorizontalVelocity()
    {
        if (isControllable == false) return;

        thisBody.velocity = new Vector2(inputX * moveSpeed, thisBody.velocity.y);
    }

    private void JumpMechanism()
    {
        if (currentMovementState == MovementState.SIMPLE)
        {
            if (groundCheck == true && Keyboard.current.cKey.wasPressedThisFrame)
            {
                thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce);
            }
        }
        else if (currentMovementState == MovementState.GRAB)
        {
            if (isGrabbing == true && Keyboard.current.cKey.wasPressedThisFrame)
            {
                thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce);
                currentGrabState = GrabStates.CLIMBJUMP;
            }
        }

    }

    private void RemoveFloatyness()
    {
        if (thisBody.velocity.y <= 0)
        {
            thisBody.gravityScale = fallGravityModifier * overallGravityModifier;
        }
        else if (thisBody.velocity.y > 0)
        {
            thisBody.gravityScale = jumpGravityModifier * overallGravityModifier;
        }
    }

    private void GroundCheck()
    {
        groundCheckRealtime = Physics2D.OverlapBox(foot.position, footSize, 0, groundMask);

        if (groundCheckRealtime == false && groundCheck == true && coyoteEnabled == false)
        {
            groundCheckTimer = coyoteTime;
            coyoteEnabled = true;
        }

        if (groundCheckRealtime == false)
        {
            if (groundCheckTimer > 0) { groundCheckTimer -= Time.deltaTime; }
            else if (groundCheckTimer <= 0) { groundCheck = false; coyoteEnabled = false; }
        }
        else
        {
            groundCheckTimer = 0;
            groundCheck = true;
            coyoteEnabled = false;
        }
    }

    private void ApplyDrag()
    {
        if (currentMovementState == MovementState.SIMPLE)
        {
            if (groundCheckRealtime == true)
            {
                thisBody.drag = landDrag;
            }
            else
            {
                thisBody.drag = airDrag;
            }
        }
        else if (currentMovementState == MovementState.DASH)
        {
            thisBody.drag = dashDrag;
        }
    }

    //Can be called from another script (crystal) to replenish the dash
    public void SetDash(bool value)
    {
        canDash = value;
    }

    public void SetDash()
    {
        if (groundCheckRealtime == true) canDash = true;
    }

    private void Dash()
    {
        //Take Dash Input and stores it in dash timer, dash timer allows some room for the player in case direction was set later than pressing the dash key
        if (Keyboard.current.xKey.wasPressedThisFrame && canDash == true)
        {
            dashTimer = dashTimeOffset;
        }
        else
        {
            dashTimer -= Time.deltaTime;
        }

        //Apply dash time spent since the dash key was pressed is less than the dash time
        if (dashTimer > 0 && currentMovementState != MovementState.DASH)
        {
            if (dashVector == Vector2.zero) return;

            currentMovementState = MovementState.DASH;

            if (dashVector == new Vector2(1 * dashForce, 0) || dashVector == new Vector2(-1 * dashForce, 0))
            {
                //If dashing sideways then overall gravity acting on the player should be zero to give a uncontrolled situation.
                isControllable = false;
                thisBody.velocity = Vector2.zero;
                overallGravityModifier = 0;
            }
            else
            {
                //If not dashing sideways then overall gravity should be normal as there is no need of uncontrolled situation as it is already is
                isControllable = false;
                thisBody.velocity = Vector2.zero;
            }

            //Apply actual dash force
            thisBody.AddForce(dashVector);
            StartCoroutine(DashRecover(dashRecoverTime));

            canDash = false;
        }
    }

    private IEnumerator<WaitForSeconds> DashRecover(float time)
    {
        yield return new WaitForSeconds(time);

        isControllable = true;
        overallGravityModifier = 1;
        thisBody.drag = airDrag;
        currentMovementState = MovementState.SIMPLE;
    }

    private void CalculateStamina(GrabStates grabState)
    {
        if (groundCheckRealtime == true) { currentStamina = maxStamina; }
        else
        {
            //The player is grabbing and is not touching the ground, so decrease the stamina
            if (isGrabbing)
            {
                if (grabState == GrabStates.HOLD)
                {
                    currentStamina -= staminaConsumptionOnHold;
                }
                else if (grabState == GrabStates.CLIMB)
                {
                    currentStamina -= staminaConsumptionOnClimb;
                }
                else if (grabState == GrabStates.CLIMBJUMP)
                {
                    currentStamina -= staminaConsumptionOnClimbJump;
                }
            }
        }

        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
    }

    private void GrabMechanism()
    {
        canGrab = Physics2D.OverlapBox(hand.position, handSize, 0, grabMask);

        //Ckeck if player is close enough to a wall to grab it
        if (canGrab)
        {
            //If the player has stamina then allow the grab
            if (currentStamina > 0)
            {
                //Grab
                if (Keyboard.current.zKey.isPressed)
                {
                    SetGrab(true);
                }
                else
                {
                    SetGrab(false);
                }
            }
            else
            {
                SetGrab(false);
            }
        }
        else
        {
            SetGrab(false);
        }
    }

    private void SetGrab(bool value)
    {
        if (value == true)
        {
            //Set the correct grab state according to the current grab state
            if (inputY == 0) { currentGrabState = GrabStates.HOLD; }
            else { currentGrabState = GrabStates.CLIMB; }

            //Grabbing
            isControllable = false;
            isGrabbing = true;
            overallGravityModifier = 0;
            thisBody.drag = grabDrag;
            currentMovementState = MovementState.GRAB;
            //thisBody.velocity = new Vector2(thisBody.velocity.x, inputY * moveSpeed * grabMovementModifier);
            thisBody.AddForce(new Vector2(0, inputY * moveSpeed * grabMovementModifier), (ForceMode2D)ForceMode.Acceleration);
        }
        else
        {
            //If player was not grabbing earlier than what is the point of setting its grab to false
            if (currentMovementState != MovementState.GRAB) { return; }

            //Not grabbing
            isControllable = true;
            isGrabbing = false;
            thisBody.drag = airDrag;
            overallGravityModifier = 1;
            currentMovementState = MovementState.SIMPLE;
        }
    }

    private void OnDrawGizmos()
    {
        if (footVisualization == true) Gizmos.DrawCube(foot.position, footSize);
        if (handVisualization == true) Gizmos.DrawCube(hand.position, handSize);
    }
}

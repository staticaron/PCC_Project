using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MovementState { SIMPLE, DASH, GRAB };
public enum GrabStates { NONE, HOLD, CLIMB, CLIMBJUMP };

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(CircleCollider2D))]
public class PlayerController : MonoBehaviour
{
    #region Data Items

    private PlayerMovementAction playerMovementActionMap;
    private Rigidbody2D thisBody;

    public MovementState currentMovementState = MovementState.SIMPLE;
    public GrabStates currentGrabState = GrabStates.NONE;

    //Delegates and Events
    public delegate void Dashed(bool started);
    public static event Dashed EDashed;

    public delegate void Grabbed(bool started);
    public static event Grabbed EGrabbed;

    public delegate void Jumped(bool sterted);
    public static event Jumped EJumped;

    //The keyboard inputs for the specified axis
    [HideInInspector] public float inputX;
    [HideInInspector] public float inputY;
    private Vector2 thisRotation;

    //Basically needed for animations to work
    [HideInInspector] public float veriticalVelcity;

    [Header("Actions------------------------------------------------------------------------------")]
    [SerializeField] private bool runEnabled;
    [SerializeField] private bool jumpEnabled;
    [SerializeField] private bool dashEnabled;
    [SerializeField] private bool grabEnabled;

    //General Properties
    [Header("General Properties------------------------------------------------------------------------------")]
    [SerializeField] private bool isControllable;
    [SerializeField] private float moveSpeed = 10;

    [Header("Checkers------------------------------------------------------------------------------")]
    [SerializeField] private bool groundCheck;
    //GroundCheck = GroundCheckRealtime + coyotiness
    [SerializeField] public bool groundCheckRealtime;
    [SerializeField] private bool shoulderCheckRealtime;

    [Header("Foot Properties------------------------------------------------------------------------------")]
    [SerializeField] private Transform foot;
    [SerializeField] private Vector2 footSize = new Vector2(1, 0.5f);
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private bool footVisualization;


    [Header("Hand Properties------------------------------------------------------------------------------")]
    [SerializeField] private Transform hand;
    [SerializeField] private Transform shoulder;
    [SerializeField] private Vector2 handSize = new Vector2(.5f, 1);
    [SerializeField] private Vector2 shoulderSize = new Vector2(.5f, 1);
    [Tooltip("Objects in this layer can be grabbed")]
    [SerializeField] private LayerMask grabMask;
    [SerializeField] private bool handVisualization;

    [Header("Gravity Values------------------------------------------------------------------------------")]
    [SerializeField] private float overallGravityModifier = 1;
    [SerializeField] private float fallGravityModifier = 10;
    [SerializeField] private float jumpGravityModifier = 4;
    [SerializeField] private float airDrag = 1, landDrag = 0, dashDrag = 5, grabDrag = 8;

    [Header("Coyote Values------------------------------------------------------------------------------")]
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float groundCheckTimer;
    [SerializeField] private bool coyoteEnabled;

    [Header("Jump Values------------------------------------------------------------------------------")]
    [SerializeField] private int numberOfJumps = 1;
    [SerializeField] private int jumpsLeft;
    [SerializeField] private float jumpForce = 20;


    [Header("Dash Values------------------------------------------------------------------------------")]
    [Tooltip("Number of dashes allowed without touching the ground")]
    [SerializeField] private int numberOfDashes = 1;
    private int dashesLeft;
    [SerializeField] private float dashForce = 3000;
    [Tooltip("The vector in which the player will dash")]
    [SerializeField] private Vector2 dashVector;
    [Tooltip("It is the time by which dash is expected to end and the state is set to normal state from the dash state")]
    [SerializeField] private float dashRecoverTime = 0.3f;
    [Tooltip("The time less than the dash recover time by which controls are enabled so as to line up the landing")]
    [SerializeField] private float preDashRecoverTime = 0.1f;
    [Tooltip("Time offset between the key press and direction set")]
    [SerializeField] private bool canDash;

    [Header("Grab Values------------------------------------------------------------------------------")]
    [SerializeField] private int maxStamina = 300;
    private int currentStamina;
    [Tooltip("Changes the force applied while climb jumping (1 means same force as the jumpforce)")]
    [SerializeField] private float climbJumpForceModifier = 1;
    [Tooltip("Changes the speed of climbing (1 means same speed as moveSpeed)")]
    [SerializeField] private float grabMovementModifier = 0.3f;
    [SerializeField] private bool isGrabbing;
    [SerializeField] private bool handCheckRealtime;
    [SerializeField] private int staminaConsumptionOnHold = 1;           //Stamina that is consumed per frame while climb holding
    [SerializeField] private int staminaConsumptionOnClimb = 5;          //Stamina that is consumed per frame while climb climbing
    [SerializeField] private int staminaConsumptionOnClimbJump = 50;     //Stamina that is consumed per frame while climb jumping
    [SerializeField] private Vector2 grabJumpDirection;
    [Tooltip("The temporary direction of applied force while climb jumping. (1, 0.5) means force will be applied at (1, 0.5) if jump is made to right and (-1, 0.5) if the jump is made to the left")]
    [SerializeField] private Vector2 tempGrabJumpDirection = new Vector2(1, 1);
    [SerializeField] private Vector2 shoulderPushForceDirection;
    [Tooltip("The temp direction in which the shoulder push is applied.  (1, 0.5) means force will be applied at (1, 0.5) if character is grabbing right and (-1, 0.5) if character is grabbing left")]
    [SerializeField] private Vector2 tempShoulderPushDirection = new Vector2(1, 1);

    //Colliders
    private BoxCollider2D boxCollider;
    private CircleCollider2D circleCollider;

    #endregion

    private void Awake()
    {
        //Initialize variables
        playerMovementActionMap = new PlayerMovementAction();
        thisBody = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        circleCollider = GetComponent<CircleCollider2D>();

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

        if (runEnabled) SetHorizontalVelocity();

        GroundCheck();

        ApplyDrag();

        if (jumpEnabled) JumpMechanism();

        RemoveFloatyness();

        SetDash();

        if (dashEnabled) Dash();

        CalculateStamina(currentGrabState);

        if (grabEnabled) GrabMechanism();
    }

    private void SetValues()
    {
        //Do checks
        groundCheckRealtime = Physics2D.OverlapBox(foot.position, footSize, 0, groundMask);
        shoulderCheckRealtime = Physics2D.OverlapBox(shoulder.position, shoulderSize, 0, grabMask);
        handCheckRealtime = Physics2D.OverlapBox(hand.position, handSize, 0, grabMask);

        //For Animations
        veriticalVelcity = thisBody.velocity.y;
        thisRotation = transform.rotation.eulerAngles;

        //Set Dash Vector based on user input
        dashVector = new Vector2(inputX, inputY).normalized * dashForce;
        if (dashVector == Vector2.zero)
        {
            if (thisRotation.y == 0) { dashVector = Vector2.right * dashForce; }
            else if (thisRotation.y == 180) { dashVector = Vector2.left * dashForce; }
        }

        //Set Grab Jump Direction
        if (currentMovementState == MovementState.GRAB)
        {
            if (thisRotation.y == 0 && inputX < 0) { grabJumpDirection = (new Vector2(-tempGrabJumpDirection.x, tempGrabJumpDirection.y).normalized); }
            else if (thisRotation.y == 180 && inputX > 0) { grabJumpDirection = (new Vector2(tempGrabJumpDirection.x, tempGrabJumpDirection.y).normalized); }
            else { grabJumpDirection = Vector2.zero; }
        }

        //Set push force direction
        if (currentMovementState == MovementState.GRAB)
        {
            if (thisRotation.y == 0) { shoulderPushForceDirection = new Vector2(tempShoulderPushDirection.x, tempShoulderPushDirection.y); }
            else { shoulderPushForceDirection = new Vector2(-tempShoulderPushDirection.x, tempShoulderPushDirection.y); }
        }

        //Maintain the jump count
        if (groundCheckRealtime)
        {
            jumpsLeft = numberOfJumps;
        }
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
            if (groundCheck == true && Keyboard.current.cKey.wasPressedThisFrame && jumpsLeft > 0)
            {
                thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce);
                jumpsLeft -= 1;
            }
        }
        else if (currentMovementState == MovementState.GRAB)
        {
            if (grabJumpDirection != Vector2.zero)
            {
                if (isGrabbing == true && Keyboard.current.cKey.wasPressedThisFrame)
                {
                    thisBody.velocity = new Vector2(grabJumpDirection.x * jumpForce * climbJumpForceModifier, grabJumpDirection.y * jumpForce * climbJumpForceModifier);
                    currentGrabState = GrabStates.CLIMBJUMP;
                    jumpsLeft -= 1;
                }
            }
            else
            {
                if (isGrabbing == true && Keyboard.current.cKey.wasPressedThisFrame)
                {
                    thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce);
                    currentGrabState = GrabStates.CLIMBJUMP;
                }
            }

            //Note that in case of climb jumping, currentMovementState is not set to simple after the jump because it is set inside the grab function itself
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
        if (groundCheckRealtime == true)
        {
            canDash = true;
            dashesLeft = numberOfDashes;
        }
    }

    private void Dash()
    {
        if (canDash == false) return;

        //Apply dash time spent since the dash key was pressed is less than the dash time
        if (Keyboard.current.xKey.wasPressedThisFrame && currentMovementState != MovementState.DASH)
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

            //Start the timer to end the dash
            StartCoroutine(PreDashRecover(preDashRecoverTime));
            StartCoroutine(DashRecover(dashRecoverTime));

            //Enable the circle collider so as to smooth out the collisions
            circleCollider.enabled = true;
            boxCollider.enabled = false;

            //Trigger the event for anyone
            if (EDashed != null) EDashed(true);

            //Manage dash count and canDash
            dashesLeft -= 1;
            if (dashesLeft <= 0) canDash = false;
        }
    }

    private IEnumerator<WaitForSeconds> PreDashRecover(float time)
    {
        yield return new WaitForSeconds(time);

        //Control is given back before the complete dash takes place
        isControllable = true;
    }

    private IEnumerator<WaitForSeconds> DashRecover(float time)
    {
        yield return new WaitForSeconds(time);

        overallGravityModifier = 1;
        thisBody.drag = airDrag;
        currentMovementState = MovementState.SIMPLE;

        if (EDashed != null) { EDashed(false); }

        //Restore the colliders to original state
        boxCollider.enabled = true;
        circleCollider.enabled = false;
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
        //Ckeck if player is close enough to a wall to grab it else release
        if (handCheckRealtime == true && shoulderCheckRealtime == true && Keyboard.current.zKey.isPressed && currentStamina > 0)
        {
            SetGrab(true);
        }
        else { SetGrab(false); }

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
            thisBody.AddForce(new Vector2(0, inputY * moveSpeed * grabMovementModifier), (ForceMode2D)ForceMode.Acceleration);

            //Raise Event
            if (currentMovementState != MovementState.GRAB) { if (EGrabbed != null) EGrabbed(true); }

            currentMovementState = MovementState.GRAB;
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

            //Raise Event
            if (EGrabbed != null) EGrabbed(false);

            currentMovementState = MovementState.SIMPLE;
        }
    }

    private void OnDrawGizmos()
    {
        if (footVisualization == true) Gizmos.DrawCube(foot.position, footSize);
        if (handVisualization == true) Gizmos.DrawCube(hand.position, handSize);
        if (handVisualization == true) Gizmos.DrawCube(shoulder.position, shoulderSize);
    }
}

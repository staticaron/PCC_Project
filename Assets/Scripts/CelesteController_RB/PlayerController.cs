using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MovementState { SIMPLE, JUMP, DASH, GRAB };
public enum GrabStates { NONE, HOLD, CLIMB, CLIMBJUMP };

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(CircleCollider2D))]
public class PlayerController : MonoBehaviour
{
    #region Data Items

    private PlayerMovementAction playerMovementActionMap;
    private Rigidbody2D thisBody;

    [SerializeField] private MovementState currentMovementState = MovementState.SIMPLE;
    public MovementState CurrentMovementState
    {
        get { return currentMovementState; }
        set
        {
            switch (value)
            {
                case MovementState.SIMPLE:
                    canGrab = true;
                    break;
                case MovementState.JUMP:
                    canGrab = true;
                    break;
                case MovementState.DASH:
                    canGrab = false;
                    break;
                case MovementState.GRAB:
                    canGrab = true;
                    break;
            }

            currentMovementState = value;
        }
    }
    public GrabStates _currentGrabState = GrabStates.NONE;
    public GrabStates CurrentGrabState
    {
        get { return _currentGrabState; }
        set { _currentGrabState = value; }
    }

    //Delegates and Events
    public delegate void Dashed(bool started);
    public static event Dashed EDashed;

    public delegate void Grabbed(bool started);
    public static event Grabbed EGrabbed;

    public delegate void Jumped(bool sterted);
    public static event Jumped EJumped;

    //The keyboard inputs for the specified axis
    [Header("Input Values")]
    public float inputX;
    public float inputY;
    private Vector2 thisRotation;
    public Vector2 thisVelocity;

    [Header("Actions------------------------------------------------------------------------------")]
    [SerializeField] private bool runEnabled;
    [SerializeField] private bool climbEnabled;
    [SerializeField] private bool jumpEnabled;
    [SerializeField] private bool dashEnabled;
    [SerializeField] private bool grabEnabled;
    [Tooltip("Turn on to enable move with movable objects")]
    [SerializeField] private bool movableColliders;

    //General Properties
    [Header("General Properties------------------------------------------------------------------------------")]
    [SerializeField] private bool isControllableX = true;
    [SerializeField] private bool isControllableY = false;
    [SerializeField] private float moveSpeed = 10;
    [SerializeField] private float horizontalVelocityToSet;

    [Header("Checkers------------------------------------------------------------------------------")]
    [SerializeField] private bool groundCheck;
    //GroundCheck = GroundCheckRealtime + coyotiness
    [SerializeField] public bool groundCheckRealtime;
    [SerializeField] private bool oldGroundCheckRealtime;
    [SerializeField] public bool handCheckRealtime;
    [SerializeField] private bool shoulderCheckRealtime;
    [SerializeField] private bool movableCheckFoot;
    [SerializeField] private bool oldMovableCheckFoot;
    [SerializeField] private bool movableCheckHand;

    [Header("Foot Properties------------------------------------------------------------------------------")]
    [SerializeField] private Transform foot;
    [SerializeField] private float footLength;
    [SerializeField] private LayerMask groundMask;
    [Tooltip("Checks whether the object below belongs to the movable objects ( Not applicable if movable object interaction is turned off)")]
    [SerializeField] private LayerMask movableObjectMask;
    [Tooltip("Stores the Movable Object if there is any")]
    [SerializeField] private Collider2D movableColliderBelow;
    [SerializeField] private bool footVisualization;


    [Header("Hand Properties------------------------------------------------------------------------------")]
    [SerializeField] private Transform hand;
    [SerializeField] private Transform shoulder;
    [SerializeField] private float handLength = 0.25f;
    [SerializeField] private float shoulderLength = 0.25f;
    [Tooltip("Objects in this layer can be grabbed")]
    [SerializeField] private LayerMask grabMask;
    [SerializeField] private Collider2D movableColliderSide;
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
    [Tooltip("The speed is reduced to this much of the current speed so as to cancel the jump")]
    [SerializeField] private float jumpCancelModifier = 0.5f;

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
    [SerializeField] private float microscopicPauseTime;
    [SerializeField] private bool canDash;

    [Header("Grab Properties-----------------------------------------------------------------------")]
    [Tooltip("1 : Climb with same speed as that of run")]
    [SerializeField] private float climbSpeedModifier;
    [SerializeField] private int maxStaminaPoints;
    [SerializeField] private int currentStaminaPoints;
    [SerializeField] private int holdStaminaConsumption;
    [SerializeField] private int climbStaminaConsumption;
    [SerializeField] private int climbJumpStaminaConsumption;
    [SerializeField] private bool grabInput;
    [SerializeField] private bool canGrab;
    [SerializeField] private Vector2 grabJumpDirection;
    [SerializeField] private Vector2 tempGrabJumpDirection = new Vector2(1, 1);

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

        //Bind Functions with the input action map
        playerMovementActionMap.General.Jump.started += ctx => Jump();
        playerMovementActionMap.General.Jump.canceled += ctx => JumpCancel();
        playerMovementActionMap.General.Dash.started += ctx => Dash();
        playerMovementActionMap.General.Grab.started += ctx => Grab(ctx);
        playerMovementActionMap.General.Grab.canceled += ctx => Grab(ctx);
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

        GetInput();

        CheckGrabInput();
    }

    private void FixedUpdate()
    {
        if (runEnabled) SetHorizontalVelocity();
        if (climbEnabled) SetVerticalVelocity();

        GroundCheck();

        ApplyDrag();

        RemoveFloatyness();
    }

    private void SetValues()
    {
        //Do checks for ground and movable objects
        groundCheckRealtime = Physics2D.Raycast(foot.position, -transform.up, footLength, groundMask);

        handCheckRealtime = Physics2D.Raycast(hand.position, transform.right, handLength, grabMask);
        shoulderCheckRealtime = Physics2D.Raycast(shoulder.position, transform.right, shoulderLength, grabMask);

        //For Animations
        thisVelocity = thisBody.velocity;
        thisRotation = transform.rotation.eulerAngles;

        //Regain Jump and stamina
        if (groundCheckRealtime == true && oldGroundCheckRealtime == false)
        {
            if (CurrentMovementState == MovementState.JUMP) CurrentMovementState = MovementState.SIMPLE;
        }

        GetDashDirection();

        //Maintain the number of dashes
        SetDash();

        if (CurrentMovementState == MovementState.GRAB) GetJumpDirection();
        CalculateStamina();
        if (CurrentMovementState == MovementState.GRAB) SetGrabState();

        //Maintain the jump count
        if ((groundCheckRealtime == true && oldGroundCheckRealtime == false) || (movableCheckFoot == true && oldMovableCheckFoot == false))
        {
            jumpsLeft = numberOfJumps;
        }

        oldGroundCheckRealtime = groundCheckRealtime;
    }

    private void GetDashDirection()
    {
        dashVector = new Vector2(inputX, inputY).normalized * dashForce;

        if (dashVector == Vector2.zero)
        {
            //If normal state then apply dash in the forward direction 
            if (CurrentMovementState == MovementState.SIMPLE || CurrentMovementState == MovementState.JUMP)
            {
                if (thisRotation.y == 0) { dashVector = Vector2.right * dashForce; }
                else if (thisRotation.y == 180) { dashVector = Vector2.left * dashForce; }
            }
            else if (CurrentMovementState == MovementState.GRAB) //If grabbing then apply dash opposite to the forward direction because the forward direction is towards the wall
            {
                if (thisRotation.y == 0) { dashVector = Vector2.left * dashForce; }
                else if (thisRotation.y == 180) { dashVector = Vector2.right * dashForce; }
            }
        }
    }

    private void GetJumpDirection()
    {
        /* If facing right and pressing left then set the jump direction to the left
        If facing left and pressing right then set the jump direction to the right
        If facing left and pressing any other direction then set the jump direction to up
        Similarly if facing right and pressing any other direction then set jump direction to up*/
        if (thisRotation.y == 0)
        {
            if (inputX < 0) { grabJumpDirection = (new Vector2(-tempGrabJumpDirection.x, tempGrabJumpDirection.y)).normalized; }
            else { grabJumpDirection = Vector2.up; }
        }
        else
        {
            if (inputX > 0) { grabJumpDirection = tempGrabJumpDirection.normalized; }
            else { grabJumpDirection = Vector2.up; }
        }
    }

    private void SetGrabState()
    {
        if (thisVelocity.y != 0) { CurrentGrabState = GrabStates.CLIMB; }
        else CurrentGrabState = GrabStates.HOLD;
    }

    private void CalculateStamina()
    {
        if (groundCheckRealtime) { currentStaminaPoints = maxStaminaPoints; }

        if (CurrentMovementState == MovementState.GRAB)
        {
            if (CurrentGrabState == GrabStates.HOLD) { currentStaminaPoints -= holdStaminaConsumption; }
            else if (CurrentGrabState == GrabStates.CLIMB) { currentStaminaPoints -= climbStaminaConsumption; }
            else if (CurrentGrabState == GrabStates.CLIMBJUMP) { currentStaminaPoints -= climbJumpStaminaConsumption; }
        }

        //Check for the percentage of the stamina and call event for low stamina
    }

    private void GetInput()
    {
        inputX = playerMovementActionMap.General.Movement.ReadValue<float>();
        inputY = playerMovementActionMap.General.VerticalMovement.ReadValue<float>();
    }

    private void SetHorizontalVelocity()
    {
        if (isControllableX == false) return;

        horizontalVelocityToSet = inputX * moveSpeed;

        thisBody.velocity = new Vector2(horizontalVelocityToSet, thisBody.velocity.y);
    }

    private void SetVerticalVelocity()
    {
        if (isControllableY == false) return;

        var vertVel = inputY * moveSpeed * climbSpeedModifier;
        thisBody.velocity = new Vector2(thisBody.velocity.x, vertVel);
    }

    private void Jump()
    {
        if (CurrentMovementState == MovementState.SIMPLE || CurrentMovementState == MovementState.JUMP)
        {
            if (jumpsLeft > 0)
            {
                ApplyJumpForce(Vector2.up);
                jumpsLeft -= 1;
                CurrentMovementState = MovementState.JUMP;
            }
        }
        else if (currentMovementState == MovementState.GRAB && jumpsLeft > 0)
        {
            ApplyJumpForce(grabJumpDirection);
            CurrentMovementState = MovementState.JUMP;
        }
    }

    private void ApplyJumpForce(Vector2 dir)
    {
        if (dir == Vector2.up)
        {
            thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce);
        }
        else
        {
            thisBody.velocity = new Vector2(dir.x * jumpForce, dir.y * jumpForce);
        }
    }

    private void JumpCancel()
    {
        //Cancel the jump if jumping
        if (CurrentMovementState != MovementState.JUMP) return;

        if (thisBody.velocity.y < 0) return;

        thisBody.velocity = new Vector2(thisBody.velocity.x, thisBody.velocity.y * jumpCancelModifier);
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
        if (CurrentMovementState == MovementState.SIMPLE)
        {
            if (groundCheckRealtime == true || movableCheckFoot == true)
            {
                thisBody.drag = landDrag;
            }
            else
            {
                thisBody.drag = airDrag;
            }
        }
        else if (CurrentMovementState == MovementState.DASH)
        {
            thisBody.drag = dashDrag;
        }

        //TODO : Apply Drag here
    }

    //Can be called from another script (crystal) to replenish the dash
    public void SetDash(bool value, int numberOfDashes)
    {
        canDash = value;
        dashesLeft = numberOfDashes;
    }

    private void SetDash()
    {
        if (CurrentMovementState == MovementState.DASH) return;

        if (groundCheckRealtime == true || movableCheckFoot == true)
        {
            canDash = true;
            dashesLeft = numberOfDashes;
        }
    }

    private void Dash()
    {
        if (canDash == false) return;

        if (CurrentMovementState != MovementState.DASH)
        {
            CurrentMovementState = MovementState.DASH;

            if (dashVector == new Vector2(1 * dashForce, 0) || dashVector == new Vector2(-1 * dashForce, 0))
            {
                //If dashing sideways then overall gravity acting on the player should be zero to give a uncontrolled situation.
                isControllableX = false;
                thisBody.velocity = Vector2.zero;
                overallGravityModifier = 0;
            }
            else
            {
                //If not dashing sideways then overall gravity should be normal as there is no need of uncontrolled situation as it is already is
                isControllableX = false;
                thisBody.velocity = Vector2.zero;
            }

            //Apply actual dash force
            thisBody.AddForce(dashVector);

            //Start the timer to end the dash
            StartCoroutine(PreDashRecover(preDashRecoverTime));
            StartCoroutine(DashRecover(dashRecoverTime));

            //Pause
            StartCoroutine(MicroscopicPause(microscopicPauseTime));

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
        isControllableX = true;
    }

    private IEnumerator<WaitForSeconds> DashRecover(float time)
    {
        yield return new WaitForSeconds(time);

        overallGravityModifier = 1;
        thisBody.drag = airDrag;

        //If another state was set before the dash is cancelled then dont set the simple state and let the current state be whatever it is
        if (CurrentMovementState == MovementState.DASH) CurrentMovementState = MovementState.SIMPLE;

        if (EDashed != null) { EDashed(false); }

        //Restore the colliders to original state
        boxCollider.enabled = true;
        circleCollider.enabled = false;

    }

    private IEnumerator<WaitForSecondsRealtime> MicroscopicPause(float time)
    {
        var originalTimeScale = Time.timeScale;
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(time);
        Time.timeScale = originalTimeScale;
    }

    private void Grab(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started) { grabInput = true; }
        else { grabInput = false; }
    }

    private void CheckGrabInput()
    {
        if (!canGrab) return;

        if (grabInput && handCheckRealtime && currentStaminaPoints > 0)
        {
            SetValuesForGrab();
        }
        else
        {
            if (CurrentMovementState == MovementState.GRAB) CurrentMovementState = MovementState.SIMPLE;
            SetNormalValues();
        }
    }

    private void SetValuesForGrab()
    {
        CurrentMovementState = MovementState.GRAB;
        isControllableX = false;
        isControllableY = true;
        overallGravityModifier = 0;
    }

    private void SetNormalValues()
    {
        isControllableX = true;
        isControllableY = false;
        overallGravityModifier = 1;
    }

    private void OnDrawGizmos()
    {
        if (footVisualization == true) Debug.DrawLine(foot.position, foot.position - new Vector3(0, footLength, 0), Color.red);
        if (thisRotation.y == 0) { if (handVisualization == true) Debug.DrawLine(hand.position, hand.position + new Vector3(handLength, 0, 0), Color.green); }
        else { Debug.DrawLine(hand.position, hand.position - new Vector3(handLength, 0, 0), Color.green); }
        if (thisRotation.y == 0) { if (handVisualization == true) Debug.DrawLine(shoulder.position, shoulder.position + new Vector3(shoulderLength, 0, 0), Color.cyan); }
        else { Debug.DrawLine(shoulder.position, shoulder.position - new Vector3(shoulderLength, 0, 0), Color.cyan); }
    }
}

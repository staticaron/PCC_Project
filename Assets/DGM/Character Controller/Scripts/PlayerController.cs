using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MovementState { SIMPLE, JUMP, DASH, GRAB };
public enum GrabState { NONE, HOLD, CLIMB, CLIMBJUMP };
public enum AnimationState { IDLE, RUN, JUMP_GOINGUP, JUMP_GOINGDOWN, DASH, GRAB };

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(CapsuleCollider2D))]
public class PlayerController : MonoBehaviour
{
    private PlayerMovementAction playerMovementActionMap;
    private Rigidbody2D thisBody;

    [Header("States--------------------------------------------------------------------------------")]
    [SerializeField] private MovementState currentMovementState = MovementState.SIMPLE;
    public MovementState CurrentMovementState
    {
        get { return currentMovementState; }
        set
        {
            if (isChangeInMovementStateEnabled == false) return;

            switch (value)
            {
                case MovementState.SIMPLE:
                    canGrab = true;
                    break;
                case MovementState.JUMP:
                    if (CurrentGrabState == GrabState.NONE) { canGrab = true; }
                    else { canGrab = false; SetNormalValues(); }
                    break;
                case MovementState.DASH:
                    canGrab = false;
                    isChangeInMovementStateEnabled = false;
                    break;
                case MovementState.GRAB:
                    canGrab = true;
                    break;
            }

            currentMovementState = value;
        }
    }

    [SerializeField] private GrabState _currentGrabState = GrabState.NONE;
    public GrabState CurrentGrabState
    {
        get { return _currentGrabState; }
        set { _currentGrabState = value; }
    }

    [SerializeField] private AnimationState _currentAnimationState = AnimationState.IDLE;
    public AnimationState CurrentAnimationState
    {
        get { return _currentAnimationState; }
        set
        {
            if (value == _currentAnimationState) return;

            //If Current Animation State is Dash and the value is another state then this means dash
            //has ended. 
            if (_currentAnimationState == AnimationState.DASH) if (EDashed != null) EDashed(false);
            if (_currentAnimationState == AnimationState.GRAB) if (EGrabbed != null) EGrabbed(false);

            ChangeState(value);
            _currentAnimationState = value;
        }
    }

    #region DataItems
    //Delegates and Events

    public delegate void Dashed(bool started);
    public static event Dashed EDashed;

    public delegate void Grabbed(bool started);
    public static event Grabbed EGrabbed;

    public delegate void Jumped(bool goingUp);
    public static event Jumped EJumped;

    public delegate void Movement(bool isMoving);
    public static event Movement EMovement;

    //The keyboard inputs for the specified axis
    [Header("Input Values")]
    [HideInInspector] public float inputX;
    [HideInInspector] public float inputY;
    [HideInInspector] private Vector2 thisRotation;
    [HideInInspector] public Vector2 thisVelocity;

    [Header("Actions------------------------------------------------------------------------------")]
    [SerializeField] private bool runEnabled;
    [SerializeField] private bool climbEnabled;
    [SerializeField] private bool jumpEnabled;
    [SerializeField] private bool dashEnabled;
    [SerializeField] private bool grabEnabled;

    //General Properties
    [Header("General Properties------------------------------------------------------------------------------")]
    [SerializeField] private float moveSpeed = 10;
    private bool isControllableX = true;
    private bool isControllableY = false;
    private float horizontalVelocityToSet;
    private bool isChangeInGrabStateEnabled = true;
    private bool isChangeInMovementStateEnabled = true;

    [Header("Checkers (Only for reference, Can't be edited)------------------------------------------------------------------------------")]
    [SerializeField] private bool groundCheck; //GroundCheck = GroundCheckRealtime + coyotiness
    [SerializeField] public bool groundCheckRealtime;
    [HideInInspector] private bool oldGroundCheckRealtime;
    [SerializeField] private bool handCheckRealtime;
    [SerializeField] private bool oldHandCheckRealtime;
    [SerializeField] private bool shoulderCheckRealtime;
    [SerializeField] private bool ledgeNearby;

    [Header("Foot Properties------------------------------------------------------------------------------")]
    [SerializeField] private Transform foot;
    [SerializeField] private float footLength;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private bool footVisualization;


    [Header("Hand Properties------------------------------------------------------------------------------")]
    [SerializeField] private Transform hand;
    [SerializeField] private Transform shoulder;
    [SerializeField] private float handLength = 0.25f;
    [SerializeField] private float shoulderLength = 0.25f;
    [Tooltip("Objects in this layer can be grabbed")]
    [SerializeField] private LayerMask grabMask;
    [SerializeField] private bool handVisualization;

    [Header("Gravity Properties-------------------------------------------------------------------------")]
    [SerializeField] private float fallGravityModifier = 10;
    [SerializeField] private float jumpGravityModifier = 4;
    private float overallGravityModifier = 1;
    [SerializeField] private float airDrag = 1, landDrag = 0, dashDrag = 5, grabDrag = 8;

    [Header("Coyote Values------------------------------------------------------------------------------")]
    [SerializeField] private float coyoteTime = 0.2f;
    private float groundCheckTimer;
    private bool coyoteEnabled;

    [Header("Jump Properties------------------------------------------------------------------------------")]
    [Tooltip("If true, the jump will be of fixed height, else will depend on the duration of the key press")]
    [SerializeField] private bool fixedJumpHeight;
    [SerializeField] private int numberOfJumps = 1;
    private int jumpsLeft;
    [SerializeField] private float jumpForce = 20;
    [SerializeField] private float jumpForceAway = 40;
    [Tooltip("The speed is reduced to this much of the current speed so as to cancel the jump \n( Note : No effect if fixedJumpHeight is enabled)")]
    [SerializeField] private float jumpCancelModifier = 0.5f;

    [Header("Dash Properties------------------------------------------------------------------------------")]
    [Tooltip("Number of dashes allowed without touching the ground")]
    [SerializeField] private int numberOfDashes = 1;
    private int dashesLeft;
    [SerializeField] private float dashForce = 3000;
    private Vector2 dashVector; //The vector in which the player will dash
    [Tooltip("It is the time by which dash is expected to end and the state is set to normal state from the dash state")]
    [SerializeField] private float dashRecoverTime = 0.3f;
    [Tooltip("The time less than the dash recover time by which controls are enabled so as to line up the landing")]
    [SerializeField] private float preDashRecoverTime = 0.1f;
    [Tooltip("The time for which the screen stops at the time of dash")]
    [SerializeField] private float microscopicPauseTime;
    private bool canDash;

    [Header("Grab Properties-----------------------------------------------------------------------")]
    [SerializeField] private bool isGrabbing;
    [Tooltip("1 : Climb with same speed as that of run \n 0 : Dont Climb")]
    [SerializeField] private float climbSpeedModifier;
    [Tooltip("1 means apply the force as jump force for the climb jump\n0 means apply no force")]
    [SerializeField] private float climbJumpForceModifier;
    [Tooltip("1 means apply the same force as the jump force for climbing the ledge\n0 means apply no force")]
    [SerializeField] private float ledgeJumpForceModifier;
    [SerializeField] private int maxStaminaPoints;
    private int currentStaminaPoints;
    private bool staminaConsumptionEnabled = true;
    [Tooltip("The stamina consumed per frame while holding any object")]
    [SerializeField] private int holdStaminaConsumption;
    [Tooltip("The stamina consumed per frame while climbing any object")]
    [SerializeField] private int climbStaminaConsumption;
    [Tooltip("The stamina consumed at the instant when climb jump is performed on any object")]
    [SerializeField] private int climbJumpStaminaConsumption;
    [Tooltip("The time by which a climb jump is expected to end since it was started \n(This is value that is set by testing)")]
    [SerializeField] private float climbJumpTime;
    private bool grabInput;
    [SerializeField] private bool canGrab = true;
    private Vector2 grabJumpDirection;  //This is the direction in which jump will happen if grabbing any object
    [Tooltip("Temporary Direction of jumping away from the wall. If the value is (1, 1) then jump force will be added in (-1, 1) if the grabbing on the right side and in (1, 1) if grabbing on the left side")]
    [SerializeField] private Vector2 tempGrabJumpDirection = new Vector2(1, 1);

    /*Colliders, box Collider is the normal collider active all the time except while dashing
    circle collider is only active while dashing to smooth out the collision while dashing */
    private BoxCollider2D boxCollider;
    private CapsuleCollider2D roundCollider;
    #endregion

    private void Awake()
    {
        //Initialize variables
        playerMovementActionMap = new PlayerMovementAction();
        thisBody = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        roundCollider = GetComponent<CapsuleCollider2D>();

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

        HandleAnimationEvents();

        GetInput();

        Rotate();

        ApplyGrab();

        GroundCheck();

        ApplyDrag();

        RemoveFloatiness();

        oldGroundCheckRealtime = groundCheckRealtime;
        oldHandCheckRealtime = handCheckRealtime;
    }

    private void FixedUpdate()
    {
        SetHorizontalVelocity();
        SetVerticalVelocity();
    }

    private void SetValues()
    {
        //Do checks for ground, hand and shoulder
        groundCheckRealtime = Physics2D.Raycast(foot.position, -transform.up, footLength, groundMask);
        handCheckRealtime = Physics2D.Raycast(hand.position, transform.right, handLength, grabMask);
        shoulderCheckRealtime = Physics2D.Raycast(shoulder.position, transform.right, shoulderLength, grabMask);


        thisVelocity = thisBody.velocity;
        thisRotation = transform.rotation.eulerAngles;

        //Check for ledge nearby
        if (handCheckRealtime == true && shoulderCheckRealtime == false) { ledgeNearby = true; }
        else { ledgeNearby = false; }

        //Regain Jump if touched the ground and reset the jumpsLeft
        if (groundCheckRealtime == true && oldGroundCheckRealtime == false)
        {
            if (CurrentMovementState == MovementState.JUMP) CurrentMovementState = MovementState.SIMPLE;
            jumpsLeft = numberOfJumps;
        }

        //Get the direction in which force will be applied in case dash is initialised
        GetDashDirection();

        //Maintain the number of dashes
        SetDash();

        //Grab Stuff
        if (CurrentMovementState == MovementState.GRAB) GetJumpDirection();
        CalculateStamina();
        if (CurrentMovementState == MovementState.GRAB) SetGrabState();
        else CurrentGrabState = GrabState.NONE;
    }

    private void GetDashDirection()
    {
        //Normal Dash Vector is equal to the input vector
        dashVector = new Vector2(inputX, inputY).normalized * dashForce;

        //In case input vector is zero, set the dash direction equal to the facing direction
        if (dashVector == Vector2.zero)
        {
            //If normal state or jump state then apply dash in the forward direction 
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
        if (isChangeInGrabStateEnabled == false) return;

        if (thisVelocity.y != 0) { CurrentGrabState = GrabState.CLIMB; }
        else CurrentGrabState = GrabState.HOLD;
    }

    private void CalculateStamina()
    {
        //If grounded then don't calculate stamina just set it to maximum
        if (groundCheckRealtime) { currentStaminaPoints = maxStaminaPoints; return; }

        /*Stamina consumption is enabled while holding and climbing because in these states,
        stamina is consumed per frame but in case of climb jump, stamina is consumed at the
        moment of jumping, so once the stamina in case of climb jump is consumed, stamina 
        consumption is set to false till another state is set and then stamina consumption resumes 
        according to the state*/

        if (CurrentMovementState == MovementState.GRAB)
        {
            if (CurrentGrabState == GrabState.HOLD)
            {
                staminaConsumptionEnabled = true;
                currentStaminaPoints -= holdStaminaConsumption;
            }
            else if (CurrentGrabState == GrabState.CLIMB)
            {
                staminaConsumptionEnabled = true;
                currentStaminaPoints -= climbStaminaConsumption;
            }
            else if (CurrentGrabState == GrabState.CLIMBJUMP)
            {
                //Only reduce the stamina points at the time of jump
                if (staminaConsumptionEnabled == false) return;

                currentStaminaPoints -= climbJumpStaminaConsumption;
                staminaConsumptionEnabled = false;
            }

        }

        //TODO : Check for the percentage of the stamina and call event for low stamina
    }

    private void GetInput()
    {
        inputX = playerMovementActionMap.General.Movement.ReadValue<float>();
        inputY = playerMovementActionMap.General.VerticalMovement.ReadValue<float>();
    }

    private void SetHorizontalVelocity()
    {
        if (isControllableX == false) return;

        if (runEnabled == true) { horizontalVelocityToSet = inputX * moveSpeed; }
        else { horizontalVelocityToSet = 0; }

        thisBody.velocity = new Vector2(horizontalVelocityToSet, thisBody.velocity.y);
    }

    private void SetVerticalVelocity()
    {
        if (isControllableY == false) return;
        if (CurrentGrabState == GrabState.CLIMBJUMP) return;

        float vertVel;
        if (climbEnabled == false) { vertVel = 0; }
        else { vertVel = inputY * moveSpeed * climbSpeedModifier; }

        if (ledgeNearby)
        {
            thisBody.velocity = new Vector2(thisBody.velocity.x, Mathf.Clamp(vertVel, vertVel, 0));
        }
        else { thisBody.velocity = new Vector2(thisBody.velocity.x, vertVel); }
    }

    private void Rotate()
    {
        //If in simple state, set the rotation according to the input else if in dash state set the rotation according to the movement direction
        if (CurrentMovementState == MovementState.SIMPLE || CurrentMovementState == MovementState.JUMP)
        {
            if (this.inputX > 0)
            {
                transform.rotation = Quaternion.Euler(0, 0, 0);
            }
            else if (this.inputX < 0)
            {
                transform.rotation = Quaternion.Euler(0, 180, 0);
            }
        }
        else if (CurrentMovementState == MovementState.DASH)
        {
            if (thisVelocity.x > 0) { transform.rotation = Quaternion.Euler(0, 0, 0); }
            else if (thisVelocity.x < 0) { transform.rotation = Quaternion.Euler(0, 180, 0); }
        }
    }

    private void HandleAnimationEvents()
    {
        if (CurrentAnimationState == AnimationState.IDLE)
        {
            if (groundCheckRealtime)
            {
                if (inputX != 0)
                {
                    CurrentAnimationState = AnimationState.RUN;
                }
            }
            else
            {
                CurrentAnimationState = AnimationState.JUMP_GOINGDOWN;
            }
        }
        else if (CurrentAnimationState == AnimationState.RUN)
        {
            if (groundCheckRealtime)
            {
                if (inputX == 0)
                {
                    CurrentAnimationState = AnimationState.IDLE;

                }
            }
            else
            {
                CurrentAnimationState = AnimationState.JUMP_GOINGDOWN;
            }
        }
        else if (CurrentAnimationState == AnimationState.JUMP_GOINGUP)
        {
            //If falling on the ground then set the state to idle
            if (groundCheckRealtime == true && oldGroundCheckRealtime == false) { CurrentAnimationState = AnimationState.IDLE; return; }

            if (thisVelocity.y < 0)
            {
                CurrentAnimationState = AnimationState.JUMP_GOINGDOWN;
            }
        }
        else if (CurrentAnimationState == AnimationState.JUMP_GOINGDOWN)
        {
            //If falling on the ground then set the state to idle
            if (groundCheckRealtime == true && oldGroundCheckRealtime == false)
            {
                CurrentAnimationState = AnimationState.IDLE;
            }
        }
        else if (CurrentAnimationState == AnimationState.DASH) { }
        else if (CurrentAnimationState == AnimationState.GRAB)
        {
            if (isGrabbing == false)
            {
                if (CurrentMovementState == MovementState.DASH)
                {
                    CurrentAnimationState = AnimationState.DASH;
                }
                else
                {
                    if (groundCheckRealtime == false) { CurrentAnimationState = AnimationState.JUMP_GOINGUP; }
                    else CurrentAnimationState = AnimationState.IDLE;
                }
            }
        }
    }

    private void ChangeState(AnimationState stateToSet)
    {
        if (stateToSet == AnimationState.IDLE) { if (EMovement != null) EMovement(false); }
        else if (stateToSet == AnimationState.RUN) { if (EMovement != null) EMovement(true); }
        else if (stateToSet == AnimationState.JUMP_GOINGUP) { if (EJumped != null) EJumped(true); }
        else if (stateToSet == AnimationState.JUMP_GOINGDOWN) { if (EJumped != null) EJumped(false); }
        else if (stateToSet == AnimationState.DASH) { if (EDashed != null) EDashed(true); }
        else if (stateToSet == AnimationState.GRAB) { if (EGrabbed != null) EGrabbed(true); }
    }

    private void Jump()
    {
        if (jumpEnabled == false) return;

        if (CurrentMovementState == MovementState.SIMPLE || CurrentMovementState == MovementState.JUMP)
        {
            if (jumpsLeft > 0)
            {
                ApplyJumpForceAndSetState(false, Vector2.up);
                jumpsLeft -= 1;
            }
        }
        else if (currentMovementState == MovementState.GRAB)
        {
            ApplyJumpForceAndSetState(true, grabJumpDirection);
        }
    }

    /*If the jump is made while grabbing then isGrabJump is true and conditions specific to the
    Climb jump are applied else normal jump takes place */
    private void ApplyJumpForceAndSetState(bool isGrabJump, Vector2 dir)
    {
        CurrentAnimationState = AnimationState.JUMP_GOINGUP;

        //If normal jump, apply jump force normally
        if (isGrabJump == false)
        {
            thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce);
            CurrentMovementState = MovementState.JUMP;
        }
        else if (isGrabJump == true) //If jumping while grabbing apply specific conditions
        {
            if (dir == Vector2.up)
            {
                if (ledgeNearby == false)
                {
                    //As this is a climb Jump, so no need to set the state to jump
                    StartCoroutine(WaitForJump(climbJumpTime));
                    thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce * climbJumpForceModifier);
                    CurrentMovementState = MovementState.GRAB;
                }
                else
                {
                    thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce * ledgeJumpForceModifier);
                    CurrentMovementState = MovementState.JUMP;
                }
            }
            else //This climb jump away from the wall in the air, so set the state accordingly
            {
                thisBody.velocity = new Vector2(dir.x * jumpForceAway, dir.y * jumpForceAway);
                CurrentMovementState = MovementState.JUMP;
            }
        }
    }

    //Wait for the climb jump to complete and allow change in state
    private IEnumerator<WaitForSeconds> WaitForJump(float time)
    {
        /*Sets the grab state to climb jump and then stops the change in state for
        some time while the jump is carried out
        After that time, change in grab state is allowed and state is set according to the situation*/
        CurrentGrabState = GrabState.CLIMBJUMP;
        CurrentAnimationState = AnimationState.JUMP_GOINGUP;
        isChangeInGrabStateEnabled = false;
        yield return new WaitForSeconds(time);
        isChangeInGrabStateEnabled = true;
        if (isGrabbing) CurrentAnimationState = AnimationState.GRAB;    //Dont Set the state to grab state if climb jump doesnt leads to grabbing
    }

    private void JumpCancel()
    {
        if (fixedJumpHeight == true) return;
        if (CurrentMovementState != MovementState.JUMP) return;
        if (thisBody.velocity.y < 0) return;

        thisBody.velocity = new Vector2(thisBody.velocity.x, thisBody.velocity.y * jumpCancelModifier);
    }

    //Apply different gravity values in different phases of the jump to remove floatiness
    private void RemoveFloatiness()
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
        if (CurrentMovementState == MovementState.SIMPLE || CurrentMovementState == MovementState.JUMP)
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
        else if (CurrentMovementState == MovementState.DASH)
        {
            thisBody.drag = dashDrag;
        }
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

        if (groundCheckRealtime == true)
        {
            canDash = true;
            dashesLeft = numberOfDashes;
        }
    }

    private void Dash()
    {
        if (dashEnabled == false) return;
        if (canDash == false) return;

        if (CurrentMovementState != MovementState.DASH)
        {
            CurrentMovementState = MovementState.DASH;
            CurrentAnimationState = AnimationState.DASH;

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
            roundCollider.enabled = true;
            boxCollider.enabled = false;

            //Manage dash count and canDash
            dashesLeft -= 1;
            if (dashesLeft <= 0) canDash = false;
        }
    }

    //This func is called before the dashRecover, so as to enable the controls before the actual dash is cancelled to allow lining up the landing
    private IEnumerator<WaitForSeconds> PreDashRecover(float time)
    {
        yield return new WaitForSeconds(time);

        //Control is given back before the complete dash takes place
        isControllableX = true;
    }

    //Sets the states and enables the gravity to end the dash
    private IEnumerator<WaitForSeconds> DashRecover(float time)
    {
        yield return new WaitForSeconds(time);

        overallGravityModifier = 1;
        thisBody.drag = airDrag;

        //If another state was set before the dash is cancelled then dont set the simple state and let the current state be whatever it is
        if (CurrentMovementState == MovementState.DASH)
        {
            isChangeInMovementStateEnabled = true;
            CurrentMovementState = MovementState.SIMPLE;
            //If in air, movement down, if on ground play idle
            if (groundCheckRealtime) { CurrentAnimationState = AnimationState.IDLE; }
            else { CurrentAnimationState = AnimationState.JUMP_GOINGDOWN; }
        }

        //Restore the colliders to original state
        boxCollider.enabled = true;
        roundCollider.enabled = false;

    }

    //Pauses the screen for a moment for dash 
    private IEnumerator<WaitForSecondsRealtime> MicroscopicPause(float time)
    {
        var originalTimeScale = Time.timeScale;
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(time);
        Time.timeScale = originalTimeScale;
    }

    //Enables or Disables the grab process according to the input
    private void Grab(InputAction.CallbackContext ctx)
    {
        if (grabEnabled == false) return;

        if (ctx.phase == InputActionPhase.Started) { grabInput = true; }
        else { grabInput = false; }
    }

    private void ApplyGrab()
    {
        if (handCheckRealtime == false && oldHandCheckRealtime == true)
        {
            //If the state is dash state then don't set can grab to true as ledge cannot be grabbed while dashing
            if (CurrentMovementState != MovementState.DASH) canGrab = true;
        }

        if (!canGrab) return;

        //The Code Below is for grabbing, so it must not be called if climb jumping
        if (grabInput && handCheckRealtime && currentStaminaPoints > 0)
        {
            SetGrabValues();
            if (CurrentGrabState != GrabState.CLIMBJUMP) CurrentAnimationState = AnimationState.GRAB;
        }
        else
        {
            if (CurrentMovementState == MovementState.GRAB) CurrentMovementState = MovementState.SIMPLE;
            SetNormalValues();
        }
    }

    //Sets the values favourable for grab
    private void SetGrabValues()
    {
        CurrentMovementState = MovementState.GRAB;
        isControllableX = false;
        isControllableY = true;
        overallGravityModifier = 0;
        isGrabbing = true;
    }

    //Sets the normal values
    private void SetNormalValues()
    {
        isControllableX = true;
        isControllableY = false;
        overallGravityModifier = 1;
        isGrabbing = false;
    }

    //Help Visuals
    private void OnDrawGizmos()
    {
        if (footVisualization == true) Debug.DrawLine(foot.position, foot.position - new Vector3(0, footLength, 0), Color.red);
        if (thisRotation.y == 0) { if (handVisualization == true) Debug.DrawLine(hand.position, hand.position + new Vector3(handLength, 0, 0), Color.green); }
        else { Debug.DrawLine(hand.position, hand.position - new Vector3(handLength, 0, 0), Color.green); }
        if (thisRotation.y == 0) { if (handVisualization == true) Debug.DrawLine(shoulder.position, shoulder.position + new Vector3(shoulderLength, 0, 0), Color.cyan); }
        else { Debug.DrawLine(shoulder.position, shoulder.position - new Vector3(shoulderLength, 0, 0), Color.cyan); }
    }
}

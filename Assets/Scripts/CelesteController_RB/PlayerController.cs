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
    [SerializeField] private bool shoulderCheckRealtime;
    [SerializeField] private bool movableCheckFoot;
    [SerializeField] private bool movableCheckHand;

    [Header("Foot Properties------------------------------------------------------------------------------")]
    [SerializeField] private Transform foot;
    [SerializeField] private Vector2 footSize = new Vector2(1, 0.5f);
    [SerializeField] private LayerMask groundMask;
    [Tooltip("Checks whether the object below belongs to the movable objects ( Not applicable if movable object interaction is turned off)")]
    [SerializeField] private LayerMask movableObjectMask;
    [Tooltip("Stores the Movable Object if there is any")]
    [SerializeField] private Collider2D movableColliderBelow;
    [SerializeField] private bool footVisualization;


    [Header("Hand Properties------------------------------------------------------------------------------")]
    [SerializeField] private Transform hand;
    [SerializeField] private Transform shoulder;
    [SerializeField] private Vector2 handSize = new Vector2(.5f, 1);
    [SerializeField] private Vector2 shoulderSize = new Vector2(.5f, 1);
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
    [Tooltip("Time offset between the key press and direction set")]
    [SerializeField] private bool canDash;

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
    }

    private void FixedUpdate()
    {

        if (runEnabled) SetHorizontalVelocity();

        GroundCheck();

        ApplyDrag();

        RemoveFloatyness();

        SetDash();
    }

    private void SetValues()
    {
        //Do checks for ground and movable objects
        groundCheckRealtime = Physics2D.OverlapBox(foot.position, footSize, 0, groundMask);

        //For Animations
        thisVelocity = thisBody.velocity;
        thisRotation = transform.rotation.eulerAngles;

        //Set Dash Vector based on user input
        dashVector = new Vector2(inputX, inputY).normalized * dashForce;
        if (dashVector == Vector2.zero)
        {
            //If normal state then apply dash in the forward direction 
            if (currentMovementState == MovementState.SIMPLE)
            {
                if (thisRotation.y == 0) { dashVector = Vector2.right * dashForce; }
                else if (thisRotation.y == 180) { dashVector = Vector2.left * dashForce; }
            }
            else if (currentMovementState == MovementState.GRAB) //If grabbing then apply dash opposite to the forward direction because the forward direction is towards the wall
            {
                if (thisRotation.y == 0) { dashVector = Vector2.left * dashForce; }
                else if (thisRotation.y == 180) { dashVector = Vector2.right * dashForce; }
            }
        }

        //Maintain the jump count
        if (groundCheckRealtime || movableCheckFoot)
        {
            jumpsLeft = numberOfJumps;
        }
    }

    private void GetInput()
    {
        inputX = playerMovementActionMap.General.Movement.ReadValue<float>();
        inputY = playerMovementActionMap.General.VerticalMovement.ReadValue<float>();
    }

    private void SetHorizontalVelocity()
    {
        if (isControllableX == true) { horizontalVelocityToSet = horizontalVelocityToSet + inputX * moveSpeed; }
        else
        {
            if (currentMovementState == MovementState.DASH) { return; }
        }

        thisBody.velocity = new Vector2(horizontalVelocityToSet, thisBody.velocity.y);
    }

    private void Jump()
    {
        if (currentMovementState == MovementState.SIMPLE)
        {
            if (groundCheck == true && jumpsLeft > 0)
            {
                thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce);
                jumpsLeft -= 1;
            }
            else if (movableCheckFoot == true && jumpsLeft > 0)
            {
                thisBody.velocity = new Vector2(thisBody.velocity.x, jumpForce);
                jumpsLeft -= 1;
            }
        }
    }

    private void JumpCancel()
    {
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
        if (currentMovementState == MovementState.SIMPLE)
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
        else if (currentMovementState == MovementState.DASH)
        {
            thisBody.drag = dashDrag;
        }
        else if (currentMovementState == MovementState.GRAB)
        {
            thisBody.velocity = new Vector2(thisBody.velocity.x, thisVelocity.y - thisVelocity.y * 0.5f);
        }
    }

    //Can be called from another script (crystal) to replenish the dash
    public void SetDash(bool value)
    {
        canDash = value;
    }

    public void SetDash()
    {
        if (groundCheckRealtime == true || movableCheckFoot == true)
        {
            canDash = true;
            dashesLeft = numberOfDashes;
        }
    }

    private void Dash()
    {
        if (canDash == false) return;

        if (currentMovementState != MovementState.DASH)
        {
            currentMovementState = MovementState.DASH;

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
        if (currentMovementState == MovementState.DASH) currentMovementState = MovementState.SIMPLE;

        if (EDashed != null) { EDashed(false); }

        //Restore the colliders to original state
        boxCollider.enabled = true;
        circleCollider.enabled = false;

    }

    private void OnDrawGizmos()
    {
        if (footVisualization == true) Gizmos.DrawCube(foot.position, footSize);
        if (handVisualization == true) Gizmos.DrawCube(hand.position, handSize);
        if (handVisualization == true) Gizmos.DrawCube(shoulder.position, shoulderSize);
    }
}

using UnityEngine;
using UnityEngine.UI;

public class DebugMenu : MonoBehaviour
{

    [SerializeField]
    private GameObject debugMenu;

    public delegate void SetDebugValues(bool timeDeltaTime, float horizontalSpeed, float jumpForce, float localGravity);
    public static event SetDebugValues eSetDebugValues;

    //Values in the debug menu
    private bool currentTimeDeltaTime;
    private float currentHorizontalSpeed, currentJumpForce, currentLocalGravity;

    //Fields
    public Toggle timeDeltaTimeToggle;
    public InputField horizontalSpeedInputField, jumpForceInputField, localGravityInputField;

    private void Start()
    {
        //Initially sets the values from the character controller script
        CharacterController.eExposeValues += SetValuesInDebugger;

        //For shitty toggles
        timeDeltaTimeToggle.onValueChanged.AddListener(delegate { SetTimeDeltaTime(timeDeltaTimeToggle); });
    }

    private void OnDisable()
    {
        CharacterController.eExposeValues -= SetValuesInDebugger;
    }

    private void SetValuesInDebugger(bool timeDeltaTime, float horizontalSpeed, float jumpForce, float localGravity)
    {

        //Set the variables
        currentTimeDeltaTime = timeDeltaTime;
        currentHorizontalSpeed = horizontalSpeed;
        currentJumpForce = jumpForce;
        currentLocalGravity = localGravity;

        //Set these values in the ui
        timeDeltaTimeToggle.isOn = currentTimeDeltaTime;
        horizontalSpeedInputField.text = currentHorizontalSpeed.ToString();
        jumpForceInputField.text = currentJumpForce.ToString();
        localGravityInputField.text = currentLocalGravity.ToString();
    }

    public void ToggleDebugMenu()
    {

        if (debugMenu.activeInHierarchy == false)
            debugMenu.SetActive(true);
        else
            debugMenu.SetActive(false);
    }

    public void SetTimeDeltaTime(Toggle toggle)
    {
        currentTimeDeltaTime = toggle.isOn;
    }

    public void SetHorizontalSpeed(InputField field)
    {
        currentHorizontalSpeed = float.Parse(field.text);
    }

    public void SetJumpForce(InputField field)
    {
        currentJumpForce = float.Parse(field.text);
    }

    public void SetLocalGravity(InputField field)
    {
        currentLocalGravity = float.Parse(field.text);
    }

    public void SetValues()
    {

        //Set the values
        if (eSetDebugValues != null)
        {
            eSetDebugValues(currentTimeDeltaTime, currentHorizontalSpeed, currentJumpForce, currentLocalGravity);
        }
    }
}

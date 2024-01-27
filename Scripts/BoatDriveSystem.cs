
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class BoatDriveSystem : UdonSharpBehaviour
{
    /*
    Purpose:
    - Manage inputs
    - Manage steering forces
    - Manage indicators
    - Manage sounds
    */

    Vector2 inputs = Vector2.zero;

    [Header("Behavior parameters")]
    [SerializeField] float thrust = 10000;
    [SerializeField] float maxRudderDeflectionAngle = 20;

    [Header("Linked components")]
    [SerializeField] BoatController linkedBoatController;
    [SerializeField] Transform thruster;
    [SerializeField] AudioSource startupSound;
    [SerializeField] AudioSource runningSound;
    [SerializeField] AudioSource shutdownSound;
    [SerializeField] StationManager driverStation;
    [SerializeField] Indicator wheel;
    [SerializeField] Indicator throttleIndicator;


    //Fixed parameters
    Rigidbody linkedRigidbody;
    bool soundAvailable;
    bool isInVR;

    //Runtime parameters
    public LocalBoatStates localBoatState;
    Vector3 currentThrust;
    float currentHorizontalSteeringAngle = 0;
    bool inputActive = false;
    float startupRamp = 0.5f;
    bool enginePreviouslyActive = false;

    public string[] DebugText
    {
        get
        {
            string[] returnString = new string[]
            {
                $"Debug of {nameof(BoatController)} called {gameObject.name}",
                $"Owner of controller = {Networking.GetOwner(gameObject).displayName}",
                $"Owner of rigidbody = {Networking.GetOwner(linkedRigidbody.gameObject).displayName}",
                $"Drag (Should be zero): {linkedRigidbody.drag}",
                $"Angular drag: {linkedRigidbody.angularDrag}",
                $"Constraints: {linkedRigidbody.constraints}",
                $"IsSleeping: {linkedRigidbody.IsSleeping()}",
                $"IsKinematic: {linkedRigidbody.isKinematic}",
                $"{nameof(inputs)}: {inputs}",
                $"{nameof(localBoatState)}: {localBoatState}",
            };

            return returnString;
        }
    }

    public bool CheckAssignments()
    {
        if (thruster == null) return false;
        if (linkedRigidbody == null) return false;
        if (driverStation == null) return false;
        if (wheel == null) return false;
        if (throttleIndicator == null) return false;
        if (startupSound == null) return false;
        if (runningSound == null) return false;
        if (shutdownSound == null) return false;

        return true;
    }
    
    bool EngineActiveNotImplementedCorrectl
    {
        get
        {
            return inputs.y > -Mathf.Infinity;
        }
        set
        {
            if (value)
            {
                if (!enginePreviouslyActive) StartSound();
                inputs.y = Mathf.Clamp(inputs.y, -1, 1);
            }
            else
            {
                if (enginePreviouslyActive) StopSound();
                inputs.y = -Mathf.Infinity;
            }

            enginePreviouslyActive = value;
        }
    }

    void SetIndicators()
    {
        if (wheel) wheel.InputValue = inputs.x;
        if (throttleIndicator) throttleIndicator.InputValue = inputs.y;
    }

    void StartSound()
    {
        if (!soundAvailable) return;

        startupSound.Play();
        runningSound.Play();
        runningSound.volume = 0;
        startupRamp = 0;
    }

    void UpdateSound()
    {
        if (!soundAvailable) return;

        startupRamp += Mathf.Clamp01(Time.deltaTime / startupSound.clip.length);

        runningSound.pitch = Mathf.Abs(inputs.y) + 1;
        runningSound.volume = (Mathf.Abs(inputs.y) * 0.5f + 0.5f) * startupRamp;
    }

    void StopSound()
    {
        if (!soundAvailable) return;

        runningSound.Stop();
        shutdownSound.Play();
    }

    public void Setup()
    {
        linkedRigidbody = linkedBoatController.LinkedRigidbody;
        if (!linkedRigidbody) Debug.LogWarning("Error: linked boat controller does not seem to have the Rigidbody assigned");

        soundAvailable = (startupSound && startupSound.clip && runningSound && runningSound.clip && shutdownSound && shutdownSound.clip);
    }

    public void TryActivating()
    {

    }

    static Vector2 GetSmoothInputs()
    {
        Vector2 returnValue = Vector2.zero;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            returnValue.y += Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.LeftControl))
        {
            returnValue.y -= Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.Q))
        {
            returnValue.x -= Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.E))
        {
            returnValue.x += Time.deltaTime;
        }

        return returnValue;
    }

    static Vector2 GetSquareInput()
    {
        //Source: https://github.com/Sacchan-VRC/SaccFlightAndVehicles/blob/b04482a48c808d8e13524ce6711a7853e4d7afda/Scripts/SaccAirVehicle/SaccAirVehicle.cs#L1140
        Vector2 inputValue = new Vector2(
            Input.GetAxis("Oculus_GearVR_LThumbstickX"),
            Input.GetAxis("Oculus_GearVR_LThumbstickY"));

        inputValue = Mathf.Clamp01(inputValue.magnitude) * inputValue.normalized; //Magnitude can apparently somehow be larger than 1

        if (Mathf.Abs(inputValue.x) > Mathf.Abs(inputValue.y))
        {
            if (Mathf.Abs(inputValue.x) > 0)
            {
                float temp = inputValue.magnitude / Mathf.Abs(inputValue.x);
                inputValue = temp * inputValue;
            }
        }
        else if (Mathf.Abs(inputValue.y) > 0)
        {
            float temp = inputValue.magnitude / Mathf.Abs(inputValue.y);
            inputValue = temp * inputValue;
        }

        return inputValue;
    }

    void GatherDirectInputs()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            inputs.y = 1;
            inputActive = true;
        }
        else if (Input.GetKeyUp(KeyCode.W))
        {
            inputs.y = 0;
            inputActive = false;
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            inputs.y = -1;
            inputActive = true;
        }
        else if (Input.GetKeyUp(KeyCode.S))
        {
            inputs.y = 0;
            inputActive = false;
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            inputs.x = -1;
            inputActive = true;
        }
        else if (Input.GetKeyUp(KeyCode.A))
        {
            inputs.x = 0;
            inputActive = false;
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            inputs.x = 1;
            inputActive = true;
        }
        else if (Input.GetKeyUp(KeyCode.D))
        {
            inputs.x = 0;
            inputActive = false;
        }

        if (!inputActive)
        {
            Vector2 controllerInput = GetSquareInput();

            if (controllerInput.magnitude < 0.1f)
            {
                //Include Shift Ctrl Q E
                Vector2 smoothInputs = GetSmoothInputs();

                inputs.x = Mathf.Clamp(inputs.x + smoothInputs.x, -1, 1);
                inputs.y = Mathf.Clamp(inputs.y + smoothInputs.y, -1, 1);
            }
            else inputs = controllerInput;
        }
    }

    private void Update()
    {
        switch (localBoatState)
        {
            case LocalBoatStates.Idle:
                break;
            case LocalBoatStates.LocallyActive:
                //Get inputs
                if (!isInVR)
                {
                    GatherDirectInputs();
                }
                else
                {
                    inputs = GetSquareInput();
                }

                currentHorizontalSteeringAngle = -inputs.x * maxRudderDeflectionAngle;

                thruster.transform.localRotation = Quaternion.Euler(0, currentHorizontalSteeringAngle, 0);

                if (EngineActive)
                {
                    SetIndicators();
                    UpdateSound();
                }

                linkedBoatController.SyncInputs(inputs.y);

                break;
            case LocalBoatStates.NetworkControlled:
                if (EngineActive)
                {
                    SetIndicators();
                    UpdateSound();
                }
                break;
            default:
                break;
        }
    }

    void FixedUpdate()
    {
        switch (localBoatState)
        {
            case LocalBoatStates.Idle:
                break;
            case LocalBoatStates.LocallyActive:

                if (thruster.transform.position.y < 0) //ToDo: Implement wave function
                {
                    currentThrust = inputs.y * thrust * thruster.forward;

                    linkedRigidbody.AddForceAtPosition(currentThrust, thruster.position);
                }
                else
                {
                    //ToDo: Modify sound
                }
                break;
            case LocalBoatStates.NetworkControlled:
                break;
            default:
                break;
        }
    }

}

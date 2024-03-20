
using iffnsStuff.iffnsVRCStuff.InteractionController;
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

    [Header("Behavior parameters")]
    [SerializeField] float thrust = 10000;
    [SerializeField] float maxRudderDeflectionAngle = 20;

    [Header("Linked components")]
    BoatController linkedBoatController;
    [SerializeField] Transform thruster;
    [SerializeField] AudioSource startupSound;
    [SerializeField] AudioSource runningSound;
    [SerializeField] AudioSource shutdownSound;
    [SerializeField] RotationInteractor inputHelm;
    [SerializeField] LinearSliderInteractor inputThrottle;

    //Fixed parameters
    Rigidbody linkedRigidbody;
    bool soundAvailable;
    bool isInVR;
    const float engineDisabledValue = -Mathf.Infinity;

    //Runtime parameters
    LocalBoatStates localBoatState;
    Vector3 currentThrust;
    float currentHorizontalSteeringAngle = 0;
    bool inputActive = false;
    float startupRamp = 0.5f;
    bool enginePreviouslyActive = false;
    Vector2 inputs = Vector2.zero;

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
        if (inputHelm == null) return false;
        if (inputThrottle == null) return false;
        if (startupSound == null) return false;
        if (runningSound == null) return false;
        if (shutdownSound == null) return false;

        return true;
    }

    public LocalBoatStates LocalBoatState
    {
        set
        {
            switch (value)
            {
                case LocalBoatStates.IdleAsOwner:
                    if (localBoatState == LocalBoatStates.ActiveAsOwner)
                    {
                        StopSound();
                    }
                    break;
                case LocalBoatStates.ActiveAsOwner:
                    inputs = Vector2.zero;
                    if(localBoatState == LocalBoatStates.IdleAsOwner)
                    {
                        StartSound();
                    }
                    break;
                case LocalBoatStates.NetworkControlled:
                    break;
                default:
                    break;
            }

            localBoatState = value;
        }
    }

    bool EngineActiveNotImplementedCorrectly
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
        if (inputHelm) inputHelm.CurrentAngleDeg = inputs.x * 30f;
        if (inputThrottle) inputThrottle.CurrentValue = inputs.y;
    }

    void StartSound()
    {
        if (!soundAvailable) return;

        startupSound.Play();
        runningSound.Play();
        runningSound.volume = 0;
        startupRamp = 0;
    }

    void UpdateContinousSound()
    {
        if (!soundAvailable) return;

        if(inputs.y == -Mathf.Infinity) return;

        startupRamp += Mathf.Clamp01(Time.deltaTime / startupSound.clip.length);

        runningSound.pitch = Mathf.Abs(inputs.y) + 1;
        runningSound.volume = (Mathf.Abs(inputs.y) * 0.5f + 0.5f) * startupRamp;
    }

    void CheckRemoteSound()
    {
        bool engineNowActive = inputs.y != engineDisabledValue;

        if (engineNowActive != enginePreviouslyActive)
        {
            if (engineNowActive) StartSound();
            else StopSound();

            enginePreviouslyActive = engineNowActive;
        }
    }

    void StopSound()
    {
        if (!soundAvailable) return;

        runningSound.Stop();
        shutdownSound.Play();
    }

    public void Setup(BoatController linkedBoatController)
    {
        this.linkedBoatController = linkedBoatController;

        isInVR = Networking.LocalPlayer.IsUserInVR();

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

    void GetRemoteValues()
    {
        inputs = linkedBoatController.SyncedInputs;
    }

    private void Update()
    {
        switch (localBoatState)
        {
            case LocalBoatStates.IdleAsOwner:
                break;
            case LocalBoatStates.ActiveAsOwner:
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

                SetIndicators();
                UpdateContinousSound();

                linkedBoatController.SyncDriveValues(inputs);

                break;
            case LocalBoatStates.NetworkControlled:
                GetRemoteValues();
                SetIndicators();
                UpdateContinousSound();
                CheckRemoteSound();
                break;
            default:
                break;
        }
    }




    void FixedUpdate()
    {
        switch (localBoatState)
        {
            case LocalBoatStates.IdleAsOwner:
                break;
            case LocalBoatStates.ActiveAsOwner:

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

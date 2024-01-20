
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

    [UdonSynced] Vector2 syncedInputs = Vector2.zero;
    [UdonSynced] bool syncedEngineActive = false;

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
                $"{nameof(syncedInputs)}: {syncedInputs}",
                $"{nameof(syncedPlatformActiveForMovement)}: {syncedPlatformActiveForMovement}",
                $"{nameof(syncedOwnershipLocked)}: {syncedOwnershipLocked}",
                $"{nameof(syncedEngineActive)}: {syncedEngineActive}",
                $"{nameof(localBoatState)}: {localBoatState}",
            };

            return returnString;
        }
    }

    public bool CheckAssignments()
    {
        if (thruster == null) return false;
        if (calculationMesh == null) return false;
        if (linkedRigidbody == null) return false;
        if (LinkedHullCalculator == null) return false;
        if (modelHolder == null) return false;
        if (externalTeleportTarget == null) return false;
        if (driverStation == null) return false;
        if (wheel == null) return false;
        if (throttleIndicator == null) return false;
        if (boatCollider == null) return false;
        if (LinkedPlayerColliderControllerCanBeNull == null) return false;
        if (startupSound == null) return false;
        if (runningSound == null) return false;
        if (shutdownSound == null) return false;

        return true;
    }

    void SetIndicators()
    {
        if (wheel) wheel.InputValue = syncedInputs.x;
        if (throttleIndicator) throttleIndicator.InputValue = syncedInputs.y;
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

        runningSound.pitch = Mathf.Abs(syncedInputs.y) + 1;
        runningSound.volume = (Mathf.Abs(syncedInputs.y) * 0.5f + 0.5f) * startupRamp;
    }

    void StopSound()
    {
        if (!soundAvailable) return;

        runningSound.Stop();
        shutdownSound.Play();
    }

    bool EngineActive
    {
        set
        {
            if (value)
            {
                if (!enginePreviouslyActive) StartSound();
            }
            else
            {
                if (enginePreviouslyActive) StopSound();
            }

            enginePreviouslyActive = value;
        }
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

    private void Update()
    {
        switch (localBoatState)
        {
            case LocalBoatStates.Idle:
                break;
            case LocalBoatStates.LocallyActive:
                if (Time.timeSinceLevelLoad > nextSerializationTime)
                {
                    RequestSerialization();
                }

                //Get inputs
                if (!isInVR)
                {
                    if (Input.GetKeyDown(KeyCode.W))
                    {
                        syncedInputs.y = 1;
                        inputActive = true;
                    }
                    else if (Input.GetKeyUp(KeyCode.W))
                    {
                        syncedInputs.y = 0;
                        inputActive = false;
                    }

                    if (Input.GetKeyDown(KeyCode.S))
                    {
                        syncedInputs.y = -1;
                        inputActive = true;
                    }
                    else if (Input.GetKeyUp(KeyCode.S))
                    {
                        syncedInputs.y = 0;
                        inputActive = false;
                    }

                    if (Input.GetKeyDown(KeyCode.A))
                    {
                        syncedInputs.x = -1;
                        inputActive = true;
                    }
                    else if (Input.GetKeyUp(KeyCode.A))
                    {
                        syncedInputs.x = 0;
                        inputActive = false;
                    }

                    if (Input.GetKeyDown(KeyCode.D))
                    {
                        syncedInputs.x = 1;
                        inputActive = true;
                    }
                    else if (Input.GetKeyUp(KeyCode.D))
                    {
                        syncedInputs.x = 0;
                        inputActive = false;
                    }

                    if (!inputActive)
                    {
                        Vector2 controllerInput = GetSquareInput();

                        if (controllerInput.magnitude < 0.1f)
                        {
                            //Include Shift Ctrl Q E
                            Vector2 smoothInputs = GetSmoothInputs();

                            syncedInputs.x = Mathf.Clamp(syncedInputs.x + smoothInputs.x, -1, 1);
                            syncedInputs.y = Mathf.Clamp(syncedInputs.y + smoothInputs.y, -1, 1);
                        }
                        else syncedInputs = controllerInput;
                    }
                }
                else
                {
                    syncedInputs = GetSquareInput();
                }

                currentHorizontalSteeringAngle = -syncedInputs.x * maxRudderDeflectionAngle;

                thruster.transform.localRotation = Quaternion.Euler(0, currentHorizontalSteeringAngle, 0);

                modelHolder.SetPositionAndRotation(rigidBodyTransform.position, rigidBodyTransform.rotation);

                if (syncedEngineActive)
                {
                    SetIndicators();
                    UpdateSound();
                }
                break;
            case LocalBoatStates.NetworkControlled:
                if (syncedEngineActive)
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
                    currentThrust = syncedInputs.y * thrust * thruster.forward;

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

    public override void OnDeserialization()
    {
        EngineActive = syncedEngineActive;
    }
}

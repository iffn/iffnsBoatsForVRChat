//UdonCompilerStopper
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
    [SerializeField] Transform thruster;
    [SerializeField] AudioSource startupSound;
    [SerializeField] AudioSource runningSound;
    [SerializeField] AudioSource shutdownSound;

    [SerializeField] SingleSyncedFloatInteractor inputHelm;
    [SerializeField] SingleSyncedFloatInteractor inputThrottle;
    [SerializeField] ButtonInteractor respawnButton;
    [SerializeField] ButtonInteractor ownershipButton;
    [SerializeField] ButtonInteractor activationButton;
    [SerializeField] MeshRenderer respawnButtonRenderer;
    [SerializeField] MeshRenderer ownershipButtonRenderer;
    [SerializeField] MeshRenderer activationButtonRenderer;
    [SerializeField] TMPro.TextMeshProUGUI ownershipText;
    [SerializeField] TMPro.TextMeshProUGUI boatStateText;
    [SerializeField] Material enabledButtonMaterial;
    [SerializeField] Material disabledButtonMaterial;
    [SerializeField] Material inactiveButtonMaterial;
    [SerializeField] Material activeRespawnButtonMaterial;
    [SerializeField] Material inactiveRespawnButtonMaterial;
    [SerializeField] TMPro.TextMeshProUGUI debugText;

    //Fixed parameters
    const float smoothTime = 0.068f;
    BoatController linkedBoatController;
    Rigidbody linkedRigidbody;
    bool soundAvailable;
    bool isInVR;
    const float engineDisabledValue = -Mathf.Infinity;

    //Runtime parameters
    LocalBoatStates localBoatState;
    Vector3 currentThrust;
    float currentHorizontalSteeringAngle = 0;
    float startupRamp = 0.5f;
    bool enginePreviouslyActive = false;
    Vector2 inputs = Vector2.zero;
    Vector2 inputSmoothingVelocity;

    public string[] DebugText
    {
        get
        {
            string[] returnString = new string[]
            {
                $"Debug of {nameof(BoatController)} called {gameObject.name}",
                $"{nameof(inputs)}: {inputs}",
                $"{nameof(localBoatState)}: {localBoatState}",
                $"{nameof(localBoatState)}: {localBoatState}",
            };

            return returnString;
        }
    }

    void OutputDebugText()
    {
        if (!linkedBoatController || !debugText) return;

        string outputText = "";

        foreach (string text in linkedBoatController.DebugText)
        {
            outputText += text + "\n";
        }

        outputText += "\n";

        foreach(string text in DebugText)
        {
            outputText += text + "\n";
        }

        debugText.text = outputText;
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

            ButtonState = value;
        }
    }

    LocalBoatStates ButtonState
    {
        set
        {
            bool isOwner;

            switch (value)
            {
                case LocalBoatStates.IdleAsOwner:
                    isOwner = true;
                    activationButtonRenderer.sharedMaterial = disabledButtonMaterial;
                    boatStateText.text = "Boat state: Idle";
                    break;
                case LocalBoatStates.ActiveAsOwner:
                    activationButtonRenderer.sharedMaterial = enabledButtonMaterial;
                    boatStateText.text = "Boat state: Running";
                    isOwner = true;
                    break;
                case LocalBoatStates.NetworkControlled:
                    activationButtonRenderer.sharedMaterial = inactiveButtonMaterial;
                    boatStateText.text = "Boat state: Network controlled";
                    isOwner = false;
                    break;
                default:
                    isOwner = false;
                    Debug.LogWarning($"Error: {nameof(LocalBoatStates)} enum state not defined");
                    break;
            }

            if (isOwner)
            {
                ownershipText.text = $"Current owner:\nYou";
            }
            else
            {
                UpdateRemoteOwnershipButton();
            }

            respawnButtonRenderer.sharedMaterial = isOwner ? activeRespawnButtonMaterial : inactiveRespawnButtonMaterial;
            ownershipButtonRenderer.sharedMaterial = isOwner ? enabledButtonMaterial : disabledButtonMaterial;

            respawnButton.InteractionCollidersEnabled = isOwner;
            activationButton.InteractionCollidersEnabled = isOwner;
            ownershipButton.InteractionCollidersEnabled = !isOwner;
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

    void HandleLocalControls()
    {
        //Get input values
        if (inputHelm) inputs.x = inputHelm.CurrentSyncedControlValue;
        if (inputThrottle) inputs.y = inputThrottle.CurrentSyncedControlValue;

        /*
        if (!isInVR)
        {
            inputs = GatherDirectInputs();
        }
        else
        {
            inputs = GetSquareInput();
        }
        */

        //Apply inputs
        currentHorizontalSteeringAngle = -inputs.x * maxRudderDeflectionAngle;
        thruster.transform.localRotation = Quaternion.Euler(0, currentHorizontalSteeringAngle, 0);

        /*
        if (inputHelm) inputHelm.CurrentControlValue = inputs.x;
        if (inputThrottle) inputThrottle.CurrentControlValue = inputs.y;
        */
    }

    void StartSound()
    {
        if (!soundAvailable) return;

        startupSound.Play();
        runningSound.Play();
        runningSound.volume = 0;
        startupRamp = 0;
    }

    void UpdateContinuousSound()
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

        LocalBoatState = linkedBoatController.LocalBoatState;

        soundAvailable = (startupSound && startupSound.clip && runningSound && runningSound.clip && shutdownSound && shutdownSound.clip);
    }

    static Vector2 GatherSquareControllerInput()
    {
        //Source: https://github.com/Sacchan-VRC/SaccFlightAndVehicles/blob/b04482a48c808d8e13524ce6711a7853e4d7afda/Scripts/SaccAirVehicle/SaccAirVehicle.cs#L1140
        Vector2 newInputs = new Vector2(
            Input.GetAxis("Oculus_GearVR_LThumbstickX"),
            Input.GetAxis("Oculus_GearVR_LThumbstickY"));

        newInputs = Mathf.Clamp01(newInputs.magnitude) * newInputs.normalized; //Magnitude can apparently somehow be larger than 1

        if (Mathf.Abs(newInputs.x) > Mathf.Abs(newInputs.y))
        {
            if (Mathf.Abs(newInputs.x) > 0)
            {
                float temp = newInputs.magnitude / Mathf.Abs(newInputs.x);
                newInputs = temp * newInputs;
            }
        }
        else if (Mathf.Abs(newInputs.y) > 0)
        {
            float temp = newInputs.magnitude / Mathf.Abs(newInputs.y);
            newInputs = temp * newInputs;
        }

        return newInputs;
    }

    static Vector2 GatherDesktopInputs(Vector2 currentInputs)
    {
        Vector2 newInputs = Vector2.zero;

        //Controller input
        Vector2 controllerInput = GatherSquareControllerInput();

        if (controllerInput.magnitude > 0.1f)
        {
            newInputs = controllerInput;
        }
        else
        {
            //Include Shift Ctrl Q E
            Vector2 smoothInputs = GetSmoothInputOffset();

            newInputs.x = Mathf.Clamp(currentInputs.x + smoothInputs.x, -1, 1);
            newInputs.y = Mathf.Clamp(currentInputs.y + smoothInputs.y, -1, 1);
        }

        if(Input.GetKey(KeyCode.W)) newInputs.y = 1;
        if(Input.GetKey(KeyCode.S)) newInputs.y = -1;
        if(Input.GetKey(KeyCode.A)) newInputs.x = -1;
        if(Input.GetKey(KeyCode.D)) newInputs.x = 1;

        return newInputs;
    }

    static Vector2 GetSmoothInputOffset()
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

    public void UpdateRemoteOwnershipButton()
    {
        if (linkedBoatController.ownershipLocked)
        {
            ownershipButton.InteractionCollidersEnabled = false;
            ownershipText.text = $"Current owner:\n{Networking.GetOwner(linkedBoatController.gameObject).displayName}\nOwnership locked";
            ownershipButtonRenderer.sharedMaterial = inactiveButtonMaterial;
        }
        else
        {
            ownershipButton.InteractionCollidersEnabled = true;
            ownershipText.text = $"Current owner:\n{Networking.GetOwner(linkedBoatController.gameObject).displayName}\n[Click to claim]";
            ownershipButtonRenderer.sharedMaterial = disabledButtonMaterial;
        }
    }

    private void Update()
    {
        OutputDebugText();

        switch (localBoatState)
        {
            case LocalBoatStates.IdleAsOwner:
                break;
            case LocalBoatStates.ActiveAsOwner:
                //Get inputs
                HandleLocalControls();
                UpdateContinuousSound();
                break;
            case LocalBoatStates.NetworkControlled:
                UpdateContinuousSound();
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

    public void OwnershipButtonPressed()
    {
        if (ownershipButton.Pressed == false) return;

        linkedBoatController.TryClaimOwnership();
    }

    public void ActivationButtonPressed()
    {
        if (activationButton.Pressed == false) return;

        linkedBoatController.TryToggleBoatActivation();
    }

    public void RespawnButtonPressed()
    {
        if (respawnButton.Pressed == false) return;

        linkedBoatController.TryRespawnBoat();
    }
}

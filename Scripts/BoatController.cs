//#define dragDebug

using NUMovementPlatformSyncMod;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

public enum LocalBoatStates
{
    Idle,
    Driven,
    Towed,
    NetworkControlled
}

public class BoatController : UdonSharpBehaviour
{
    /*
    Purpose:
    - Manage interaction beteen boat physics player collider
    - Manage ownership
    - Manage inputs
    - Manage steering forces
    - Manage indicators
    - Manage sounds
    - Manage visual effects
    */

    [UdonSynced] Vector2 syncedInputs = Vector2.zero;
    [UdonSynced] bool syncedPlatformActiveForMovement = false;
    [UdonSynced] bool syncedOwnershipLocked = false;
    [UdonSynced] bool syncedEngineActive = false;
    
    //Unity assignments
    [Header("Behavior parameters")]
    [SerializeField] float thrust = 10000;
    [SerializeField] float maxRudderDeflectionAngle = 20;
    [SerializeField] Vector3 dragCoefficientsWithDensity = new Vector3(1000, 1000, 50);
    
    [Header("Linked components")]
    [SerializeField] Transform thruster;
    [SerializeField] MeshFilter calculationMesh;
    [SerializeField] Rigidbody linkedRigidbody;
    [SerializeField] HullCalculator linkedHullCalculator;
    [SerializeField] Transform modelHolder;
    [SerializeField] Transform externalTeleportTarget;
    [SerializeField] StationManager driverStation;
    [SerializeField] Indicator wheel;
    [SerializeField] Indicator throttleIndicator;
    [SerializeField] Collider boatCollider;
    [SerializeField] ParticleSystem[] BowEmitters;
    [SerializeField] AudioSource startupSound;
    [SerializeField] AudioSource runningSound;
    [SerializeField] AudioSource shutdownSound;
    [SerializeField] PlayerColliderController linkedPlayerColliderCanBeNull;
    
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

    //Fixed parameters
    Transform rigidBodyTransform;
    VRCPlayerApi localPlayer;
    readonly float timeBetweenSerializations = 1f / 6f;
    readonly int defaultLayer = 0;
    //readonly int pickupLayer = 13;
    readonly int mirrorReflectionLayer = 18;
    VRCObjectSync linkedObjectSync;
    bool soundAvailable;
    bool isInVR;

    //Runtime parameters
    public LocalBoatStates localBoatState = LocalBoatStates.Idle;
    bool enginePreviouslyActive = false;
    Vector3 currentThrust;
    Vector3 velocity;
    float startupRamp = 0.5f;
    float currentHorizontalSteeringAngle = 0;
    float nextSerializationTime;
    bool inputActive = false;

    public Rigidbody LinkedRigidbody
    {
        get
        {
            return linkedRigidbody;
        }
    }

    public VRCObjectSync LinkedObjectSync
    {
        get
        {
            return linkedObjectSync;
        }
    }

    public Transform ExternalTeleportTarget
    {
        get
        {
            return externalTeleportTarget;
        }
    }

    public Transform ModelHolder
    {
        get
        {
            return modelHolder;
        }
    }

    public PlayerColliderController LinkedPlayerColliderControllerCanBeNull
    {
        get
        {
            return linkedPlayerColliderCanBeNull;
        }
    }

    public HullCalculator LinkedHullCalculator
    {
        get
        {
            return linkedHullCalculator;
        }
    }

    //Functions
    bool LocalPlayerHasPriority(VRCPlayerApi remotePlayer)
    {
        return localPlayer.playerId < remotePlayer.playerId;
    }

    public void StopRigidbody()
    {
        linkedRigidbody.velocity = Vector3.zero;
        linkedRigidbody.angularVelocity = Vector3.zero;

        modelHolder.SetPositionAndRotation(rigidBodyTransform.position, rigidBodyTransform.rotation);
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
                if(!enginePreviouslyActive) StartSound();
            }
            else
            {
                if(enginePreviouslyActive) StopSound();
            }

            enginePreviouslyActive = value;
        }
    }

    bool LocalPhysicsActive
    {
        set
        {
            //Colliders
            if (linkedPlayerColliderCanBeNull) 
                linkedPlayerColliderCanBeNull.gameObject.layer = value ? mirrorReflectionLayer : defaultLayer;

            boatCollider.gameObject.SetActive(value);

            //Rigidbody
            if (linkedObjectSync) linkedObjectSync.SetKinematic(value: !value);
            else linkedRigidbody.isKinematic = !value;
            
            linkedRigidbody.useGravity = value;

            if (!value)
            {
                linkedRigidbody.velocity = Vector3.zero;
                linkedRigidbody.angularVelocity = Vector3.zero;
            }

            //Calculation
            linkedHullCalculator.disablePhysics = !value;
        }
    }

    bool PlatformActiveForMovement
    {
        set
        {
            syncedPlatformActiveForMovement = value;
            if (Networking.IsOwner(gameObject)) RequestSerialization();
            if (linkedPlayerColliderCanBeNull) linkedPlayerColliderCanBeNull.shouldSyncPlayer = value;
        }
    }

    public LocalBoatStates LocalBoatState
    {
        get
        {
            return localBoatState;
        }
        set
        {
            switch (value)
            {
                case LocalBoatStates.Idle:
                    switch (localBoatState)
                    {
                        case LocalBoatStates.Idle:
                            //No change
                            break;
                        case LocalBoatStates.Driven:
                            LocalPhysicsActive = false;
                            PlatformActiveForMovement = false;
                            EngineActive = false;
                            break;
                        case LocalBoatStates.Towed:
                            LocalPhysicsActive = false;
                            PlatformActiveForMovement = false;
                            break;
                        case LocalBoatStates.NetworkControlled:
                            PlatformActiveForMovement = false;
                            syncedOwnershipLocked = false;
                            break;
                        default:
                            break;
                    }
                    break;
                case LocalBoatStates.Driven:
                    switch (localBoatState)
                    {
                        case LocalBoatStates.Idle:
                            LocalPhysicsActive = true;
                            PlatformActiveForMovement = true;
                            EngineActive = true;
                            syncedOwnershipLocked = true;
                            break;
                        case LocalBoatStates.Driven:
                            //No change
                            break;
                        case LocalBoatStates.Towed:
                            //Exception: Should not be reachable
                            break;
                        case LocalBoatStates.NetworkControlled:
                            break;
                        default:
                            break;
                    }
                    break;
                case LocalBoatStates.Towed:
                    switch (localBoatState)
                    {
                        case LocalBoatStates.Idle:
                            LocalPhysicsActive = true;
                            PlatformActiveForMovement = true;
                            syncedOwnershipLocked = true;
                            break;
                        case LocalBoatStates.Driven:
                            //Exception: Should not be reachable
                            break;
                        case LocalBoatStates.Towed:
                            //No change
                            break;
                        case LocalBoatStates.NetworkControlled:
                            Networking.SetOwner(localPlayer, gameObject);
                            PlatformActiveForMovement = true;
                            LocalPhysicsActive = true;
                            syncedOwnershipLocked = true;
                            break;
                        default:
                            break;
                    }
                    break;
                case LocalBoatStates.NetworkControlled:
                    switch (localBoatState)
                    {
                        case LocalBoatStates.Idle:
                            //Normal condition: Not fix needed, continue to default procedure
                            break;
                        case LocalBoatStates.Driven:
                            //Handle failed race condition:
                            //ToDo: Kick out
                            PlatformActiveForMovement = syncedPlatformActiveForMovement;
                            LocalPhysicsActive = false;
                            break;
                        case LocalBoatStates.Towed:
                            //Handle failed race condition:
                            //ToDo: Disconnect tow
                            PlatformActiveForMovement = syncedPlatformActiveForMovement;
                            LocalPhysicsActive = false;
                            break;
                        case LocalBoatStates.NetworkControlled:
                            //No change
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }

            localBoatState = value; //Only switch at the end for previous state
        }
    }

    public bool TryDriving()
    {
        //Checks
        switch (localBoatState)
        {
            case LocalBoatStates.Idle:
                //Pass
                break;
            case LocalBoatStates.Driven:
                return false; //Already driving
            case LocalBoatStates.Towed:
                return false;
            case LocalBoatStates.NetworkControlled:
                if (syncedOwnershipLocked) return false;
                break;
            default:
                break;
        }

        //Enable towing
        LocalBoatState = LocalBoatStates.Driven;

        //Return state
        return true;
    }

    public void StopDriving()
    {
        //Checks
        switch (localBoatState)
        {
            case LocalBoatStates.Idle:
                //Already not driving
                return;
            case LocalBoatStates.Driven:
                //pass
                break;
            case LocalBoatStates.Towed:
                return;
            case LocalBoatStates.NetworkControlled:
                return;
            default:
                break;
        }

        LocalBoatState = LocalBoatStates.Idle;
    }

    public bool TryEnableTowing()
    {
        //Checks
        switch (localBoatState)
        {
            case LocalBoatStates.Idle:
                //Pass
                break;
            case LocalBoatStates.Driven:
                return false;
            case LocalBoatStates.Towed:
                return false;
            case LocalBoatStates.NetworkControlled:
                if(syncedOwnershipLocked) return false;
                break;
            default:
                break;
        }

        //Enable towing
        LocalBoatState = LocalBoatStates.Towed;

        //Return state
        return true;
    }

    void SetIndicators()
    {
        if (wheel) wheel.InputValue = syncedInputs.x;
        if (throttleIndicator) throttleIndicator.InputValue = syncedInputs.y;
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


    //Events
    void Start()
    {
        soundAvailable = (startupSound && startupSound.clip && runningSound && runningSound.clip && shutdownSound && shutdownSound.clip);

        localPlayer = Networking.LocalPlayer;
        isInVR = localPlayer.IsUserInVR();

        rigidBodyTransform = linkedRigidbody.transform;

        calculationMesh.transform.parent = rigidBodyTransform;

        linkedHullCalculator.Setup(calculationMesh, calculationMesh.transform, linkedRigidbody);

        linkedObjectSync = rigidBodyTransform.GetComponent<VRCObjectSync>();

        localBoatState = Networking.IsOwner(gameObject) ? LocalBoatStates.Idle : LocalBoatStates.NetworkControlled;

        /*
        dragCoefficientsWithDensity.x = Mathf.Clamp(Mathf.Abs(dragCoefficientsWithDensity.x), 0.0001f, 1000);
        dragCoefficientsWithDensity.y = Mathf.Clamp(Mathf.Abs(dragCoefficientsWithDensity.y), 0.0001f, 1000);
        dragCoefficientsWithDensity.z = Mathf.Clamp(Mathf.Abs(dragCoefficientsWithDensity.z), 0.0001f, 1000);

        if (dragCoefficientsWithDensity.x == 0 ) dragCoefficientsWithDensity = Vector3.one;
        */
    }

    void Update()
    {
        switch (localBoatState)
        {
            case LocalBoatStates.Idle:
                break;
            case LocalBoatStates.Driven:
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
            case LocalBoatStates.Towed:
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

        

        //Bow emitteres;
        /*
        float velocity = linkedRigidbody.velocity.magnitude;
        float rateOverTime = velocity;
        float startSpeed = velocity * 1f;
        foreach(ParticleSystem particleSystem in BowEmitters)
        {
            ParticleSystem.MainModule mainModule = particleSystem.main;
            mainModule.startSpeed = startSpeed;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            //emission.rateOverTime = rateOverTime;
        }
        */
    }

#if dragDebug 
    public Vector3 velocityDebug;
    public Vector3 dragForceDebug;
    public Vector3 dragAreaDebug;
#endif

    private void FixedUpdate()
    {
        modelHolder.SetPositionAndRotation(rigidBodyTransform.position, rigidBodyTransform.rotation);

        switch (localBoatState)
        {
            case LocalBoatStates.Idle:
                break;
            case LocalBoatStates.Driven:

                if (thruster.transform.position.y < 0) //ToDo: Implement wave function
                {
                    currentThrust = syncedInputs.y * thrust * thruster.forward;

                    linkedRigidbody.AddForceAtPosition(currentThrust, thruster.position);
                }
                else
                {
                    //ToDo: Modify sound
                }

                Vector3 localVelocity = rigidBodyTransform.InverseTransformVector(linkedRigidbody.velocity);

                Vector3 dragArea = LinkedHullCalculator.DragAreaBelowWater;

                Vector3 localDragForce = Vector3.zero;
                localDragForce.x = -localVelocity.x * Mathf.Abs(localVelocity.x) * dragArea.x * dragCoefficientsWithDensity.x;
                localDragForce.y = -localVelocity.y * Mathf.Abs(localVelocity.y) * dragArea.y * dragCoefficientsWithDensity.y;
                localDragForce.z = -localVelocity.z * Mathf.Abs(localVelocity.z) * dragArea.z * dragCoefficientsWithDensity.z;

#if dragDebug
                velocityDebug = localVelocity;
                dragAreaDebug = dragArea;
                dragForceDebug = localDragForce;
#endif

                linkedRigidbody.AddForce(rigidBodyTransform.TransformVector(localDragForce));

                velocity = linkedRigidbody.velocity;

                break;
            case LocalBoatStates.Towed:
                break;
            case LocalBoatStates.NetworkControlled:
                break;
            default:
                break;
        }
    }

    public override void OnPreSerialization()
    {
        nextSerializationTime = Time.timeSinceLevelLoad + timeBetweenSerializations;
    }

    public override void OnDeserialization()
    {
        EngineActive = syncedEngineActive;

        PlatformActiveForMovement = syncedPlatformActiveForMovement;
    }

    public void LocalPlayerEntered()
    {
        bool entryWorked = TryDriving();

        if (!entryWorked)
        {
            //ToDo: Kick back out
        }
    }

    public void LocalPlayerExited()
    {
        StopDriving();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        base.OnOwnershipTransferred(player);

        if (player.isLocal)
        {
            switch (localBoatState)
            {
                case LocalBoatStates.Idle:
                    //No change, assumed discard
                    break;
                case LocalBoatStates.Driven:
                    //No change, assumed discard
                    break;
                case LocalBoatStates.Towed:
                    //No change, assumed discard
                    break;
                case LocalBoatStates.NetworkControlled:
                    LocalBoatState = LocalBoatStates.Idle;
                    break;
                default:
                    break;
            }
        }
        else
        {
            switch (localBoatState)
            {
                case LocalBoatStates.Idle:
                    //Normal behavior
                    LocalBoatState = LocalBoatStates.NetworkControlled;
                    break;
                case LocalBoatStates.Driven:
                    //Handle race condition:
                    if (LocalPlayerHasPriority(player))
                    {
                        Networking.SetOwner(localPlayer, gameObject); //Reestablish ownership
                    }
                    else
                    {
                        LocalBoatState = LocalBoatStates.NetworkControlled; //Handle corner case in state
                    }
                    break;
                case LocalBoatStates.Towed:
                    //Handle race condition:
                    if (LocalPlayerHasPriority(player))
                    {
                        Networking.SetOwner(localPlayer, gameObject); //Reestablish ownership
                    }
                    else
                    {
                        LocalBoatState = LocalBoatStates.NetworkControlled; //Handle corner case in state
                    }
                    break;
                case LocalBoatStates.NetworkControlled:
                    //Ignore switch between remote players
                    break;
                default:
                    break;
            }
        }
    }
}

//#define dragDebug

using NUMovementPlatformSyncMod;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

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
    [UdonSynced] bool engineEnabled = false;
    
    //Unity assignments
    [Header("Behavior parameters")]
    [SerializeField] float thrust = 10000;
    [SerializeField] Vector3 localCenterOfGravity;
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
    readonly int pickupLayer = 13;
    readonly int mirrorReflectionLayer = 18;
    VRCObjectSync linkedObjectSync;
    bool soundAvailable;
    bool isInVR;

    //Runtime parameters
    bool active = false;
    bool remotelyActive = false;
    Vector3 currentThrust;
    Vector3 velocity;
    float startupRamp = 0.5f;
    float currentHorizontalSteeringAngle = 0;
    float nextSerializationTime;
    bool inputActive = false;

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

    public bool Active
    {
        private set
        {
            if (value)
            {
                //Networking
                if (!Networking.IsOwner(gameObject)) Networking.SetOwner(localPlayer, gameObject);
                if (!Networking.IsOwner(linkedRigidbody.gameObject)) Networking.SetOwner(localPlayer, linkedRigidbody.gameObject);

                remotelyActive = false;

                //Colliders
                if (linkedPlayerColliderCanBeNull) linkedPlayerColliderCanBeNull.gameObject.layer = mirrorReflectionLayer;
                boatCollider.gameObject.layer = pickupLayer;
                boatCollider.gameObject.SetActive(true);

                //Rigidbody
                if (linkedObjectSync) linkedObjectSync.SetKinematic(false);
                else linkedRigidbody.isKinematic = false;

                //Sound
                if (!active) //When switching from false to true
                {
                    StartSound();
                }
            }
            else
            {
                //Colliders
                if (linkedPlayerColliderCanBeNull) linkedPlayerColliderCanBeNull.gameObject.layer = defaultLayer;
                boatCollider.gameObject.SetActive(false);

                //Rigidbody
                if (linkedObjectSync) linkedObjectSync.SetKinematic(true);
                else linkedRigidbody.isKinematic = true;

                linkedRigidbody.velocity = Vector3.zero;
                linkedRigidbody.angularVelocity = Vector3.zero;

                if (active) //When going from true to false
                {
                    StopSound();
                }
            }

            engineEnabled = value;
            RequestSerialization();
            linkedHullCalculator.disablePhysics = !value;
            linkedRigidbody.useGravity = value;
            active = value;
            SetIndicators();
            if(linkedPlayerColliderCanBeNull) linkedPlayerColliderCanBeNull.shouldSyncPlayer = value;
        }
        get
        {
            return active;
        }
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
                $"{nameof(active)}: {active}",
                $"{nameof(remotelyActive)}: {remotelyActive}",
            };

            return returnString;
        }
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

        linkedRigidbody.centerOfMass = localCenterOfGravity;
        Active = false;

        /*
        dragCoefficientsWithDensity.x = Mathf.Clamp(Mathf.Abs(dragCoefficientsWithDensity.x), 0.0001f, 1000);
        dragCoefficientsWithDensity.y = Mathf.Clamp(Mathf.Abs(dragCoefficientsWithDensity.y), 0.0001f, 1000);
        dragCoefficientsWithDensity.z = Mathf.Clamp(Mathf.Abs(dragCoefficientsWithDensity.z), 0.0001f, 1000);

        if (dragCoefficientsWithDensity.x == 0 ) dragCoefficientsWithDensity = Vector3.one;
        */
    }

    void Update()
    {
        if (active)
        {
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
        }

        if(active || remotelyActive)
        {
            SetIndicators();

            //Sound
            UpdateSound();
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
        if (Input.GetKey(KeyCode.KeypadPlus))
        {
            linkedRigidbody.AddTorque(Vector3.up * 50000);
        }

        modelHolder.SetPositionAndRotation(rigidBodyTransform.position, rigidBodyTransform.rotation);

        if (active)
        {
            if(thruster.transform.position.y > 0) //ToDo: Implement wave function
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
        }
    }

    public override void OnPreSerialization()
    {
        nextSerializationTime = Time.timeSinceLevelLoad + timeBetweenSerializations;
    }

    public override void OnDeserialization()
    {
        if (!engineEnabled && remotelyActive)
        {
            StopSound();
        }
        
        if (engineEnabled && !remotelyActive)
        {
            StartSound();
        }

        remotelyActive = engineEnabled;

        if (linkedPlayerColliderCanBeNull) linkedPlayerColliderCanBeNull.shouldSyncPlayer = remotelyActive;
    }

    public void LocalPlayerEntered()
    {
        Active = true; //Also claims ownership
    }

    public void LocalPlayerExited()
    {
        Active = false;
    }
}

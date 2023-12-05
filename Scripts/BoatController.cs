
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

    [UdonSynced] float syncedThrottleInput = 0;
    [UdonSynced] float steeringInput = 0;
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

    //Runtime parameters
    bool active = false;
    bool remotelyActive = false;
    Vector3 currentThrust;
    Vector3 currentDragForce;
    Vector3 velocity;
    float startupRamp = 0.5f;
    float currentHorizontalSteeringAngle = 0;
    float nextSerializationTime;

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

        runningSound.pitch = Mathf.Abs(syncedThrottleInput) + 1;
        runningSound.volume = (Mathf.Abs(syncedThrottleInput) * 0.5f + 0.5f) * startupRamp;
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
        if (wheel) wheel.InputValue = steeringInput;
        if (throttleIndicator) throttleIndicator.InputValue = syncedThrottleInput;
    }

    void GetDesktopInputs()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            syncedThrottleInput = Mathf.Clamp(syncedThrottleInput += Time.deltaTime, -1, 1);
        }

        if (Input.GetKey(KeyCode.LeftControl))
        {
            syncedThrottleInput = Mathf.Clamp(syncedThrottleInput -= Time.deltaTime, -1, 1);
        }

        if (Input.GetKey(KeyCode.Q))
        {
            steeringInput = Mathf.Clamp(steeringInput -= Time.deltaTime, -1, 1);
        }

        if (Input.GetKey(KeyCode.E))
        {
            steeringInput = Mathf.Clamp(steeringInput += Time.deltaTime, -1, 1);
        }
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
                $"{nameof(steeringInput)}: {steeringInput}",
                $"{nameof(syncedThrottleInput)}: {syncedThrottleInput}",
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

        rigidBodyTransform = linkedRigidbody.transform;

        calculationMesh.transform.parent = rigidBodyTransform;

        linkedHullCalculator.Setup(calculationMesh, calculationMesh.transform, linkedRigidbody);

        linkedObjectSync = rigidBodyTransform.GetComponent<VRCObjectSync>();

        linkedRigidbody.centerOfMass = localCenterOfGravity;
        Active = false;

        dragCoefficientsWithDensity.x = Mathf.Clamp(Mathf.Abs(dragCoefficientsWithDensity.x), 0.0001f, 1000);
        dragCoefficientsWithDensity.y = Mathf.Clamp(Mathf.Abs(dragCoefficientsWithDensity.y), 0.0001f, 1000);
        dragCoefficientsWithDensity.z = Mathf.Clamp(Mathf.Abs(dragCoefficientsWithDensity.z), 0.0001f, 1000);

        if (dragCoefficientsWithDensity.x == 0 ) dragCoefficientsWithDensity = Vector3.one;
    }

    // Update is called once per frame
    void Update()
    {
        if (active)
        {
            if (Time.timeSinceLevelLoad > nextSerializationTime)
            {
                RequestSerialization();
            }

            //Get inputs
            if (!localPlayer.IsUserInVR()) GetDesktopInputs();

            float target = -steeringInput * maxRudderDeflectionAngle;

            //currentHorizontalSteeringAngle = Mathf.MoveTowards(currentHorizontalSteeringAngle, target, horizontalSteeringSpeed * Time.deltaTime);
            currentHorizontalSteeringAngle = target;

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

    public float debug;

    public Vector3 velocityDebug;
    public Vector3 dragForceDebug;
    public Vector3 dragAreaDebug;

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.KeypadPlus))
        {
            linkedRigidbody.AddTorque(Vector3.up * 50000);
        }

        modelHolder.SetPositionAndRotation(rigidBodyTransform.position, rigidBodyTransform.rotation);

        if (active)
        {
            currentThrust = syncedThrottleInput * thrust * thruster.forward;

            linkedRigidbody.AddForceAtPosition(currentThrust, thruster.position);

            Vector3 localVelocity = rigidBodyTransform.InverseTransformVector(linkedRigidbody.velocity);

            Vector3 dragArea = LinkedHullCalculator.DragAreaBelowWater;

            Vector3 localDragForce = Vector3.zero;
            localDragForce.x = -localVelocity.x * Mathf.Abs(localVelocity.x) * dragArea.x * dragCoefficientsWithDensity.x;
            localDragForce.y = -localVelocity.y * Mathf.Abs(localVelocity.y) * dragArea.y * dragCoefficientsWithDensity.y;
            localDragForce.z = -localVelocity.z * Mathf.Abs(localVelocity.z) * dragArea.z * dragCoefficientsWithDensity.z;

            velocityDebug = localVelocity;
            dragAreaDebug = LinkedHullCalculator.DragAreaBelowWater;
            dragForceDebug = localDragForce;

            linkedRigidbody.AddForce(rigidBodyTransform.TransformVector(localDragForce));

            currentDragForce = localDragForce;

            /*
            Vector3 localVelocityNormalized = rigidBodyTransform.InverseTransformDirection(linkedRigidbody.velocity).normalized;

            Vector3 drag = new Vector3(
                Mathf.Abs(localVelocityNormalized.x * dragCoefficients.x),
                Mathf.Abs(localVelocityNormalized.y * dragCoefficients.y),
                Mathf.Abs(localVelocityNormalized.z * dragCoefficients.z));

            linkedRigidbody.drag = drag.magnitude;

            */

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

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args)
    {
        if(!active) return;

        steeringInput = value;
    }

    public override void InputMoveVertical(float value, UdonInputEventArgs args)
    {
        if(!active) return;

        syncedThrottleInput = value;
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

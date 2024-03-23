﻿//#define dragDebug

using NUMovementPlatformSyncMod;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

public enum LocalBoatStates
{
    IdleAsOwner,
    ActiveAsOwner,
    NetworkControlled
}

public class BoatController : UdonSharpBehaviour
{
    /*
    Purpose:
    - Manage ownership
    - Manage interaction beteen boat physics player collider
    - Collider states
    - Platform movement
    - Manage visual effects
    */
    
    [UdonSynced] bool syncedPlatformActiveForMovement = false;
    [UdonSynced] bool syncedOwnershipLocked = false;
    [UdonSynced] Vector2 syncedInputs;
    
    //Unity assignments
    [Header("Behavior parameters")]
    [SerializeField] Vector3 dragCoefficientsWithDensity = new Vector3(1000, 1000, 50);

    [Header("Boat specific components")]
    [SerializeField] MeshFilter calculationMesh;
    [SerializeField] Transform modelHolder;
    [SerializeField] Transform externalTeleportTarget;
    [SerializeField] Collider boatCollider;
    [SerializeField] ParticleSystem[] BowEmitters;
    [SerializeField] PlayerColliderController linkedPlayerColliderCanBeNull;

    [Header("Consistent components")]
    [SerializeField] BoatDriveSystem linkedDriveSystem;
    [SerializeField] Rigidbody linkedRigidbody;
    [SerializeField] HullCalculator linkedHullCalculator;

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
                $"{nameof(syncedPlatformActiveForMovement)}: {syncedPlatformActiveForMovement}",
                $"{nameof(syncedOwnershipLocked)}: {syncedOwnershipLocked}",
                $"{nameof(localBoatState)}: {localBoatState}",
            };

            return returnString;
        }
    }

    public bool CheckAssignments()
    {
        if (calculationMesh == null) return false;
        if (linkedRigidbody == null) return false;
        if (LinkedHullCalculator == null) return false;
        if (modelHolder == null) return false;
        if (externalTeleportTarget == null) return false;
        if (boatCollider == null) return false;
        if (LinkedPlayerColliderControllerCanBeNull == null) return false;

        return true;
    }

    //Fixed parameters
    Transform rigidBodyTransform;
    VRCPlayerApi localPlayer;
    VRCObjectSync linkedObjectSync;
    readonly float timeBetweenSerializations = 1f / 6f;
    public const int defaultLayer = 0;
    public const int pickupLayer = 13;
    public const int mirrorReflectionLayer = 18;
    bool isInVR;

    Vector3 worldRespawnPosition;
    Quaternion worldRespawnRotation;

    //Runtime parameters
    float nextSerializationTime;

    LocalBoatStates localBoatState = LocalBoatStates.IdleAsOwner;
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
                case LocalBoatStates.IdleAsOwner:
                    switch (localBoatState)
                    {
                        case LocalBoatStates.IdleAsOwner:
                            //No change
                            break;
                        case LocalBoatStates.ActiveAsOwner:
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
                case LocalBoatStates.ActiveAsOwner:
                    switch (localBoatState)
                    {
                        case LocalBoatStates.IdleAsOwner:
                            LocalPhysicsActive = true;
                            PlatformActiveForMovement = true;
                            syncedOwnershipLocked = true;
                            RequestSerialization();
                            break;
                        case LocalBoatStates.ActiveAsOwner:
                            //No change
                            break;
                        case LocalBoatStates.NetworkControlled:
                            if (syncedOwnershipLocked)
                            {
                                value = LocalBoatStates.NetworkControlled;
                                break;
                            }
                            Networking.SetOwner(localPlayer, gameObject);
                            LocalPhysicsActive = true;
                            PlatformActiveForMovement = true;
                            syncedOwnershipLocked = true;
                            RequestSerialization();
                            break;
                        default:
                            break;
                    }
                    break;
                /*
                case LocalBoatStates.Towed:
                    switch (localBoatState)
                    {
                        case LocalBoatStates.Idle:
                            LocalPhysicsActive = true;
                            PlatformActiveForMovement = true;
                            syncedOwnershipLocked = true;
                            break;
                        case LocalBoatStates.LocallyActive:
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
                */
                case LocalBoatStates.NetworkControlled:
                    switch (localBoatState)
                    {
                        case LocalBoatStates.IdleAsOwner:
                            //Normal condition: Not fix needed, continue to default procedure
                            break;
                        case LocalBoatStates.ActiveAsOwner:
                            //Handle failed race condition:
                            //ToDo: Kick out
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
            linkedDriveSystem.LocalBoatState = localBoatState;
        }
    }

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

    public Vector2 SyncedInputs
    {
        get
        {
            return syncedInputs;
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

    bool PlatformActiveForMovement
    {
        set
        {
            syncedPlatformActiveForMovement = value;
            if (Networking.IsOwner(gameObject)) RequestSerialization();
            if (linkedPlayerColliderCanBeNull) linkedPlayerColliderCanBeNull.shouldSyncPlayer = value;
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
            linkedHullCalculator.physicsActive = value;
        }
    }

    //Internal functions
    bool LocalPlayerHasPriority(VRCPlayerApi remotePlayer)
    {
        return localPlayer.playerId < remotePlayer.playerId;
    }

#if dragDebug 
    public Vector3 velocityDebug;
    public Vector3 dragForceDebug;
    public Vector3 dragAreaDebug;
#endif
    
    void CalculateAndApplyDrag() //ToDo: Move to hull calculator
    {
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
    }

    //Public functions
    public void StopRigidbody()
    {
        linkedRigidbody.velocity = Vector3.zero;
        linkedRigidbody.angularVelocity = Vector3.zero;

        modelHolder.SetPositionAndRotation(rigidBodyTransform.position, rigidBodyTransform.rotation);
    }

    public void RespawnBoatAttempt()
    {
        if (localBoatState == LocalBoatStates.NetworkControlled) return;

        linkedRigidbody.transform.SetPositionAndRotation(worldRespawnPosition, worldRespawnRotation);

        StopRigidbody();
    }

    public void SyncDriveValues(Vector2 inputs)
    {
        this.syncedInputs = inputs;
    }

    public void LocalPlayerEntered()
    {
        bool entryWorked = IsOrTrySettingLocallyActive();

        if (!entryWorked)
        {
            //ToDo: Kick back out
        }
    }

    public void LocalPlayerExited()
    {
        SetLocalInactive();
    }

    public bool IsOrTrySettingLocallyActive()
    {
        //Checks
        switch (localBoatState)
        {
            case LocalBoatStates.IdleAsOwner:
                //Pass
                break;
            case LocalBoatStates.ActiveAsOwner:
                return true; //Already active
            case LocalBoatStates.NetworkControlled:
                if (syncedOwnershipLocked) return false;
                //Pass
                break;
            default:
                break;
        }

        //Enable towing
        LocalBoatState = LocalBoatStates.ActiveAsOwner;

        //Return state
        return true;
    }

    public void SetLocalInactive()
    {
        //Checks
        switch (localBoatState)
        {
            case LocalBoatStates.IdleAsOwner:
                //Ignore since already not driving
                return;
            case LocalBoatStates.ActiveAsOwner:
                //pass
                break;
            case LocalBoatStates.NetworkControlled:
                //Ignore since remotely controlled
                return;
            default:
                break;
        }

        LocalBoatState = LocalBoatStates.IdleAsOwner;
    }

    //Unity functions
    void Start()
    {
        localPlayer = Networking.LocalPlayer;
        isInVR = localPlayer.IsUserInVR();

        rigidBodyTransform = linkedRigidbody.transform;

        calculationMesh.transform.parent = rigidBodyTransform;

        linkedHullCalculator.Setup(calculationMesh, calculationMesh.transform, linkedRigidbody);

        linkedObjectSync = rigidBodyTransform.GetComponent<VRCObjectSync>();

        LocalBoatState = Networking.IsOwner(gameObject) ? LocalBoatStates.IdleAsOwner : LocalBoatStates.NetworkControlled;

        linkedDriveSystem.Setup(this);

        worldRespawnPosition = linkedRigidbody.transform.position;
        worldRespawnRotation = linkedRigidbody.transform.rotation;

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
            case LocalBoatStates.IdleAsOwner:
                break;
            case LocalBoatStates.ActiveAsOwner:
                if (Time.timeSinceLevelLoad > nextSerializationTime)
                {
                    RequestSerialization();
                }
                break;
            case LocalBoatStates.NetworkControlled:
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

    private void FixedUpdate()
    {
        modelHolder.SetPositionAndRotation(rigidBodyTransform.position, rigidBodyTransform.rotation);

        switch (localBoatState)
        {
            case LocalBoatStates.IdleAsOwner:
                break;
            case LocalBoatStates.ActiveAsOwner:
                CalculateAndApplyDrag();
                break;
            case LocalBoatStates.NetworkControlled:
                break;
            default:
                break;
        }
    }

    //VRChat functions
    public override void OnPreSerialization()
    {
        nextSerializationTime = Time.timeSinceLevelLoad + timeBetweenSerializations;
    }

    public override void OnDeserialization()
    {
        PlatformActiveForMovement = syncedPlatformActiveForMovement;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        base.OnOwnershipTransferred(player);

        if (player.isLocal)
        {
            switch (localBoatState)
            {
                case LocalBoatStates.IdleAsOwner:
                    //No change, assumed discard
                    break;
                case LocalBoatStates.ActiveAsOwner:
                    //No change, assumed discard
                    break;
                case LocalBoatStates.NetworkControlled:
                    //Other player disconnected
                    LocalBoatState = LocalBoatStates.IdleAsOwner;
                    break;
                default:
                    break;
            }
        }
        else
        {
            switch (localBoatState)
            {
                case LocalBoatStates.IdleAsOwner:
                    //Normal behavior
                    LocalBoatState = LocalBoatStates.NetworkControlled;
                    break;
                case LocalBoatStates.ActiveAsOwner:
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

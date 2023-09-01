
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

public class BoatController : UdonSharpBehaviour
{
    [SerializeField] StationManager driverStation;

    [SerializeField] MeshFilter calculationMesh;
    [SerializeField] HullCalculator linkedHullCalculator;
    [SerializeField] Rigidbody linkedRigidbody;
    [SerializeField] Transform thruster;
    [SerializeField] Transform modelHolder;
    [SerializeField] Vector3 localCenterOfGravity;

    public HullCalculator LinkedHullCalculator
    {
        get
        {
            return linkedHullCalculator;
        }
    }

    bool active = false;

    float originalLinearDrag = 0;
    float originalAngularDrag = 0;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log($"Vertices count = {calculationMesh.mesh.vertexCount}");

        calculationMesh.transform.parent = transform;

        Debug.Log(calculationMesh.transform.parent.name);

        linkedHullCalculator.Setup(calculationMesh, calculationMesh.transform, linkedRigidbody);

        originalLinearDrag = linkedRigidbody.drag;
        originalAngularDrag = linkedRigidbody.angularDrag;

        linkedRigidbody.isKinematic = false;
        linkedRigidbody.centerOfMass = localCenterOfGravity;
        Active = false;
    }

    public void StopRigidbody()
    {
        linkedRigidbody.velocity = Vector3.zero;
        linkedRigidbody.angularVelocity = Vector3.zero;

        modelHolder.SetPositionAndRotation(transform.position, transform.rotation);
    }

    bool Active
    {
        set
        {
            active = value;

            if (value)
            {
                modelHolder.SetPositionAndRotation(transform.position, transform.rotation);

                transform.parent = modelHolder.transform.parent; //Set parent to parent of model holder if active

                linkedRigidbody.drag = originalLinearDrag;
                linkedRigidbody.angularDrag = originalAngularDrag;

                Networking.SetOwner(Networking.LocalPlayer, modelHolder.gameObject);
            }
            else
            {
                modelHolder.SetPositionAndRotation(transform.position, transform.rotation);

                transform.parent = modelHolder.transform; //Set parent to synced model holder position if not active

                linkedRigidbody.velocity = Vector3.zero;
                linkedRigidbody.angularVelocity = Vector3.zero;

                linkedRigidbody.drag = 1000000;
                linkedRigidbody.angularDrag = 1000000;
                SendCustomEventDelayedFrames(nameof(StopRigidbody), 1, VRC.Udon.Common.Enums.EventTiming.LateUpdate);
                SendCustomEventDelayedFrames(nameof(StopRigidbody), 2, VRC.Udon.Common.Enums.EventTiming.LateUpdate);
            }

            linkedHullCalculator.disablePhysics = !value;
            linkedRigidbody.useGravity = value;
        }
    }

    public float calculationTimeMs = 0;
    public float applyTimeMs = 0;

    // Update is called once per frame
    void Update()
    {
        if (active)
        {
            float target = -steeringInput * maxDeflectionAngle;

            currentHorizontalSteeringAngle = Mathf.MoveTowards(currentHorizontalSteeringAngle, target, horizontalSteeringSpeed * Time.deltaTime);

            thruster.transform.localRotation = Quaternion.Euler(0, currentHorizontalSteeringAngle, 0);

            modelHolder.SetPositionAndRotation(transform.position, transform.rotation);
        }
    }

    private void FixedUpdate()
    {
        if (active)
        {
            linkedRigidbody.AddForceAtPosition(thruster.forward * throttleInput * force, thruster.position);
        }
    }

    float throttleInput = 0;
    float steeringInput = 0;

    public float force = 10000;
    public float velocity = 0;

    public float horizontalSteeringSpeed = 30;

    public float currentHorizontalSteeringAngle = 0;

    public float maxDeflectionAngle = 20;

    public int counter = 2;

    public int iteration = 2;

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args)
    {
        if(!active) return;

        steeringInput = value;
    }

    public override void InputMoveVertical(float value, UdonInputEventArgs args)
    {
        if(!active) return;

        throttleInput = value;
    }

    public void LocalPlayerEntered()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);

        Active = true;
    }

    public void LocalPlayerExited()
    {
        Active = false;
    }
}

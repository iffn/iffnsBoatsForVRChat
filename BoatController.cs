
using Newtonsoft.Json.Linq;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

public class BoatController : UdonSharpBehaviour
{
    [SerializeField] StationManager DriverStation;

    [SerializeField] MeshFilter calculationMesh;
    [SerializeField] HullCalculator linkedHullCalculator;
    [SerializeField] Rigidbody linkedRigidbody;
    [SerializeField] Transform thruster;
    [SerializeField] Transform modelHolder;
    
    bool active = false;

    public HullCalculator LinkedHullCalculator
    {
        get
        {
            return linkedHullCalculator;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log($"Vertices count = {calculationMesh.mesh.vertexCount}");

        linkedHullCalculator.Setup(calculationMesh, calculationMesh.transform, linkedRigidbody);
    }

    public float calculationTimeMs = 0;
    public float applyTimeMs = 0;

    // Update is called once per frame
    void Update()
    {
        modelHolder.SetPositionAndRotation(transform.position, transform.rotation);

        if (active)
        {
            float target = -steeringInput * maxDeflectionAngle;

            currentHorizontalSteeringAngle = Mathf.MoveTowards(currentHorizontalSteeringAngle, target, horizontalSteeringSpeed * Time.deltaTime);

            thruster.transform.localRotation = Quaternion.Euler(0, currentHorizontalSteeringAngle, 0);
        }

        /*
        AboveWaterMesh.mesh.triangles = new int[0];
        AboveWaterMesh.mesh.vertices = generator.calculationVerticesGlobal;
        AboveWaterMesh.mesh.triangles = generator.aboveWaterCorners;
        AboveWaterMesh.mesh.RecalculateNormals();
        AboveWaterMesh.mesh.RecalculateBounds();

        UnderwaterMesh.mesh.triangles = new int[0];
        UnderwaterMesh.mesh.vertices = generator.calculationVerticesGlobal;
        UnderwaterMesh.mesh.triangles = generator.belowWaterCorners;
        UnderwaterMesh.mesh.RecalculateNormals();
        UnderwaterMesh.mesh.RecalculateBounds();
        */
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

    public float horizontalSteeringSpeed = 1;

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
        
        active = true;

        linkedHullCalculator.disablePhysics = false;
        linkedRigidbody.useGravity = true;
    }

    public void LocalPlayerExited()
    {
        active = false;

        linkedHullCalculator.disablePhysics = true;
        linkedRigidbody.useGravity = false;
        linkedRigidbody.velocity = Vector3.zero;
        linkedRigidbody.angularVelocity = Vector3.zero;
    }
}

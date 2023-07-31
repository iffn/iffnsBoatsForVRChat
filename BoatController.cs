
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class BoatController : UdonSharpBehaviour
{
    [SerializeField] StationManager DriverStation;

    [SerializeField] MeshFilter BaseMesh;
    [SerializeField] MeshFilter AboveWaterMesh;
    [SerializeField] MeshFilter UnderwaterMesh;
    [SerializeField] HullCalculator generator;
    [SerializeField] Rigidbody LinkedRigidbody;
    [SerializeField] Transform Thruster;
    
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log($"Vertices count = {BaseMesh.mesh.vertexCount}");

        UnderwaterMesh.mesh = new Mesh();
        AboveWaterMesh.mesh = new Mesh();

        generator.Setup(BaseMesh.mesh, BaseMesh.transform, LinkedRigidbody);
    }

    public float calculationTimeMs = 0;
    public float applyTimeMs = 0;

    // Update is called once per frame
    void Update()
    {
        if (DriverStation.inStation)
        {
            float target = 0;

            if (Input.GetKey(KeyCode.A))
            {
                target = maxDeflectionAngle;
            }
            if (Input.GetKey(KeyCode.D))
            {
                target = -maxDeflectionAngle;

            }

            currentHorizontalSteeringAngle = Mathf.MoveTowards(currentHorizontalSteeringAngle, target, horizontalSteeringSpeed * Time.deltaTime);

            Thruster.transform.localRotation = Quaternion.Euler(0, currentHorizontalSteeringAngle, 0);
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

    public float force = 10000;
    public float velocity = 0;

    public float horizontalSteeringSpeed = 1;

    public float currentHorizontalSteeringAngle = 0;

    public float maxDeflectionAngle = 20;

    public int counter = 2;

    public int iteration = 2;

    private void FixedUpdate()
    {
        if (DriverStation.inStation)
        {
            if (Input.GetKey(KeyCode.W))
            {
                LinkedRigidbody.AddForceAtPosition(Thruster.forward * force, Thruster.position);
            }
        }

        velocity = LinkedRigidbody.velocity.magnitude;
    }
}

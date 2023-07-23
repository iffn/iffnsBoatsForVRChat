
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MeshController : UdonSharpBehaviour
{
    [SerializeField] MeshFilter BaseMesh;
    [SerializeField] MeshFilter AboveWaterMesh;
    [SerializeField] MeshFilter UnderwaterMesh;
    [SerializeField] CalculationMeshGenerator generator;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log($"Vertices count = {BaseMesh.mesh.vertexCount}");

        UnderwaterMesh.mesh = new Mesh();
        AboveWaterMesh.mesh = new Mesh();

        generator.Setup(BaseMesh.mesh, BaseMesh.transform);
    }

    // Update is called once per frame
    void Update()
    {


        generator.GenerateCalculationmeshes();

        AboveWaterMesh.mesh.triangles = new int[0];
        AboveWaterMesh.mesh.vertices = generator.calculationVerticesGlobal;
        AboveWaterMesh.mesh.triangles = generator.aboveWaterTriangles;
        AboveWaterMesh.mesh.RecalculateNormals();
        AboveWaterMesh.transform.gameObject.SetActive(true);

        UnderwaterMesh.mesh.triangles = new int[0];
        UnderwaterMesh.mesh.vertices = generator.calculationVerticesGlobal;
        UnderwaterMesh.mesh.triangles = generator.belowWaterTriangles;
        UnderwaterMesh.mesh.RecalculateNormals();
        UnderwaterMesh.transform.gameObject.SetActive(true);
    }
}

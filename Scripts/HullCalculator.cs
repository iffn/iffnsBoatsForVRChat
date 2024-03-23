//#define applyForces
//#define sumUpForcesWhileDriving
//#define complexCalculation
//#define debugVelocity


using System;
using UdonSharp;
using UnityEngine;

public class HullCalculator : UdonSharpBehaviour
{
    // Inspired by:
    // https://www.habrador.com/tutorials/unity-boat-tutorial
    // https://www.gamedeveloper.com/programming/water-interaction-model-for-boats-in-video-games

    //Original hull data
    Mesh hullMesh;
    Transform hullTransform;
    Vector3[] hullVerticesLocal;
    float[] hullTriangleArea = new float[0];
    Vector3[] hullTriangleCenters = new Vector3[0];
    Vector3[] hullTriangleNormals = new Vector3[0];

    //Required data
    public Vector3[] calculationVerticesGlobal = new Vector3[0];
    public int[] belowWaterCorners = new int[0];
    public int[] aboveWaterCorners = new int[0];
    public int aboveWaterCornerCounter = 0;
    public int belowWaterCornerCounter = 0;
    public float[] belowWaterTriangleArea = new float[0];
    public Vector3[] belowWaterTriangleCenters = new Vector3[0];
    public Vector3[] belowWaterTriangleNormals = new Vector3[0];
    public Vector3[] buoyancyForces = new Vector3[0];
    public Vector3[] frictionDragForces = new Vector3[0];
    public Vector3[] pressureForces = new Vector3[0];

    public Vector3 totalPressureForce = Vector3.zero;
    public Vector3 totalPressureTorque = Vector3.zero;
    public Vector3 totalFrictionDragForce = Vector3.zero;
    public Vector3 totalFrictionDragTorque = Vector3.zero;
    public Vector3 totalBuoyancyForce = Vector3.zero;
    public Vector3 totalBuoyancyTorque = Vector3.zero;

    int[] oneAboveTheWaterTriangles = new int[0];
    int[] twoAboveTheWaterTriangles = new int[0];

    public Vector3 DragAreaBelowWater {  get; private set; }

    public float timeMs;

    public bool physicsActive = false;

    //Physics
    float waterDensity = 1000f;
    //float waterKinematicViscosity = 1000034;
    //float waterDynamicViscosity = 1001.6f;

    float gravity = 9.8f;

    public float[] times = new float[20];

    float factor1over12 = 1f / 12f;
    
    public string state = "";

    Rigidbody linkedRigidbody;

    public float pressureDragCoefficientLinear = 1f;
    public float pressureDragCoefficientQuadratic = 1f;
    public float suctionDragCoefficientLinear = 1f;
    public float suctionDragCoefficientQuadratic = 1f;
    public float pressureFalloffPower = 1f;
    public float suctionFalloffPower = 1f;

    public float frictionForceFactor = 1;
    public float pressureForceFactor = 1;

    public float anglePressureDragForceVelocity;

    void Start()
    {
        //Use setup instead
    }

    public void Setup(MeshFilter hullMeshFilter, Transform hullTransform, Rigidbody linkedRigidbody)
    {
        hullMesh = hullMeshFilter.mesh;
        this.hullTransform = hullTransform;

        hullVerticesLocal = hullMesh.vertices;

        calculationVerticesGlobal = new Vector3[hullVerticesLocal.Length + hullMesh.triangles.Length / 3 * 2];

        //Hull triangles
        int[] hullCorners = hullMesh.triangles;

        hullTriangleArea = new float[hullCorners.Length / 3];
        hullTriangleCenters = new Vector3[hullCorners.Length / 3];
        hullTriangleNormals = new Vector3[hullCorners.Length / 3];
        belowWaterTriangleArea = new float[hullCorners.Length]; //Potentially 3 triangles for each triangle

        int hullMeshTriangleCount = hullMesh.triangles.Length;

        int cornerArrayLength = hullMeshTriangleCount * 2;
        int triangleArrayLength = cornerArrayLength / 3;

        aboveWaterCorners = new int[hullMeshTriangleCount * 2];
        belowWaterCorners = new int[hullMeshTriangleCount * 2];

        this.linkedRigidbody = linkedRigidbody;

        belowWaterCorners = new int[cornerArrayLength];
        aboveWaterCorners = new int[cornerArrayLength];
        belowWaterTriangleArea = new float[triangleArrayLength];

        belowWaterTriangleCenters = new Vector3[triangleArrayLength];
        belowWaterTriangleNormals = new Vector3[triangleArrayLength];
        buoyancyForces = new Vector3[triangleArrayLength];
        frictionDragForces = new Vector3[triangleArrayLength];
        pressureForces = new Vector3[triangleArrayLength];

        oneAboveTheWaterTriangles = new int[triangleArrayLength];
        twoAboveTheWaterTriangles = new int[triangleArrayLength];

        oneAboveTheWaterTriangles = new int[hullMeshTriangleCount];
        twoAboveTheWaterTriangles = new int[hullMeshTriangleCount];

        for (int i = 0; i < hullCorners.Length; i += 3)
        {
            hullTriangleArea[i / 3] = CalculateTriangleAreFromPoints(hullVerticesLocal[hullCorners[i]], hullVerticesLocal[hullCorners[i + 1]], hullVerticesLocal[hullCorners[i + 2]]);
            hullTriangleCenters[i / 3] = (hullVerticesLocal[hullCorners[i]] + hullVerticesLocal[hullCorners[i + 1]] + hullVerticesLocal[hullCorners[i + 2]]) * 0.33333333333333333f;
            hullTriangleNormals[i / 3] = CalculateTriangleNormal(hullVerticesLocal[hullCorners[i]], hullVerticesLocal[hullCorners[i + 1]], hullVerticesLocal[hullCorners[i + 2]]);
        }

        Vector3 worldBoundingBoxOfMeshCanBeNegative = hullMeshFilter.transform.TransformVector(hullMesh.bounds.size);

        Vector3 localBoundingBoxOfMeshCanBeNegative = linkedRigidbody.transform.InverseTransformVector(worldBoundingBoxOfMeshCanBeNegative);

        Vector3 boundingBoxOfMeshPosition = linkedRigidbody.transform.InverseTransformPoint(hullMeshFilter.transform.TransformPoint(hullMesh.bounds.center));

        Vector3 belowWaterSizeCanBeNegativie = localBoundingBoxOfMeshCanBeNegative;
        belowWaterSizeCanBeNegativie.y = Mathf.Abs(worldBoundingBoxOfMeshCanBeNegative.y) * 0.5f - boundingBoxOfMeshPosition.y;
        DragAreaBelowWater = new Vector3(
            Mathf.Abs(belowWaterSizeCanBeNegativie.y * belowWaterSizeCanBeNegativie.z),
            Mathf.Abs(belowWaterSizeCanBeNegativie.x * belowWaterSizeCanBeNegativie.z),
            Mathf.Abs(belowWaterSizeCanBeNegativie.x * belowWaterSizeCanBeNegativie.y));

        linkedRigidbody.inertiaTensor = CalculateInertiaTensorOfBox(localBoundingBoxOfMeshCanBeNegative, linkedRigidbody.mass);
    }

    Vector3 CalculateInertiaTensorOfBox(Vector3 size, float mass)
    {
        Vector3 returnVector = Vector3.zero;

        returnVector.x = Calculate2DInertiaOfRectangle(size.y, size.z, mass);
        returnVector.y = Calculate2DInertiaOfRectangle(size.x, size.z, mass);
        returnVector.z = Calculate2DInertiaOfRectangle(size.x, size.y, mass);

        return returnVector;
    }

    float Calculate2DInertiaOfRectangle(float a, float b, float mass)
    {
        return factor1over12 * mass * (a * a + b * b);
    }

    private void FixedUpdate()
    {
        if (!physicsActive) return;

        GenerateCalculationMeshes();

        CalculateAndAddForcesToRigidbody(1);
    }

    public void GenerateCalculationMeshes()
    {
        float[] distancesToWater = new float[hullVerticesLocal.Length];

        //Generate global positions and distances to water
        for (int i = 0; i < hullVerticesLocal.Length; i++)
        {
            Vector3 globalVertexPosition = hullTransform.TransformPoint(hullVerticesLocal[i]);

            calculationVerticesGlobal[i] = globalVertexPosition;
            distancesToWater[i] = GetDistanceToWater(globalVertexPosition);
        }

        //Separate triangles
        int[] hullMeshTriangles = hullMesh.triangles;

        int hullMeshTriangleCount = hullMeshTriangles.Length;

        aboveWaterCornerCounter = 0;
        belowWaterCornerCounter = 0;

        int oneAboveWaterTriangleCornerCounter = 0;
        int twoAboveWaterTriangleCornerCounter = 0;

        for (int i = 0; i < hullMeshTriangleCount; i += 3)
        {
            int aboveWaterCounter = 0;

            int i1 = i + 1;
            int i2 = i + 2;
            int i3rd = i / 3;

            if (distancesToWater[hullMeshTriangles[i]] > 0) aboveWaterCounter++;
            if (distancesToWater[hullMeshTriangles[i1]] > 0) aboveWaterCounter++;
            if (distancesToWater[hullMeshTriangles[i2]] > 0) aboveWaterCounter++;

            if (aboveWaterCounter == 0)
            {
                int belowWaterCornerCounter3rd = belowWaterCornerCounter / 3;

                belowWaterCorners[belowWaterCornerCounter] = hullMeshTriangles[i];
                belowWaterCorners[belowWaterCornerCounter + 1] = hullMeshTriangles[i1];
                belowWaterCorners[belowWaterCornerCounter + 2] = hullMeshTriangles[i2];
                belowWaterTriangleArea[belowWaterCornerCounter3rd] = hullTriangleArea[i3rd];
                belowWaterTriangleCenters[belowWaterCornerCounter3rd] = hullTransform.TransformPoint(hullTriangleCenters[i3rd]);
                belowWaterTriangleNormals[belowWaterCornerCounter3rd] = hullTransform.TransformVector(hullTriangleNormals[i3rd]);

                belowWaterCornerCounter += 3;
            }
            else if (aboveWaterCounter == 1)
            {
                oneAboveTheWaterTriangles[oneAboveWaterTriangleCornerCounter] = hullMeshTriangles[i];
                oneAboveTheWaterTriangles[oneAboveWaterTriangleCornerCounter + 1] = hullMeshTriangles[i1];
                oneAboveTheWaterTriangles[oneAboveWaterTriangleCornerCounter + 2] = hullMeshTriangles[i2];

                oneAboveWaterTriangleCornerCounter += 3;
            }
            else if (aboveWaterCounter == 2)
            {
                twoAboveTheWaterTriangles[twoAboveWaterTriangleCornerCounter] = hullMeshTriangles[i];
                twoAboveTheWaterTriangles[twoAboveWaterTriangleCornerCounter + 1] = hullMeshTriangles[i1];
                twoAboveTheWaterTriangles[twoAboveWaterTriangleCornerCounter + 2] = hullMeshTriangles[i2];

                twoAboveWaterTriangleCornerCounter += 3;
            }
            else //if (aboveWaterCounter == 3)
            {
                int belowWaterCornerCounter3rd = belowWaterCornerCounter / 3;

                aboveWaterCorners[aboveWaterCornerCounter] = hullMeshTriangles[i];
                aboveWaterCorners[aboveWaterCornerCounter + 1] = hullMeshTriangles[i1];
                aboveWaterCorners[aboveWaterCornerCounter + 2] = hullMeshTriangles[i2];

                aboveWaterCornerCounter += 3;
            }
        }

        //Handle between water triangles
        int vertexCounter = hullVerticesLocal.Length;

        //One above
        for (int i = 0; i < oneAboveWaterTriangleCornerCounter; i += 3)
        {
            Vector3 point0 = calculationVerticesGlobal[oneAboveTheWaterTriangles[i]];
            Vector3 point1 = calculationVerticesGlobal[oneAboveTheWaterTriangles[i + 1]];
            Vector3 point2 = calculationVerticesGlobal[oneAboveTheWaterTriangles[i + 2]];

            int highCorner;
            int lowCorner1;
            int lowCorner2;

            //Note: The value of lowIndex1 always has to be lower than lowIndex2. Otherwise, the triangles will be inverted
            if (point0.y > point1.y)
            {
                if (point0.y > point2.y)
                {
                    //Order tested
                    highCorner = i;
                    lowCorner1 = i + 1;
                    lowCorner2 = i + 2;
                }
                else
                {
                    //Order not tested
                    highCorner = i + 2;
                    lowCorner1 = i;
                    lowCorner2 = i + 1;
                }
            }
            else
            {
                if (point1.y > point2.y)
                {
                    //Order tested
                    highCorner = i + 1;
                    lowCorner1 = i + 2;
                    lowCorner2 = i;
                }
                else
                {
                    //Order not tested
                    highCorner = i + 2;
                    lowCorner1 = i;
                    lowCorner2 = i + 1;
                }
            }

            highCorner = oneAboveTheWaterTriangles[highCorner];
            lowCorner1 = oneAboveTheWaterTriangles[lowCorner1];
            lowCorner2 = oneAboveTheWaterTriangles[lowCorner2];

            Vector3 highPoint = calculationVerticesGlobal[highCorner];
            Vector3 lowPoint1 = calculationVerticesGlobal[lowCorner1];
            Vector3 lowPoint2 = calculationVerticesGlobal[lowCorner2];

            Vector3 betweenPoint1 = Vector3.Lerp(highPoint, lowPoint1, highPoint.y / (highPoint.y - lowPoint1.y));
            Vector3 betweenPoint2 = Vector3.Lerp(highPoint, lowPoint2, highPoint.y / (highPoint.y - lowPoint2.y));

            //Triangles before vertices because of index
            int aboveWaterCornerCounter3rd = aboveWaterCornerCounter / 3;

            int belowWaterCornerCounter3rd = belowWaterCornerCounter / 3;
            belowWaterTriangleArea[belowWaterCornerCounter3rd] = CalculateTriangleAreFromPoints(lowPoint1, lowPoint2, betweenPoint2);
            belowWaterTriangleCenters[belowWaterCornerCounter3rd] = (lowPoint1 + lowPoint2 + betweenPoint2) * 0.33333333333333333f;
            belowWaterTriangleNormals[belowWaterCornerCounter3rd] = CalculateTriangleNormal(lowPoint1, lowPoint2, betweenPoint2);
            belowWaterCorners[belowWaterCornerCounter++] = lowCorner1;
            belowWaterCorners[belowWaterCornerCounter++] = lowCorner2;
            belowWaterCorners[belowWaterCornerCounter++] = vertexCounter + 1;

            belowWaterCornerCounter3rd++;
            belowWaterTriangleArea[belowWaterCornerCounter3rd] = CalculateTriangleAreFromPoints(lowPoint1, betweenPoint1, betweenPoint2);
            belowWaterTriangleCenters[belowWaterCornerCounter3rd] = (lowPoint1 + betweenPoint1 + betweenPoint2) * 0.33333333333333333f;
            belowWaterTriangleNormals[belowWaterCornerCounter3rd] = CalculateTriangleNormal(lowPoint1, betweenPoint2, betweenPoint1);
            belowWaterCorners[belowWaterCornerCounter++] = lowCorner1;
            belowWaterCorners[belowWaterCornerCounter++] = vertexCounter + 1;
            belowWaterCorners[belowWaterCornerCounter++] = vertexCounter;

            calculationVerticesGlobal[vertexCounter++] = betweenPoint1;
            calculationVerticesGlobal[vertexCounter++] = betweenPoint2;
        }

        //two above
        for (int i = 0; i < twoAboveWaterTriangleCornerCounter; i += 3)
        {
            Vector3 point0 = calculationVerticesGlobal[twoAboveTheWaterTriangles[i]];
            Vector3 point1 = calculationVerticesGlobal[twoAboveTheWaterTriangles[i + 1]];
            Vector3 point2 = calculationVerticesGlobal[twoAboveTheWaterTriangles[i + 2]];

            int lowCorner;
            int highCorner1;
            int highCorner2;

            //Note: The value of highIndex1 always has to be higher than highIndex2. Otherwise, the triangles will be inverted
            if (point0.y < point1.y)
            {
                if (point0.y < point2.y)
                {
                    //Order tested
                    lowCorner = i;
                    highCorner1 = i + 1;
                    highCorner2 = i + 2;
                }
                else
                {
                    //Order not tested
                    lowCorner = i + 2;
                    highCorner1 = i;
                    highCorner2 = i + 1;
                }
            }
            else
            {
                if (point1.y < point2.y)
                {
                    //Order tested
                    lowCorner = i + 1;
                    highCorner1 = i + 2;
                    highCorner2 = i;
                }
                else
                {
                    //Order not tested
                    lowCorner = i + 2;
                    highCorner1 = i;
                    highCorner2 = i + 1;
                }
            }

            lowCorner = twoAboveTheWaterTriangles[lowCorner];
            highCorner1 = twoAboveTheWaterTriangles[highCorner1];
            highCorner2 = twoAboveTheWaterTriangles[highCorner2];

            Vector3 lowPoint = calculationVerticesGlobal[lowCorner];
            Vector3 highPoint1 = calculationVerticesGlobal[highCorner1];
            Vector3 highPoint2 = calculationVerticesGlobal[highCorner2];

            Vector3 betweenPoint1 = Vector3.Lerp(highPoint1, lowPoint, highPoint1.y / (highPoint1.y - lowPoint.y));
            Vector3 betweenPoint2 = Vector3.Lerp(highPoint2, lowPoint, highPoint2.y / (highPoint2.y - lowPoint.y));

            //Triangles before vertices because of index
            int belowWaterCornerCounter3rd = belowWaterCornerCounter / 3;
            int aboveWaterCornerCounter3rd = aboveWaterCornerCounter / 3;

            belowWaterTriangleArea[belowWaterCornerCounter3rd] = CalculateTriangleAreFromPoints(lowPoint, betweenPoint1, betweenPoint2);
            belowWaterTriangleCenters[belowWaterCornerCounter3rd] = (lowPoint + betweenPoint1 + betweenPoint2) * 0.33333333333333333f;
            belowWaterTriangleNormals[belowWaterCornerCounter3rd] = CalculateTriangleNormal(lowPoint, betweenPoint1, betweenPoint2);
            belowWaterCorners[belowWaterCornerCounter++] = lowCorner;
            belowWaterCorners[belowWaterCornerCounter++] = vertexCounter;
            belowWaterCorners[belowWaterCornerCounter++] = vertexCounter + 1;

            calculationVerticesGlobal[vertexCounter++] = betweenPoint1;
            calculationVerticesGlobal[vertexCounter++] = betweenPoint2;
        }
    }

    public void CalculateForcesAndSaveToArray(Vector3 boatVelocity)
    {
        int belowWaterTriangles = belowWaterCornerCounter / 3;

        Vector3 linearVelocityOfCenter = boatVelocity;

        for (int i = 0; i < belowWaterTriangles; i++)
        {
            //General data
            Vector3 center = belowWaterTriangleCenters[i];
            Vector3 normal = belowWaterTriangleNormals[i];
            float area = belowWaterTriangleArea[i];

            //Buoyancy
            Vector3 buoyancyForce = waterDensity * gravity * GetDistanceToWater(center) * area * normal;
            buoyancyForce.x = 0;
            buoyancyForce.z = 0;

            buoyancyForces[i] = buoyancyForce;
        }
    }

    public void SumUpDForces()
    {
        totalBuoyancyForce = Vector3.zero;
        totalBuoyancyTorque = Vector3.zero;
        totalPressureForce = Vector3.zero;
        totalPressureTorque = Vector3.zero;
        totalFrictionDragForce = Vector3.zero;
        totalFrictionDragTorque = Vector3.zero;

        int triangles = belowWaterCornerCounter / 3;

        for (int i = 0; i < triangles; i++)
        {
            Vector3 currentBuoyancyForce = buoyancyForces[i];
            Vector3 currentFrictionDragForce = frictionDragForces[i];
            Vector3 currentPressureForce = pressureForces[i];

            totalBuoyancyForce += currentBuoyancyForce;
            totalBuoyancyTorque += Vector3.Cross(belowWaterTriangleCenters[i], currentBuoyancyForce);

            totalPressureForce += currentPressureForce;
            totalPressureTorque += Vector3.Cross(belowWaterTriangleCenters[i], currentPressureForce);

            totalFrictionDragForce += currentFrictionDragForce;
            totalFrictionDragTorque += Vector3.Cross(belowWaterTriangleCenters[i], currentFrictionDragForce);
        }
    }

    public void CalculateAndAddForcesToRigidbody(float forceMultiplier)
    {
        int belowWaterTriangles = belowWaterCornerCounter / 3;

        for (int i = 0; i < belowWaterTriangles; i++)
        {
            //General data
            Vector3 center = belowWaterTriangleCenters[i];
            Vector3 normal = belowWaterTriangleNormals[i];
            float area = belowWaterTriangleArea[i];

            //Buoyancy
            Vector3 buoyancyForce = waterDensity * gravity * GetDistanceToWater(center) * area * normal;
            buoyancyForce.x = 0;
            buoyancyForce.z = 0;

            linkedRigidbody.AddForceAtPosition(forceMultiplier * buoyancyForce, belowWaterTriangleCenters[i]);
        }
    }

    float GetDistanceToWater(Vector3 positionGlobal)
    {
        return positionGlobal.y; //Simple: Calm water at 0
    }

    public Vector3 CalculateTriangleNormal(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return Vector3.Cross(p2 - p1, p3 - p1).normalized;
    }

    public static float CalculateTriangleAreFromPoints(Vector3 A, Vector3 B, Vector3 C)
    {
        // Triangle area according to sin formula:
        // A = 0.5 * a * b * sin(gamma)
        // A = Area [m^2]
        // a = Distance between B and C [m]
        // b = Distance between A and C [m]
        // gamma = angle at point C [rad]

        float a = Vector3.Distance(C, B);
        float b = Vector3.Distance(C, A);
        float gamma = Vector3.Angle(A - C, B - C) * Mathf.Deg2Rad;
        return 0.5f * a * b * Mathf.Sin(gamma);
    }
}

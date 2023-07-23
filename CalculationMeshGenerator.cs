using UdonSharp;
using UnityEngine;

public class CalculationMeshGenerator : UdonSharpBehaviour
{
    Mesh hullMesh;
    Transform hullTransform;

    Vector3[] underwaterMeshVerticesLocal = new Vector3[0];
    int[] underwaterMeshTriagnles = new int[0];

    public Vector3[] calculationVerticesGlobal = new Vector3[0];
    public int[] aboveWaterTriangles = new int[0];
    public int[] belowWaterTriangles = new int[0];

    public float timeMs;

    Vector3[] hullVerticesLocal;
    Vector3[] hullVerticesGlobal;

    void Start()
    {
        //Use setup instead
    }

    public string state = "";

    public void Setup(Mesh hullMesh, Transform hullTransform)
    {
        this.hullMesh = hullMesh;
        this.hullTransform = hullTransform;

        hullVerticesLocal = hullMesh.vertices;
        hullVerticesGlobal = new Vector3[hullVerticesLocal.Length];
    }

    public void GenerateCalculationmeshes()
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        stopwatch.Start();

        //Generate global positions and distances to water
        for (int i = 0; i < hullVerticesLocal.Length; i++)
        {
            hullVerticesGlobal[i] = hullTransform.TransformPoint(hullVerticesLocal[i]);
        }

        float[] distancesToWater = new float[hullVerticesLocal.Length];

        for (int i = 0; i < hullVerticesLocal.Length; i++)
        {
            distancesToWater[i] = GetDistanceToWater(hullVerticesGlobal[i]);
        }

        //Separate traingles
        int[] hullMeshTriangles = hullMesh.triangles;
        int hullMeshTriangleCount = hullMeshTriangles.Length;

        int[] aboveWaterTrianglesInitial = new int[hullMeshTriangleCount];
        int aboveWaterTriangleCounter = 0;
        int[] belowWaterTrianglesInitial = new int[hullMeshTriangleCount];
        int belowWaterTriangleCounter = 0;
        int[] oneAboveTheWaterTriangles = new int[hullMeshTriangleCount];
        int oneAboveWaterTriangleCounter = 0;
        int[] twoAboveTheWaterTriangles = new int[hullMeshTriangleCount];
        int twoAboveWaterTriangleCounter = 0;

        for (int i = 0; i < hullMeshTriangleCount; i += 3)
        {
            int aboveWaterCounter = 0;

            if (distancesToWater[hullMeshTriangles[i]] > 0) aboveWaterCounter++;
            if (distancesToWater[hullMeshTriangles[i + 1]] > 0) aboveWaterCounter++;
            if (distancesToWater[hullMeshTriangles[i + 2]] > 0) aboveWaterCounter++;

            if (aboveWaterCounter == 0)
            {
                belowWaterTrianglesInitial[belowWaterTriangleCounter] = hullMeshTriangles[i];
                belowWaterTrianglesInitial[belowWaterTriangleCounter + 1] = hullMeshTriangles[i + 1];
                belowWaterTrianglesInitial[belowWaterTriangleCounter + 2] = hullMeshTriangles[i + 2];

                belowWaterTriangleCounter += 3;
            }
            else if (aboveWaterCounter == 1)
            {
                oneAboveTheWaterTriangles[oneAboveWaterTriangleCounter] = hullMeshTriangles[i];
                oneAboveTheWaterTriangles[oneAboveWaterTriangleCounter + 1] = hullMeshTriangles[i + 1];
                oneAboveTheWaterTriangles[oneAboveWaterTriangleCounter + 2] = hullMeshTriangles[i + 2];

                oneAboveWaterTriangleCounter += 3;
            }
            else if (aboveWaterCounter == 2)
            {
                twoAboveTheWaterTriangles[twoAboveWaterTriangleCounter] = hullMeshTriangles[i];
                twoAboveTheWaterTriangles[twoAboveWaterTriangleCounter + 1] = hullMeshTriangles[i + 1];
                twoAboveTheWaterTriangles[twoAboveWaterTriangleCounter + 2] = hullMeshTriangles[i + 2];

                twoAboveWaterTriangleCounter += 3;
            }
            else //if (aboveWaterCounter == 3)
            {
                aboveWaterTrianglesInitial[aboveWaterTriangleCounter] = hullMeshTriangles[i];
                aboveWaterTrianglesInitial[aboveWaterTriangleCounter + 1] = hullMeshTriangles[i + 1];
                aboveWaterTrianglesInitial[aboveWaterTriangleCounter + 2] = hullMeshTriangles[i + 2];

                aboveWaterTriangleCounter += 3;
            }
        }

        //Handle between water triangles
        Vector3[] finalVerticesGlobal = new Vector3[hullVerticesGlobal.Length + oneAboveWaterTriangleCounter / 3 * 2 + twoAboveWaterTriangleCounter / 3 * 2];
        int[] finalAboveWaterTriangles = new int[aboveWaterTriangleCounter + oneAboveWaterTriangleCounter + twoAboveWaterTriangleCounter * 2];
        int[] finalBelowWaterTriangles = new int[belowWaterTriangleCounter + oneAboveWaterTriangleCounter * 2 + twoAboveWaterTriangleCounter];

        int vertexCounter = hullVerticesGlobal.Length;
        for (int i = 0; i < hullVerticesGlobal.Length; i++)
        {
            finalVerticesGlobal[i] = hullVerticesGlobal[i];
        }

        for (int i = 0; i < aboveWaterTriangleCounter; i++)
        {
            finalAboveWaterTriangles[i] = aboveWaterTrianglesInitial[i];
        }

        for (int i = 0; i < belowWaterTriangleCounter; i++)
        {
            finalBelowWaterTriangles[i] = belowWaterTrianglesInitial[i];
        }

        //One above
        for (int i = 0; i < oneAboveWaterTriangleCounter; i += 3)
        {
            Vector3 point0 = hullVerticesGlobal[oneAboveTheWaterTriangles[i]];
            Vector3 point1 = hullVerticesGlobal[oneAboveTheWaterTriangles[i + 1]];
            Vector3 point2 = hullVerticesGlobal[oneAboveTheWaterTriangles[i + 2]];

            int highIndex;
            int lowIndex1;
            int lowIndex2;

            //Note: The value of lowIndex1 always has to be lower than lowIndex2. Otherwise, the triangles will be inverted
            if (point0.y > point1.y)
            {
                if (point0.y > point2.y)
                {
                    //Order tested
                    highIndex = i;
                    lowIndex1 = i + 1;
                    lowIndex2 = i + 2;
                }
                else
                {
                    //Order not tested
                    highIndex = i + 2;
                    lowIndex1 = i;
                    lowIndex2 = i + 1;
                }
            }
            else
            {
                if (point1.y > point2.y)
                {
                    //Order tested
                    highIndex = i + 1;
                    lowIndex1 = i + 2;
                    lowIndex2 = i;
                }
                else
                {
                    //Order not tested
                    highIndex = i + 2;
                    lowIndex1 = i;
                    lowIndex2 = i + 1;
                }
            }

            highIndex = oneAboveTheWaterTriangles[highIndex];
            lowIndex1 = oneAboveTheWaterTriangles[lowIndex1];
            lowIndex2 = oneAboveTheWaterTriangles[lowIndex2];

            Vector3 highPoint = hullVerticesGlobal[highIndex];
            Vector3 lowPoint1 = hullVerticesGlobal[lowIndex1];
            Vector3 lowPoint2 = hullVerticesGlobal[lowIndex2];

            Vector3 betweenPoint1 = Vector3.Lerp(highPoint, lowPoint1, highPoint.y / (highPoint.y - lowPoint1.y));
            Vector3 betweenPoint2 = Vector3.Lerp(highPoint, lowPoint2, highPoint.y / (highPoint.y - lowPoint2.y));

            //Triangles before vertices because of index
            finalAboveWaterTriangles[aboveWaterTriangleCounter++] = highIndex;
            finalAboveWaterTriangles[aboveWaterTriangleCounter++] = vertexCounter;
            finalAboveWaterTriangles[aboveWaterTriangleCounter++] = vertexCounter + 1;

            finalBelowWaterTriangles[belowWaterTriangleCounter++] = lowIndex1;
            finalBelowWaterTriangles[belowWaterTriangleCounter++] = lowIndex2;
            finalBelowWaterTriangles[belowWaterTriangleCounter++] = vertexCounter + 1;

            finalBelowWaterTriangles[belowWaterTriangleCounter++] = lowIndex1;
            finalBelowWaterTriangles[belowWaterTriangleCounter++] = vertexCounter + 1;
            finalBelowWaterTriangles[belowWaterTriangleCounter++] = vertexCounter;

            finalVerticesGlobal[vertexCounter++] = betweenPoint1;
            finalVerticesGlobal[vertexCounter++] = betweenPoint2;
        }

        //two above
        for (int i = 0; i < twoAboveWaterTriangleCounter; i += 3)
        {
            Vector3 point0 = hullVerticesGlobal[twoAboveTheWaterTriangles[i]];
            Vector3 point1 = hullVerticesGlobal[twoAboveTheWaterTriangles[i + 1]];
            Vector3 point2 = hullVerticesGlobal[twoAboveTheWaterTriangles[i + 2]];

            int lowIndex;
            int highIndex1;
            int highIndex2;

            //Note: The value of highIndex1 always has to be higher than highIndex2. Otherwise, the triangles will be inverted
            if (point0.y < point1.y)
            {
                if (point0.y < point2.y)
                {
                    //Order tested
                    lowIndex = i;
                    highIndex1 = i + 1;
                    highIndex2 = i + 2;
                }
                else
                {
                    //Order not tested
                    lowIndex = i + 2;
                    highIndex1 = i;
                    highIndex2 = i + 1;
                }
            }
            else
            {
                if (point1.y < point2.y)
                {
                    //Order tested
                    lowIndex = i + 1;
                    highIndex1 = i + 2;
                    highIndex2 = i;
                }
                else
                {
                    //Order not tested
                    lowIndex = i + 2;
                    highIndex1 = i;
                    highIndex2 = i + 1;
                }
            }

            lowIndex = twoAboveTheWaterTriangles[lowIndex];
            highIndex1 = twoAboveTheWaterTriangles[highIndex1];
            highIndex2 = twoAboveTheWaterTriangles[highIndex2];

            Vector3 lowPoint = hullVerticesGlobal[lowIndex];
            Vector3 highPoint1 = hullVerticesGlobal[highIndex1];
            Vector3 highPoint2 = hullVerticesGlobal[highIndex2];

            Vector3 betweenPoint1 = Vector3.Lerp(highPoint1, lowPoint, highPoint1.y / (highPoint1.y - lowPoint.y));
            Vector3 betweenPoint2 = Vector3.Lerp(highPoint2, lowPoint, highPoint2.y / (highPoint2.y - lowPoint.y));

            //Triangles before vertices because of index
            finalBelowWaterTriangles[belowWaterTriangleCounter++] = lowIndex;
            finalBelowWaterTriangles[belowWaterTriangleCounter++] = vertexCounter;
            finalBelowWaterTriangles[belowWaterTriangleCounter++] = vertexCounter + 1;

            finalAboveWaterTriangles[aboveWaterTriangleCounter++] = highIndex1;
            finalAboveWaterTriangles[aboveWaterTriangleCounter++] = highIndex2;
            finalAboveWaterTriangles[aboveWaterTriangleCounter++] = vertexCounter + 1;

            finalAboveWaterTriangles[aboveWaterTriangleCounter++] = highIndex1;
            finalAboveWaterTriangles[aboveWaterTriangleCounter++] = vertexCounter + 1;
            finalAboveWaterTriangles[aboveWaterTriangleCounter++] = vertexCounter;

            finalVerticesGlobal[vertexCounter++] = betweenPoint1;
            finalVerticesGlobal[vertexCounter++] = betweenPoint2;
        }

        //Finalize
        aboveWaterTriangles = finalAboveWaterTriangles;
        belowWaterTriangles = finalBelowWaterTriangles;
        calculationVerticesGlobal = finalVerticesGlobal;

        stopwatch.Stop();

        timeMs = (float)(stopwatch.Elapsed.TotalSeconds * 1000);
    }



    float GetDistanceToWater(Vector3 positionGlobal)
    {
        return positionGlobal.y; //Simple: Calm water at 0
    }
}

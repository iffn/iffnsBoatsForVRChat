//#define complexCalculation

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[DefaultExecutionOrder(100)]
public class BoatAnalyzer : UdonSharpBehaviour
{
    [SerializeField] BoatController linkedBoatController;
    float belowSurfaceDistance = 50;

    //Original data
    Vector3 originalPosition;
    Vector3 currentVelocity;

    void Start()
    {
        CheckFullySubmergeDrag();
    }

    void CheckFullySubmergeDrag()
    {
        CalculateFullySubmergedDrag(10 * Vector3.forward);
        CalculateFullySubmergedDrag(20 * Vector3.forward);
        CalculateFullySubmergedDrag(30 * Vector3.forward);
    }

    void CheckForContinuousDisplacementMass()
    {
        belowSurfaceDistance = 5;
        CalculateFullDisplacementMass();

        belowSurfaceDistance = 10;
        CalculateFullDisplacementMass();

        belowSurfaceDistance = 20;
        CalculateFullDisplacementMass();
    }

    void SaveCurrentData()
    {
        originalPosition = linkedBoatController.transform.position;
    }

    void RestoreData()
    {
        linkedBoatController.transform.position = originalPosition;
    }

    void CalculateFullySubmergedDrag(Vector3 velocity)
    {
        SaveCurrentData();

        linkedBoatController.transform.position = belowSurfaceDistance * Vector3.down;
        HullCalculator calculator = linkedBoatController.LinkedHullCalculator;

        calculator.GenerateCalculationMeshes();

#if complexCalculation
        calculator.CalculateForcesAndSaveToArray(velocity, Vector3.zero);
#else
        calculator.CalculateForcesAndSaveToArray(velocity);
#endif


        calculator.SumUpDForces();

        Debug.Log($"Drag force when completely submerged at {velocity.magnitude}m/s = {(calculator.totalFrictionDragForce + calculator.totalPressureForce)}"); //.magnitude.ToString("G30")}");

        RestoreData();
    }

    void CalculateFullDisplacementMass()
    {
        SaveCurrentData();

        linkedBoatController.transform.position = belowSurfaceDistance * Vector3.down;

        HullCalculator calculator = linkedBoatController.LinkedHullCalculator;

        calculator.GenerateCalculationMeshes();
#if complexCalculation
        calculator.CalculateForcesAndSaveToArray(Vector3.zero, Vector3.zero);
#else
        calculator.CalculateForcesAndSaveToArray(Vector3.zero);
#endif

        calculator.SumUpDForces();

        Debug.Log($"Buoyancy force when completely submerged at {belowSurfaceDistance}m = {calculator.totalBuoyancyForce.magnitude.ToString("G30")}");
        
        RestoreData();
    }
}

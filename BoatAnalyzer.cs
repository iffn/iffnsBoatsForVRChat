
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[DefaultExecutionOrder(100)]
public class BoatAnalyzer : UdonSharpBehaviour
{
    [SerializeField] BoatController linkedBoatController;
    float belowSurfaceDistance = 50;

    void Start()
    {
        belowSurfaceDistance = 5;
        CalculateFullDisplacementMass();

        belowSurfaceDistance = 10;
        CalculateFullDisplacementMass();

        belowSurfaceDistance = 20;
        CalculateFullDisplacementMass();
    }

    void CalculateFullDisplacementMass()
    {
        Vector3 originalPosition = linkedBoatController.transform.position;

        linkedBoatController.transform.position = belowSurfaceDistance * Vector3.down;

        HullCalculator calculator = linkedBoatController.LinkedHullCalculator;

        calculator.GenerateCalculationMeshes();

        calculator.CalculateForcesAndSaveToArray(Vector3.zero, Vector3.zero);

        calculator.SumUpDForces();

        Debug.Log($"Buoyancy force when completely submerged at {belowSurfaceDistance}m = {calculator.totalBuoyancyForce.magnitude.ToString("G30")}");

        linkedBoatController.transform.position = originalPosition;
    }
}

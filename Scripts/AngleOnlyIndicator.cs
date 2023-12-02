
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class AngleOnlyIndicator : Indicator
{
    [SerializeField] Vector3 localRotationAnglePerUnit;

    Quaternion originalLocalRotation;
    Quaternion localRotationAnglePerUnitAsQuaternion;

    bool setupDidRun = false;

    private void Start() //Start somehow broken
    {
        SetupOnce();
    }

    void SetupOnce()
    {
        if (setupDidRun) return;
        setupDidRun = true;

        originalLocalRotation = transform.localRotation;
        localRotationAnglePerUnitAsQuaternion = Quaternion.Euler(localRotationAnglePerUnit);
    }

    public override float InputValue
    {
        set
        {
            if (!setupDidRun) SetupOnce(); //Update of one script can run before Start of another script

            transform.localRotation = Quaternion.LerpUnclamped(originalLocalRotation, localRotationAnglePerUnitAsQuaternion, value);
        }
    }
}

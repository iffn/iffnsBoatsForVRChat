
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class AngleOnlyIndicator : Indicator
{
    [SerializeField] Vector3 localRotationAnglePerUnit;

    Quaternion originalLocalRotation;
    Quaternion localRotationAnglePerUnitAsQuaternion;

    private void Start()
    {
        originalLocalRotation = transform.localRotation;
        localRotationAnglePerUnitAsQuaternion = Quaternion.Euler(localRotationAnglePerUnit);
    }

    public override float InputValue
    {
        set
        {
            transform.localRotation = Quaternion.LerpUnclamped(originalLocalRotation, localRotationAnglePerUnitAsQuaternion, value);
        }
    }
}

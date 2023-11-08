
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public abstract class Indicator : UdonSharpBehaviour
{
    public abstract float InputValue { set; }
}

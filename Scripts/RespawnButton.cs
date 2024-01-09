
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class RespawnButton : UdonSharpBehaviour
{
    [SerializeField] BoatController boat;

    public override void Interact()
    {
        if (!boat) return;

        boat.Respawn();
    }
}

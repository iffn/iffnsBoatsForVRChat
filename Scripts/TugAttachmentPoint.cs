
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class TugAttachmentPoint : UdonSharpBehaviour
{
    [SerializeField] BoatController linkedBoatController;

    public BoatController LinkedBoatController
    {
        get
        {
            return linkedBoatController;
        }
    }

    public override void Interact()
    {
        //Just here to show interaction
    }
}

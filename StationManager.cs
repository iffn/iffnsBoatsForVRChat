
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class StationManager : UdonSharpBehaviour
{
    [SerializeField] VRCStation LinkedStation;

    public bool inStation = false;

    public override void Interact()
    {
        LinkedStation.UseStation(Networking.LocalPlayer);

        inStation = true;
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Return))
        {
            LinkedStation.ExitStation(Networking.LocalPlayer);

            inStation = false;
        }
    }
}

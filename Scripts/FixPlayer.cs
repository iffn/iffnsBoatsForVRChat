
using Newtonsoft.Json.Linq;
using NUMovementPlatformSyncMod;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[RequireComponent(typeof(Collider))]
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FixPlayer : UdonSharpBehaviour
{
    [UdonSynced] int attachedPlayer = 0; //ID starts with 1

    [SerializeField] NUMovementSyncMod linkedMovementController;
    [SerializeField] BoatController linkedBoatController;

    Collider linkedCollider;
    bool isFixed = false;
    VRCPlayerApi localPlayer;
    int myPlayerID;

    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        myPlayerID = localPlayer.playerId;

        linkedCollider = transform.GetComponent<Collider>();
    }

    public override void OnDeserialization()
    {
        //Check for double occupation
        if (isFixed && attachedPlayer != myPlayerID)
        {
            //Keep person with lower player ID
            if (attachedPlayer < myPlayerID)
            {
                //Kick the player out since they have a higher ID than the new player
                Exit();
            }
            else
            {
                //Fix the synced player ID
                attachedPlayer = myPlayerID;
                Sync();
            }
        }

        linkedCollider.enabled = attachedPlayer == 0;
    }

    void Sync()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(localPlayer, gameObject);
        RequestSerialization();
    }

    void Exit()
    {
        isFixed = false;

        linkedMovementController._SetCanMove(true);
        linkedBoatController.LocalPlayerExited();
        linkedCollider.enabled = true;
    }

    public override void Interact()
    {
        isFixed = true;

        linkedMovementController._SetCanMove(false);
        linkedBoatController.LocalPlayerEntered();
        linkedCollider.enabled = false;

        attachedPlayer = myPlayerID;
        Sync();
    }

    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if(!isFixed) return;
        if (!value) return;

        Exit();

        attachedPlayer = 0;
        Sync();
    }
}

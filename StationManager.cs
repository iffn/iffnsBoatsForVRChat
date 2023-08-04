
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
[RequireComponent(typeof(VRCStation))]
public class StationManager : UdonSharpBehaviour
{
    [UdonSynced] Vector3 syncedPlayerPosition;

    [SerializeField] float HeadXOffset = 0.25f;
    public VRCStation LinkedStation { get; private set; }
    public VRCPlayerApi SeatedPlayer { get; private set; }
    public bool localPlayerInStation { get; private set; } = false;
    Collider linkedCollider;

    [SerializeField] UdonSharpBehaviour[] EntryAndExitInformants;

    float transitionSpeed = 0.2f;

    bool isOwner;

    Transform playerMover;

    public Vector3 preferredStationPosition = 0.6f * Vector3.down;

    void ResetStationPosition()
    {
        playerMover.localPosition = preferredStationPosition;
    }

    private void Start()
    {
        LinkedStation = (VRCStation)GetComponent(typeof(VRCStation));

        playerMover = LinkedStation.stationEnterPlayerLocation;

        ResetStationPosition();

        isOwner = Networking.LocalPlayer.IsOwner(gameObject);

        linkedCollider = transform.GetComponent<Collider>();
    }


    public override void Interact()
    {
        LinkedStation.UseStation(Networking.LocalPlayer);
    }

    public float Remap(float iMin, float iMax, float oMin, float oMax, float iValue)
    {
        float t = Mathf.InverseLerp(iMin, iMax, iValue);
        return Mathf.Lerp(oMin, oMax, t);
    }

    private void Update()
    {
        if (SeatedPlayer == null || SeatedPlayer.IsUserInVR()) return;
        if (LinkedStation.PlayerMobility == VRCStation.Mobility.Mobile) return;

        //360 stuff
        Quaternion headRotation;

        //Rotation:
#if UNITY_EDITOR
        headRotation = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
#else
        headRotation = SeatedPlayer.GetBoneRotation(HumanBodyBones.Head);
#endif

        Quaternion relativeHeadRotation = Quaternion.Inverse(playerMover.rotation) * headRotation;
        float headHeading = relativeHeadRotation.eulerAngles.y;
        playerMover.localRotation = Quaternion.Euler(headHeading * Vector3.up);

        //Offset:
        float xOffset = 0;
        if (headHeading > 45 && headHeading < 180)
        {
            xOffset = Remap(iMin: 45, iMax: 90, oMin: 0, oMax: HeadXOffset, iValue: headHeading);
        }
        else if (headHeading < 315 && headHeading > 180)
        {
            xOffset = -Remap(iMin: 315, iMax: 270, oMin: 0, oMax: HeadXOffset, iValue: headHeading);
        }

        //Destktop movement stuff
        if (localPlayerInStation)
        {
            bool sync = false;

            if (Input.GetKey(KeyCode.PageUp))
            {
                preferredStationPosition += Time.deltaTime * transitionSpeed * Vector3.up;
                sync = true;
            }

            if (Input.GetKey(KeyCode.PageDown))
            {
                preferredStationPosition += Time.deltaTime * transitionSpeed * Vector3.down;
                sync = true;
            }

            if (Input.GetKey(KeyCode.UpArrow))
            {
                preferredStationPosition += Time.deltaTime * transitionSpeed * Vector3.forward;
                sync = true;
            }

            if (Input.GetKey(KeyCode.DownArrow))
            {
                preferredStationPosition += Time.deltaTime * transitionSpeed * Vector3.back;
                sync = true;
            }

            if (Input.GetKey(KeyCode.LeftArrow))
            {
                preferredStationPosition += Time.deltaTime * transitionSpeed * Vector3.left;
                sync = true;
            }

            if (Input.GetKey(KeyCode.RightArrow))
            {
                preferredStationPosition += Time.deltaTime * transitionSpeed * Vector3.right;
                sync = true;
            }

            if (sync)
            {
                RequestSerialization();
            }

            playerMover.localPosition = preferredStationPosition + xOffset * Vector3.right;
        }
        else
        {
            playerMover.localPosition = syncedPlayerPosition + xOffset * Vector3.right;
        }
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if(player.isLocal)
        {
            isOwner = true;

            RequestSerialization();
        }
        else
        {
            if (isOwner)
            {
                preferredStationPosition = playerMover.localPosition;
            }

            isOwner = false;
        }
    }

    public override void OnPreSerialization()
    {
        syncedPlayerPosition = preferredStationPosition;
    }

    public override void OnDeserialization()
    {
        playerMover.localPosition = syncedPlayerPosition;
    }

    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if (value && localPlayerInStation && LinkedStation.disableStationExit)
        {
            LinkedStation.ExitStation(Networking.LocalPlayer);
        }
    }

    public override void OnStationEntered(VRCPlayerApi player)
    {
        SeatedPlayer = player;

        if (player.isLocal)
        {
            linkedCollider.enabled = false;

            localPlayerInStation = true;

            Networking.SetOwner(player, gameObject);

            foreach (UdonSharpBehaviour behavior in EntryAndExitInformants)
            {
                behavior.SendCustomEvent("LocalPlayerEntered");
            }
        }
        else
        {
            foreach(UdonSharpBehaviour behavior in EntryAndExitInformants)
            {
                behavior.SendCustomEvent("RemotePlayerEntered");
            }
        }
    }

    public override void OnStationExited(VRCPlayerApi player)
    {
        SeatedPlayer = null;

        if (player.isLocal)
        {
            linkedCollider.enabled = true;

            localPlayerInStation = false;

            foreach (UdonSharpBehaviour behavior in EntryAndExitInformants)
            {
                behavior.SendCustomEvent("LocalPlayerExited");
            }
        }
        else
        {
            ResetStationPosition();

            foreach (UdonSharpBehaviour behavior in EntryAndExitInformants)
            {
                behavior.SendCustomEvent("RemotePlayerExited");
            }
        }
    }
}

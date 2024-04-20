
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using NUMovementPlatformSyncMod;
using iffnsStuff.iffnsVRCStuff.InteractionController;

public class NUMovementPlatformReferenceForInteractions : UdonSharpBehaviour
{
    [SerializeField] NUMovementSyncMod linkedNUMovementPlaftormMod;
    [SerializeField] InteractionController linkedInteractionController;

    public void Start()
    {
        if (nameof(NUMovementPlafrormChanged).Equals(linkedNUMovementPlaftormMod.PlatformChangeEventName))
        {
            Debug.LogWarning($"Warning: {nameof(NUMovementPlatformReferenceForInteractions)} cannot link events due to naming mismatch");
        }
    }

    public void NUMovementPlafrormChanged()
    {
        linkedInteractionController.ReferenceTransform = linkedNUMovementPlaftormMod.GroundTransformLink;
    }
}

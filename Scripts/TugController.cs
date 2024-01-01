
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[RequireComponent(typeof(VRCObjectSync))]
public class TugController : UdonSharpBehaviour
{
    [SerializeField] Rigidbody originRigidbody;
    [SerializeField] Transform originPoint;
    [SerializeField] Transform rope;
    [SerializeField] Transform targetPoint;
    [SerializeField] float springFactor;

    //Static values
    bool inVR;
    VRCPlayerApi localPlayer;


    //Runtime values
    TugAttachmentPoint currentAttachmentPoint;
    bool held = false;
    bool ropeActive = false;
    float idleDistance = 0f;
    Rigidbody targetRigidbody;

    public float ropeForceDebug;

    void Start()
    {
        localPlayer = Networking.LocalPlayer;
        inVR = localPlayer.IsUserInVR();
    }

    public override void PostLateUpdate()
    {
        if(held)
        {
            VRCPlayerApi.TrackingData rightHand = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);

            transform.SetPositionAndRotation(rightHand.position, rightHand.rotation);
        }

        VisualizeRope();
    }

    void VisualizeRope()
    {
        //rope.transform.position = originPoint.position; //Keep default positon
        rope.LookAt(originPoint.position);
        Vector3 ropeScale = rope.transform.localScale;
        ropeScale.z = (targetPoint.position - originPoint.position).magnitude;
        rope.transform.localScale = ropeScale;
    }

    void AttachRope(TugAttachmentPoint attachmentPoint)
    {
        if(!attachmentPoint.LinkedBoatController.TryEnableTowing()) return;
        
        transform.parent = attachmentPoint.transform;
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        //transform.SetPositionAndRotation(attachmentPoint.transform.position, attachmentPoint.transform.rotation);

        currentAttachmentPoint = attachmentPoint;
        idleDistance = (originPoint.position - targetPoint.position).magnitude;
        targetPoint = attachmentPoint.transform;
        targetRigidbody = attachmentPoint.LinkedBoatController.LinkedRigidbody;
        held = false;
        ropeActive = true;
    }

    void DetachRope()
    {
        transform.parent = originPoint.transform;
        ropeActive = false;
    }


    private void FixedUpdate()
    {
        if (ropeActive)
        {
            Vector3 originPosition = originPoint.position;
            Vector3 targetPosition = targetPoint.position;

            Vector3 offset = (targetPosition - originPosition);

            float distance = offset.magnitude;

            if(distance > idleDistance)
            {
                float force = (distance - idleDistance) * springFactor;

                Vector3 direction = offset.normalized;

                ropeForceDebug = force;

                originRigidbody.AddForceAtPosition(force * direction, originPosition);
                targetRigidbody.AddForceAtPosition(-force * direction, targetPosition);
            }
        }
    }

    public override void Interact()
    {
        if(ropeActive) DetachRope();

        held = true;

        if(!Networking.IsOwner(gameObject)) Networking.SetOwner(localPlayer, gameObject);
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (!held) return;

        if (inVR)
        {

        }
        else
        {
            VRCPlayerApi.TrackingData head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

            Ray ray = new Ray(head.position, head.rotation * Vector3.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                if (hit.collider != null) //At least VRChat client sim canvas hit collider somehow null
                {
                    Transform hitObject = hit.collider.transform;

                    //if (hitObject.transform.TryGetComponent(out TugAttachmentPoint attachmentPoint)) //Not exposed (...)

                    TugAttachmentPoint attachmentPoint = hitObject.transform.GetComponent<TugAttachmentPoint>();

                    if(attachmentPoint != null)
                    {
                        AttachRope(attachmentPoint);
                    }
                }
            }
        }
    }

    public override void InputDrop(bool value, UdonInputEventArgs args)
    {
        if (!held) return;

        held = false;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!player.isLocal)
        {
            held = false;
            if (ropeActive) DetachRope();
        }
    }
}

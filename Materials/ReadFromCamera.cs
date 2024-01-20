//#define logFrequency
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Wrapper.Modules;

public class ReadFromCamera : UdonSharpBehaviour
{

    [SerializeField] Transform[] debugTransforms;

    [SerializeField] Texture linkedRenderTexture;
    [SerializeField] Camera linkedCamera;
    public float  waveHeight = 1f;

#if logFrequency
    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
#endif

    //Fixed parameters
    public int resolution = 256;
    public float[] pixels = new float[8*8];
    public float cameraSize = 1f;
    public float pixelSize = 0.1f;
    public float invertedPixelSize = 0.1f;
    public int halfResolution = 256 / 2;
    public Vector3 centerOffset;
    
    void Start()
    {
        resolution = linkedRenderTexture.width;
        pixels = new float[resolution * resolution];
        halfResolution = resolution / 2;

        //Currently driven by camera orthographic size --> Swtich to bounding box
        cameraSize = linkedCamera.orthographicSize;
        linkedCamera.transform.localScale = new Vector3(cameraSize * 2, cameraSize * 2, linkedCamera.transform.localScale.z);
        pixelSize = cameraSize / resolution;
        invertedPixelSize = 1f / pixelSize;
        centerOffset = new Vector3(cameraSize * 0.5f, 0, cameraSize * 0.5f);


        VRCAsyncGPUReadback.Request(linkedRenderTexture, 0, TextureFormat.RFloat, (IUdonEventReceiver)this);
#if logFrequency
        sw.Restart();
#endif
    }

    /*
    void OnPostRender()
    {
        VRCAsyncGPUReadback.Request(linkedRenderTexture, 0, TextureFormat.RFloat, (IUdonEventReceiver)this);

        Debug.Log("post");
    }
    */
    


    private void FixedUpdate()
    {
        foreach(Transform debugTransform in debugTransforms)
        {
            Vector3 localPosition = debugTransform.localPosition;

            localPosition.y = GetWaterHeightAtPosition(localPosition);

            debugTransform.position = localPosition;
        }
    }

    public Vector3 relativePositionDebug;
    public int debugIndex;
    public float debugValue;

    float GetWaterHeightAtPosition(Vector3 localPosition)
    {
        Vector3 relativePosition = cameraSize * localPosition + centerOffset;

        relativePositionDebug = relativePosition;

        int index = Mathf.RoundToInt(relativePosition.x * invertedPixelSize) * resolution
            + Mathf.RoundToInt(relativePosition.z * invertedPixelSize);

        debugIndex = index;

        if (index < 0) index = 0;
        else if (index > pixels.Length - 1) index = pixels.Length - 1;

        float val = pixels[index];

        debugValue = pixels[index];

        //return debugValue * waveHeight;

        return val * waveHeight;
    }



    //Source: https://creators.vrchat.com/worlds/vrc-graphics/asyncgpureadback/
    public override void OnAsyncGpuReadbackComplete(VRCAsyncGPUReadbackRequest request)
    {
        if (!request.hasError)
        {
#if logFrequency
            Debug.Log($"Took {sw.Elapsed.TotalSeconds * 1000.0}ms");
            sw.Restart();
#endif
            VRCAsyncGPUReadback.Request(linkedRenderTexture, 0, TextureFormat.RFloat, (IUdonEventReceiver)this);

            request.TryGetData(pixels);

        }
        else
        {
            Debug.LogError("GPU readback error!");
            return;
        }
    }
}

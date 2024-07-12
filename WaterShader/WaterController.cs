
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class WaterController : UdonSharpBehaviour
{
    [Header("Unity assignments")]
    [SerializeField] CustomRenderTexture linkedCRT;
    [SerializeField] Material WaterInitializationMaterial;
    [SerializeField] Material WaterCalculationMaterial;

    [Header("Debug")]
    public Material debugMaterial;
    public float FPS;
    public double CRTRefreshRateDebug;
    public Vector2Int grabCoordinate;
    public float r;
    public float g;
    public float b;
    public float a;

    int initialTimeCounter;
    int resolution = 256;
    Color[] pixels = new Color[8 * 8];
    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

    void InitializeMaterial()
    {
        initialTimeCounter = Time.frameCount;
        linkedCRT.material = WaterInitializationMaterial;

        //linkedCRT.Initialize();
        //GL.Clear(true, true, new Color(0.5f, 0.5f, 0.5f));
    }


    void Start()
    {
        resolution = linkedCRT.width;
        pixels = new Color[resolution * resolution];

        InitializeMaterial();

        VRCAsyncGPUReadback.Request(linkedCRT, 0, TextureFormat.RGBAFloat, (IUdonEventReceiver)this);
    }

    void Update()
    {
        FPS = 1f / Time.deltaTime;

        if (Time.frameCount == initialTimeCounter + 60)
        {
            linkedCRT.material = WaterCalculationMaterial;
        }

        Color debugColor = GetPixel(grabCoordinate.x, grabCoordinate.y);
        
        r = debugColor.r;
        g = debugColor.g;
        b = debugColor.b;
        a = debugColor.a;

        debugMaterial = linkedCRT.material;
    }

    public override void OnAsyncGpuReadbackComplete(VRCAsyncGPUReadbackRequest request)
    {
        if (!request.hasError)
        {
            CRTRefreshRateDebug = 1.0 / sw.Elapsed.TotalSeconds;
            sw.Restart();

            VRCAsyncGPUReadback.Request(linkedCRT, 0, TextureFormat.RGBAFloat, (IUdonEventReceiver)this);

            request.TryGetData(pixels);
        }
        else
        {
            Debug.LogError("GPU readback error!");
            return;
        }
    }

    Color GetPixel(int x, int y)
    {
        return pixels[x + resolution * y];
    }

    public void InitializeLinkedCRT()
    {
        linkedCRT.Initialize();
    }
}

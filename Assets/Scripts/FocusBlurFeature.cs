using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>
/// ODAK BULANIKLIGI: oyuncu ve demirci net, cevresi hafif bulanik (alan derinligi hissi).
/// Renderer2D'ye eklenir. Ayarlar: FocusBlurDriver (sahnede) - guc, net alan boyutu, acik/kapali.
/// Sadece ana oyun kamerasinda calisir.
/// </summary>
public class FocusBlurFeature : ScriptableRendererFeature
{
    public Shader shader;
    public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

    // FocusBlurDriver doldurur
    public static bool Active;
    public static float Strength = 1f;
    public static float BlurSize = 1.6f;
    public static Vector4 Focus0, Focus1;
    public static bool Use0, Use1;
    public static Vector4 Shape = new Vector4(0.16f, 0.5f, 0.3f, 0.7f);

    private Material mat;
    private Pass pass;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find("ProjectM/FocusBlur");
        if (shader != null) mat = CoreUtils.CreateEngineMaterial(shader);
        pass = new Pass { renderPassEvent = passEvent };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!Active || mat == null || Strength <= 0.001f) return;
        var cam = renderingData.cameraData.camera;
        if (cam != Camera.main || renderingData.cameraData.cameraType != CameraType.Game) return;
        pass.Setup(mat);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(mat);
    }

    private class Pass : ScriptableRenderPass
    {
        private Material m;
        private class CData { public TextureHandle src; public TextureHandle blur; public Material mat; public Vector4 f0, f1, shape, prm; }
        private class Data { public TextureHandle src; public TextureHandle blur; public Material mat; public int pass; public Vector4 dir; }

        public void Setup(Material material) { m = material; requiresIntermediateTexture = true; }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData)
        {
            var res = frameData.Get<UniversalResourceData>();
            

            TextureHandle src = res.cameraColor;
            var desc = rg.GetTextureDesc(src);
            desc.clearBuffer = false;
            desc.msaaSamples = MSAASamples.None;
            desc.depthBufferBits = 0;

            var half = desc;
            half.width = Mathf.Max(1, desc.width / 2);
            half.height = Mathf.Max(1, desc.height / 2);
            half.name = "_FocusBlurA";
            TextureHandle a = rg.CreateTexture(half);
            half.name = "_FocusBlurB";
            TextureHandle b = rg.CreateTexture(half);
            desc.name = "_FocusBlurOut";
            TextureHandle outTex = rg.CreateTexture(desc);

            float tx = FocusBlurFeature.BlurSize / half.width;
            float ty = FocusBlurFeature.BlurSize / half.height;

            AddBlur(rg, src, a, 0, new Vector4(tx, 0, 0, 0), "FocusBlur H");
            AddBlur(rg, a, b, 0, new Vector4(0, ty, 0, 0), "FocusBlur V");
            // ikinci tur (daha yumusak)
            AddBlur(rg, b, a, 0, new Vector4(tx * 1.6f, 0, 0, 0), "FocusBlur H2");
            AddBlur(rg, a, b, 0, new Vector4(0, ty * 1.6f, 0, 0), "FocusBlur V2");

            using (var builder = rg.AddRasterRenderPass<CData>("FocusBlur Composite", out var cd))
            {
                cd.src = src; cd.blur = b; cd.mat = m;
                cd.f0 = FocusBlurFeature.Focus0; cd.f1 = FocusBlurFeature.Focus1; cd.shape = FocusBlurFeature.Shape;
                float aspect = (float)desc.width / Mathf.Max(1, desc.height);
                cd.prm = new Vector4(FocusBlurFeature.Strength, aspect, FocusBlurFeature.Use0 ? 1f : 0f, FocusBlurFeature.Use1 ? 1f : 0f);
                builder.UseTexture(src);
                builder.UseTexture(b);
                builder.SetRenderAttachment(outTex, 0);
                builder.SetRenderFunc((CData data, RasterGraphContext ctx) =>
                {
                    data.mat.SetTexture("_BlurTex", data.blur);
                    data.mat.SetVector("_Focus0", data.f0);
                    data.mat.SetVector("_Focus1", data.f1);
                    data.mat.SetVector("_FocusShape", data.shape);
                    data.mat.SetVector("_FocusParams", data.prm);
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.mat, 1);
                });
            }
            // sonucu kamera rengine geri kopyala
            rg.AddCopyPass(outTex, src, "FocusBlur CopyBack");
        }

        private void AddBlur(RenderGraph rg, TextureHandle from, TextureHandle to, int passIndex, Vector4 dir, string name)
        {
            using (var builder = rg.AddRasterRenderPass<Data>(name, out var d))
            {
                d.src = from; d.mat = m; d.pass = passIndex; d.dir = dir;
                builder.UseTexture(from);
                builder.SetRenderAttachment(to, 0);
                builder.SetRenderFunc((Data data, RasterGraphContext ctx) =>
                {
                    data.mat.SetVector("_BlurDir", data.dir);
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.mat, data.pass);
                });
            }
        }
    }
}

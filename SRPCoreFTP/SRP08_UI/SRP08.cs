using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP08 : RenderPipelineAsset
{
    public SRP08CustomParameter SRP08CP = new SRP08CustomParameter();
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP08", priority = 1)]
    static void CreateSRP08()
    {
        var instance = ScriptableObject.CreateInstance<SRP08>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP08.asset");
    }
    #endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        SRP08CP.ClearColor = ClearColor;
        SRP08CP.DrawSkybox = DrawSkybox;
        SRP08CP.DrawOpaque = DrawOpaque;
        SRP08CP.DrawTransparent = DrawTransparent;
        return new SRP08Instance(SRP08CP);
    }
}

public class SRP08Instance : RenderPipeline
{
    public SRP08CustomParameter SRP08CP;

    public SRP08Instance(SRP08CustomParameter SRP08CustomParameter)
    {
        SRP08CP = SRP08CustomParameter;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP08Rendering.Render(renderContext, cameras,SRP08CP);
    }
}

public static class SRP08Rendering
{
    private static readonly ShaderPassName m_UnlitPassName = new ShaderPassName("SRPDefaultUnlit");

    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras, SRP08CustomParameter SRP08CP)
    {
        foreach (Camera camera in cameras)
        {
            ScriptableCullingParameters cullingParams;

            // Stereo-aware culling parameters are configured to perform a single cull for both eyes
            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            CullResults cull = new CullResults();
            CullResults.Cull(ref cullingParams, context, ref cull);

            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            context.SetupCameraProperties(camera);

            // clear depth buffer
            CommandBuffer cmd = new CommandBuffer();
            cmd.ClearRenderTarget(true, !SRP08CP.DrawSkybox, SRP08CP.ClearColor);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            // Setup global lighting shader variables
            //SetupLightShaderVariables(cull.visibleLights, context);

            if(SRP08CP.DrawSkybox)
            {
                // Draw skybox
                context.DrawSkybox(camera);
            }

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            drawSettings.SetShaderPassName(1,m_UnlitPassName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            if (SRP08CP.DrawOpaque)
            {
                // Draw opaque objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonOpaque;
                filterSettings.renderQueueRange = RenderQueueRange.opaque;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
            }

            if (SRP08CP.DrawTransparent)
            {
                // Draw transparent objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonTransparent;
                filterSettings.renderQueueRange = RenderQueueRange.transparent;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
            }

            context.Submit();
        }
    }
}

public class SRP08CustomParameter
{
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    public SRP08CustomParameter()
    {

    }
}
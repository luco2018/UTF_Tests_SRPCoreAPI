using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP02 : RenderPipelineAsset
{
    public SRP02CustomParameter SRP02CP = new SRP02CustomParameter();
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP02", priority = 1)]
    static void CreateSRP02()
    {
        var instance = ScriptableObject.CreateInstance<SRP02>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP02.asset");
    }
    #endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        SRP02CP.ClearColor = ClearColor;
        SRP02CP.DrawSkybox = DrawSkybox;
        SRP02CP.DrawOpaque = DrawOpaque;
        SRP02CP.DrawTransparent = DrawTransparent;
        return new SRP02Instance(SRP02CP);
    }
}

public class SRP02Instance : RenderPipeline
{
    public SRP02CustomParameter SRP02CP;

    public SRP02Instance(SRP02CustomParameter SRP02CustomParameter)
    {
        SRP02CP = SRP02CustomParameter;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        Camera[] defaultCameras;
        Camera[] customCameras;

        SRPDefault.FilterCameras(cameras,out defaultCameras, out customCameras );
        
        SRP02Rendering.Render(renderContext, customCameras ,SRP02CP);
        SRPDefault.Render(renderContext, defaultCameras );
    }
}

public static class SRP02Rendering
{

    private static readonly ShaderPassName m_UnlitPassName = new ShaderPassName("SRPDefaultUnlit"); //For default shaders

    public static void Render(ScriptableRenderContext context, Camera[] cameras, SRP02CustomParameter SRP02CP)
    {
        RenderPipeline.BeginFrameRendering(cameras);

        foreach (Camera camera in cameras)
        {
            RenderPipeline.BeginCameraRendering(camera);

            //Culling
            ScriptableCullingParameters cullingParams;
            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            CullResults cull = new CullResults();
            CullResults.Cull(ref cullingParams, context, ref cull);

            context.SetupCameraProperties(camera);

            if( camera.renderingPath == RenderingPath.UsePlayerSettings )
            {
                // clear depth buffer
                CommandBuffer cmd = new CommandBuffer();
                cmd.ClearRenderTarget(true, !SRP02CP.DrawSkybox, SRP02CP.ClearColor);
                context.ExecuteCommandBuffer(cmd);
                cmd.Release();

                // Setup global lighting shader variables
                //SetupLightShaderVariables(cull.visibleLights, context);

                // Setup DrawSettings and FilterSettings
                ShaderPassName passName = new ShaderPassName("BasicPass");
                DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
                FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

                //Draw passes that has no light mode (default)
                ShaderPassName passNameDefault = new ShaderPassName("");
                DrawRendererSettings drawSettingsDefault = new DrawRendererSettings(camera, passNameDefault);
                drawSettingsDefault.SetShaderPassName(1,m_UnlitPassName);

                if(SRP02CP.DrawSkybox)
                {
                       context.DrawSkybox(camera);
                }

                if (SRP02CP.DrawOpaque)
                {
                    drawSettings.sorting.flags = SortFlags.CommonOpaque;
                    filterSettings.renderQueueRange = RenderQueueRange.opaque;
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
                }

                // Default
                drawSettingsDefault.sorting.flags = SortFlags.CommonOpaque;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDefault, filterSettings);

                if (SRP02CP.DrawTransparent)
                {
                    drawSettings.sorting.flags = SortFlags.CommonTransparent;
                    filterSettings.renderQueueRange = RenderQueueRange.transparent;
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
                }

                // Default
                drawSettingsDefault.sorting.flags = SortFlags.CommonTransparent;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDefault, filterSettings);
            }

            context.Submit();
        }
    }
}

public class SRP02CustomParameter
{
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    public SRP02CustomParameter()
    {

    }
}
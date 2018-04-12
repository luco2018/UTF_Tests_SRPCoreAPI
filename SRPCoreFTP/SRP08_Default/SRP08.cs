using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP08 : RenderPipelineAsset
{
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
        return new SRP08Instance();
    }
}

public class SRP08Instance : RenderPipeline
{
    public SRP08Instance()
    {
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRPDefault.Render(renderContext, cameras);
    }
}

public static class SRPDefault
{
    private static readonly ShaderPassName m_UnlitPassName = new ShaderPassName("SRPDefaultUnlit");

    public static void FilterCameras(Camera[] cameras, out Camera[] defaultCameras, out Camera[] customCameras)
    {
        List<Camera> customCam = new List<Camera>();
        List<Camera> defaultCam = new List<Camera>();

        foreach (Camera cam in cameras)
        {
            if(cam.renderingPath != RenderingPath.UsePlayerSettings)
            {
                defaultCam.Add(cam);
            }
            else
            {
                customCam.Add(cam);
            }
        }

        defaultCameras = defaultCam.ToArray();
        customCameras = customCam.ToArray();
    }

    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras)
    {
        foreach (Camera camera in cameras)
        {
            ScriptableCullingParameters cullingParams;
            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            CullResults cull = new CullResults();
            CullResults.Cull(ref cullingParams, context, ref cull);

            context.SetupCameraProperties(camera);

            // clear depth buffer
            CommandBuffer cmd = new CommandBuffer();
            bool drawskybox = false;
            if(camera.clearFlags == CameraClearFlags.Skybox) drawskybox = true;
            cmd.ClearRenderTarget(true, !drawskybox, camera.backgroundColor);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            // Setup global lighting shader variables
            //SetupLightShaderVariables(cull.visibleLights, context);

            // Draw skybox
            if(drawskybox) context.DrawSkybox(camera);

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            drawSettings.SetShaderPassName(1,m_UnlitPassName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            // Draw opaque objects using BasicPass shader pass
            drawSettings.sorting.flags = SortFlags.CommonOpaque;
            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

            // Draw transparent objects using BasicPass shader pass
            drawSettings.sorting.flags = SortFlags.CommonTransparent;
            filterSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

            context.Submit();
        }
    }
}
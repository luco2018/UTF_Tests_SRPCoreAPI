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
    private static ShaderPassName passNameDefault = new ShaderPassName("");
    private static ShaderPassName passNameBase = new ShaderPassName("ForwardBase");
    private static ShaderPassName passNameAdd = new ShaderPassName("ForwardAdd");

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
            CommandBuffer cmdLighting = new CommandBuffer();
            bool set = false;
            for (int i=0; i< cull.visibleLights.Count; i++)
            {
                if(!set)
                {
                    VisibleLight light = cull.visibleLights[i];
                    if (light.lightType == LightType.Directional)
                    {
                        cmdLighting.SetGlobalVector("_LightColor0", light.light.color);
                        Vector4 dir = light.localToWorld.GetColumn(2);
                        cmdLighting.SetGlobalVector("_WorldSpaceLightPos0", new Vector4(-dir.x, -dir.y, -dir.z, 0));
                        set = true;
                    }
                }
                else
                {
                    continue;
                }
            }
            context.ExecuteCommandBuffer(cmdLighting);
            cmdLighting.Release();

            // Draw skybox
            if (drawskybox) context.DrawSkybox(camera);

            // Setup DrawSettings and FilterSettings
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            DrawRendererSettings drawSettingsDefault = new DrawRendererSettings(camera, passNameDefault);
            drawSettingsDefault.SetShaderPassName(1,m_UnlitPassName);

            DrawRendererSettings drawSettingsBase = new DrawRendererSettings(camera, passNameBase);
            DrawRendererSettings drawSettingsAdd = new DrawRendererSettings(camera, passNameAdd);

            //OPAQUE
            filterSettings.renderQueueRange = RenderQueueRange.opaque;

            // Draw OPAQUE objects using DEFAULT pass
            drawSettingsDefault.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDefault, filterSettings);

            // Draw OPAQUE objects using BASE pass
            drawSettingsBase.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsBase, filterSettings);

            // Draw OPAQUE objects using ADD pass
            drawSettingsAdd.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsAdd, filterSettings);

            //TRANSPARENT
            filterSettings.renderQueueRange = RenderQueueRange.transparent;

            // Draw TRANSPARENT objects using DEFAULT pass
            drawSettingsDefault.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDefault, filterSettings);

            // Draw TRANSPARENT objects using BASE pass
            drawSettingsBase.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsBase, filterSettings);

            // Draw TRANSPARENT objects using ADD pass
            drawSettingsAdd.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsAdd, filterSettings);

            context.Submit();
        }
    }

}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.PostProcessing;

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
    private static ShaderPassName passNameShadow = new ShaderPassName("ShadowCaster");

    private static int m_ColorRTid = Shader.PropertyToID("_CameraColorRT");
    private static int m_DepthRTid = Shader.PropertyToID("_CameraDepthTexture");
    private static int m_OpaqueRTid = Shader.PropertyToID("_GrabTexture");
    private static int m_ShadowMapid = Shader.PropertyToID("_ShadowMapTexture");
    private static RenderTargetIdentifier m_CurrCameraColorRT;
    private static RenderTargetIdentifier m_ColorRT = new RenderTargetIdentifier(m_ColorRTid);
    private static RenderTargetIdentifier m_DepthRT = new RenderTargetIdentifier(m_DepthRTid);

    private static RenderTextureFormat m_ColorFormat = RenderTextureFormat.DefaultHDR;
    private static PostProcessRenderContext m_PostProcessRenderContext = new PostProcessRenderContext();
    private static PostProcessLayer m_CameraPostProcessLayer;

    private static RendererConfiguration renderConfig = RendererConfiguration.PerObjectReflectionProbes | 
                                                        RendererConfiguration.PerObjectLightmaps;

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

    private static void ClearFlag(CommandBuffer cmd, Camera cam)
    {
        bool clearcolor = true;
        bool cleardepth = true;
        if( cam.clearFlags == CameraClearFlags.Skybox || cam.clearFlags == CameraClearFlags.Depth ) {clearcolor = false;}
        cmd.ClearRenderTarget(cleardepth, clearcolor, cam.backgroundColor);
    }

    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras)
    {

        //************************** SetRenderingFeatures ****************************************
        #if UNITY_EDITOR
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures()
            {
                reflectionProbeSupportFlags = SupportedRenderingFeatures.ReflectionProbeSupportFlags.None,
                defaultMixedLightingMode = SupportedRenderingFeatures.LightmapMixedBakeMode.Subtractive,
                supportedMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeMode.Subtractive,
                supportedLightmapBakeTypes = LightmapBakeType.Baked | LightmapBakeType.Mixed,
                supportedLightmapsModes = LightmapsMode.CombinedDirectional | LightmapsMode.NonDirectional,
                rendererSupportsLightProbeProxyVolumes = false,
                rendererSupportsMotionVectors = false,
                rendererSupportsReceiveShadows = true,
                rendererSupportsReflectionProbes = true
            };
            SceneViewDrawMode.SetupDrawMode();
        #endif

        // //////////////////////////////////////////////////////////////////////////////////
        foreach (Camera camera in cameras)
        {
            //************************** Culling ****************************************
            ScriptableCullingParameters cullingParams;
            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            CullResults cull = new CullResults();
            CullResults.Cull(ref cullingParams, context, ref cull);

            //************************** Cam Properties **********************************
            context.SetupCameraProperties(camera);

            //************************** Clear Flags  ************************************
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "Clear Flag";
            ClearFlag(cmd, camera);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            //************************** Lighting Variables  *****************************
            CommandBuffer cmdLighting = new CommandBuffer();
            cmdLighting.name = "Lighting variable";
            int additionalLightSet = 0;
            int mainLightIndex = -1;
            for (int i=0; i< cull.visibleLights.Count; i++)
            {
                VisibleLight light = cull.visibleLights[i];

                if(mainLightIndex == -1) //Directional light
                {
                    if (light.lightType == LightType.Directional)
                    {
                        cmdLighting.SetGlobalVector("_LightColor0", light.light.color);
                        Vector4 dir = light.localToWorld.GetColumn(2);
                        cmdLighting.SetGlobalVector("_WorldSpaceLightPos0", new Vector4(-dir.x, -dir.y, -dir.z, 0));
                        mainLightIndex = i;
                    }
                }
                else
                {
                    additionalLightSet++;
                    continue;//so far just do only 1 directional light
                }
            }
            context.ExecuteCommandBuffer(cmdLighting);
            cmdLighting.Release();

            //************************** Draw Settings  ************************************
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            DrawRendererSettings drawSettingsDefault = new DrawRendererSettings(camera, passNameDefault);
                drawSettingsDefault.rendererConfiguration = renderConfig;
                drawSettingsDefault.SetShaderPassName(5,m_UnlitPassName);

            DrawRendererSettings drawSettingsBase = new DrawRendererSettings(camera, passNameBase);
                drawSettingsBase.rendererConfiguration = renderConfig;
            DrawRendererSettings drawSettingsAdd = new DrawRendererSettings(camera, passNameAdd);
                drawSettingsAdd.rendererConfiguration = renderConfig;
            DrawRendererSettings drawSettingsShadow = new DrawRendererSettings(camera, passNameShadow);
            //DrawRendererSettings drawSettingsMeta = new DrawRendererSettings(camera, passNameMeta);

            //************************** Skybox ************************************
            if( camera.clearFlags == CameraClearFlags.Skybox) context.DrawSkybox(camera);

            //************************** Opaque ************************************
            // Opaque
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

            //************************** Transparent ************************************
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
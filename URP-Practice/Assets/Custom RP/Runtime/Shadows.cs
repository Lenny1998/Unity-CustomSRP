using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    private const string bufferName = "Shadows";

    private CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    private ScriptableRenderContext context;

    private CullingResults cullingResults;

    private ShadowSettings settings;

    private const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

    private struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    private static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };

    private ShadowedDirectionalLight[] shadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    private int ShadowedDirectionalLightCount;

    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    private static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades];

    private static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cullingResults"></param>
    /// <param name="settings"></param>
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;

        ShadowedDirectionalLightCount = 0;
    }

    private void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount && light.shadows != LightShadows.None &&
            light.shadowStrength > 0f && cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            shadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight()
            {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
            return new Vector3(light.shadowStrength,
                settings.directional.cascadeCount * ShadowedDirectionalLightCount++, light.shadowNormalBias);
        }

        return Vector3.zero;
    }

    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }

    private void RenderDirectionalShadows()
    {
        int atlasSize = (int) settings.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear,
            RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);

        //渲染完所有阴影光后，通过调用缓冲区上的SetGlobalMatrixArray将矩阵发给GPU。
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);

        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId,
            new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f)));
        SetKeywords();
        buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }


    private void SetKeywords()
    {
        int enabledIndex = (int) settings.directional.filter - 1;
        for (int i = 0; i < directionalFilterKeywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(directionalFilterKeywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(directionalFilterKeywords[i]);
            }
        }
    }

    private void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i,
                cascadeCount, ratios, tileSize,
                light.nearPlaneOffset, out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
            shadowSettings.splitData = splitData;
            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }

            int tileIndex = tileOffset + i;

            //灯光的投影矩阵和RenderDirectionalShadows中的视图矩阵相乘，创建从世界空间到灯光空间的转换矩阵
            dirShadowMatrices[tileIndex] =
                ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    private void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }

    public void Cleanup()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(dirShadowAtlasId);
            ExecuteBuffer();
        }
    }

    private Vector2 SetTileViewport(int index, int split, float titleSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * titleSize, offset.y * titleSize, titleSize, titleSize));
        return offset;
    }

    /// <summary>
    /// 获取一个从世界空间转换为阴影图块空间的矩阵
    /// </summary>
    /// <param name="m"></param>
    /// <param name="offset"></param>
    /// <param name="split"></param>
    /// <returns></returns>
    private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        return m;
    }
}
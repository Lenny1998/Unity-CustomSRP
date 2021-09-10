using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 用于渲染单个摄像机
/// </summary>
public class CameraRenderer
{
    private ScriptableRenderContext context;
    private Camera camera;

    private const string bufferName = "Render Camera";

    private readonly CommandBuffer buffer = new CommandBuffer {name = bufferName};

    private CullingResults cullingResults;

    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;

        if (!Cull())
        {
            return;
        }
        
        Setup();
        DrawVisibleGeometry();
        Submit();
    }

    private void Setup()
    {
        context.SetupCameraProperties(camera);
        
        //之前已经画过得东西仍然存在，可能会干扰现在渲染的图像。为了保证正常渲染，必须清除渲染目标，消除旧的内容
        buffer.ClearRenderTarget(true, true, Color.clear);
        
        buffer.BeginSample(bufferName);
        
        ExcuteBuffer();
    }

    /// <summary>
    /// 向上下文发出的命令都是缓冲的，所以要做提交处理
    /// </summary>
    private void Submit()
    {
        buffer.EndSample(bufferName);
        ExcuteBuffer();
        context.Submit();
    }

    private void DrawVisibleGeometry()
    {
        context.DrawSkybox(camera);
    }

    private void ExcuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private bool Cull()
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            cullingResults = context.Cull(ref p);
            return true;
        }

        return false;
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer = new CameraRenderer();

    public CustomRenderPipeline()
    {
        //启用SRP批处理
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
    }

    /// <summary>
    /// Render方法 Unity每一帧都会调用
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cameras"></param>
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (var camera in cameras)
        {
            renderer.Render(context, camera);
        }
    }
}
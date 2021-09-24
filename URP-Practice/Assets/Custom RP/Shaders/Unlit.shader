Shader "Custom RP/Unlit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (1.0,1.0,1.0,1.0)
        _Cutoff("Alpha Cutoff", Range(0.0,1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0

        //不透明渲染和透明渲染之间的主要区别是，是替换之前绘制的任何内容还是与之前的结果结合以产生透视效果。
        //可以通过设置源和目标混合模式来控制。这里的源是指现在绘制的内容，目标是先前绘制的内容
        //默认值表示我们已经使用的不透明混合配置。源设置为1，表示完全添加，而目标设置为零，表示忽略。
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0

        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1
    }

    SubShader
    {
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma shader_feature _CLIPPING
            #pragma multi_compile_instancing
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            #include "UnlitPass.hlsl"
            ENDHLSL
        }
    }

}
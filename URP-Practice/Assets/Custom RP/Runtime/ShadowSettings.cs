using UnityEngine;

[System.Serializable]
public class ShadowSettings
{
    [Min(0.001f)]
    public float maxDistance = 100f;

    [Range(0.001f, 1f)]
    public float distanceFade = 0.1f;

    public enum TextrueSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096,
        _8192 = 8192
    }

    [System.Serializable]
    public struct Directional
    {
        public TextrueSize atlasSize;

        [Range(1, 4)]
        public int cascadeCount; //级联

        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;

        public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);

        [Range(0.001f, 1f)]
        public float cascadeFade;
    }

    public Directional directional = new Directional()
    {
        atlasSize = TextrueSize._1024,
        cascadeFade = 0.1f
    };
}
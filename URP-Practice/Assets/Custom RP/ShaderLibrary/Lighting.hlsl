#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight(Surface surface, Light light)
{
    //当点积为负时，将其限制为零，通过saturate函数来实现。
    return saturate(dot(surface.normal, light.direction)) * light.color;
}

float3 GetLighting(Surface surface, Light light)
{
    return IncomingLight(surface, light) * surface.color;
}

float3 GetLighting(Surface surface)
{
    return GetLighting(surface, GetDirectionLight());
}

#endif

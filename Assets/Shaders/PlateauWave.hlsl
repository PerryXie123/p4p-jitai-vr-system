#ifndef PLATEAU_WAVE_INCLUDED
#define PLATEAU_WAVE_INCLUDED

void PlateauWave_float(float In, out float Out, out float3 PhaseColor)
{
    const float RiseSeconds = 4.0;
    const float HighPlateauSeconds = 4.0;
    const float FallSeconds = 4.0;
    const float LowPlateauSeconds = 4.0;
    const float ColorTransitionSeconds = 0.35;
    const float3 InhaleColor = float3(0.0, 0.65, 1.0);
    const float3 FullHoldColor = float3(0.0, 1.0, 0.0);
    const float3 ExhaleColor = float3(0.7, 0.0, 1.0);
    const float3 EmptyHoldColor = float3(1.0, 0.0, 0.0);

    float cycleSeconds = LowPlateauSeconds + RiseSeconds + HighPlateauSeconds + FallSeconds;
    float cycleTime = fmod(In, cycleSeconds);

    if (cycleTime < RiseSeconds)
    {
        float t = cycleTime / RiseSeconds;
        Out = t;
        float colorT = smoothstep(0.0, ColorTransitionSeconds, cycleTime);
        PhaseColor = lerp(EmptyHoldColor, InhaleColor, colorT);
    }
    else if (cycleTime < RiseSeconds + HighPlateauSeconds)
    {
        Out = 1.0;
        float colorT = smoothstep(0.0, ColorTransitionSeconds, cycleTime - RiseSeconds);
        PhaseColor = lerp(InhaleColor, FullHoldColor, colorT);
    }
    else if (cycleTime < RiseSeconds + HighPlateauSeconds + FallSeconds)
    {
        float t = (cycleTime - RiseSeconds - HighPlateauSeconds) / FallSeconds;
        Out = 1.0 - t;
        float colorT = smoothstep(0.0, ColorTransitionSeconds, cycleTime - RiseSeconds - HighPlateauSeconds);
        PhaseColor = lerp(FullHoldColor, ExhaleColor, colorT);
    }
    else
    {
        Out = 0.0;
        float colorT = smoothstep(0.0, ColorTransitionSeconds, cycleTime - RiseSeconds - HighPlateauSeconds - FallSeconds);
        PhaseColor = lerp(ExhaleColor, EmptyHoldColor, colorT);
    }
}

void PlateauWave_half(half In, out half Out, out half3 PhaseColor)
{
    const half RiseSeconds = 4.0;
    const half HighPlateauSeconds = 4.0;
    const half FallSeconds = 4.0;
    const half LowPlateauSeconds = 4.0;
    const half ColorTransitionSeconds = 0.35;
    const half3 InhaleColor = half3(0.0, 0.65, 1.0);
    const half3 FullHoldColor = half3(0.0, 1.0, 0.0);
    const half3 ExhaleColor = half3(0.7, 0.0, 1.0);
    const half3 EmptyHoldColor = half3(1.0, 0.0, 0.0);

    half cycleSeconds = LowPlateauSeconds + RiseSeconds + HighPlateauSeconds + FallSeconds;
    half cycleTime = fmod(In, cycleSeconds);

    if (cycleTime < RiseSeconds)
    {
        half t = cycleTime / RiseSeconds;
        Out = t;
        half colorT = smoothstep(0.0, ColorTransitionSeconds, cycleTime);
        PhaseColor = lerp(EmptyHoldColor, InhaleColor, colorT);
    }
    else if (cycleTime < RiseSeconds + HighPlateauSeconds)
    {
        Out = 1.0;
        half colorT = smoothstep(0.0, ColorTransitionSeconds, cycleTime - RiseSeconds);
        PhaseColor = lerp(InhaleColor, FullHoldColor, colorT);
    }
    else if (cycleTime < RiseSeconds + HighPlateauSeconds + FallSeconds)
    {
        half t = (cycleTime - RiseSeconds - HighPlateauSeconds) / FallSeconds;
        Out = 1.0 - t;
        half colorT = smoothstep(0.0, ColorTransitionSeconds, cycleTime - RiseSeconds - HighPlateauSeconds);
        PhaseColor = lerp(FullHoldColor, ExhaleColor, colorT);
    }
    else
    {
        Out = 0.0;
        half colorT = smoothstep(0.0, ColorTransitionSeconds, cycleTime - RiseSeconds - HighPlateauSeconds - FallSeconds);
        PhaseColor = lerp(ExhaleColor, EmptyHoldColor, colorT);
    }
}

#endif

#ifndef PLATEAU_WAVE_INCLUDED
#define PLATEAU_WAVE_INCLUDED

void PlateauWave_float(float In, out float Out)
{
    const float LowPlateauSeconds = 4.0;
    const float RiseSeconds = 4.0;
    const float HighPlateauSeconds = 4.0;
    const float FallSeconds = 4.0;

    float cycleSeconds = LowPlateauSeconds + RiseSeconds + HighPlateauSeconds + FallSeconds;
    float cycleTime = fmod(In, cycleSeconds);

    if (cycleTime < LowPlateauSeconds)
    {
        Out = 0.0;
    }
    else if (cycleTime < LowPlateauSeconds + RiseSeconds)
    {
        float t = (cycleTime - LowPlateauSeconds) / RiseSeconds;
        Out = smoothstep(0.0, 1.0, t);
    }
    else if (cycleTime < LowPlateauSeconds + RiseSeconds + HighPlateauSeconds)
    {
        Out = 1.0;
    }
    else
    {
        float t = (cycleTime - LowPlateauSeconds - RiseSeconds - HighPlateauSeconds) / FallSeconds;
        Out = 1.0 - smoothstep(0.0, 1.0, t);
    }
}

void PlateauWave_half(half In, out half Out)
{
    const half LowPlateauSeconds = 4.0;
    const half RiseSeconds = 4.0;
    const half HighPlateauSeconds = 4.0;
    const half FallSeconds = 4.0;

    half cycleSeconds = LowPlateauSeconds + RiseSeconds + HighPlateauSeconds + FallSeconds;
    half cycleTime = fmod(In, cycleSeconds);

    if (cycleTime < LowPlateauSeconds)
    {
        Out = 0.0;
    }
    else if (cycleTime < LowPlateauSeconds + RiseSeconds)
    {
        half t = (cycleTime - LowPlateauSeconds) / RiseSeconds;
        Out = smoothstep(0.0, 1.0, t);
    }
    else if (cycleTime < LowPlateauSeconds + RiseSeconds + HighPlateauSeconds)
    {
        Out = 1.0;
    }
    else
    {
        half t = (cycleTime - LowPlateauSeconds - RiseSeconds - HighPlateauSeconds) / FallSeconds;
        Out = 1.0 - smoothstep(0.0, 1.0, t);
    }
}

#endif

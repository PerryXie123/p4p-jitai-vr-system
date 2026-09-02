#ifndef PLATEAU_WAVE_INCLUDED
#define PLATEAU_WAVE_INCLUDED

void PlateauWave_float(float In, out float Out)
{
    const float RiseSeconds = 4.0;
    const float HighPlateauSeconds = 4.0;
    const float FallSeconds = 4.0;
    const float LowPlateauSeconds = 4.0;

    float cycleSeconds = LowPlateauSeconds + RiseSeconds + HighPlateauSeconds + FallSeconds;
    float cycleTime = fmod(In, cycleSeconds);

    if (cycleTime < RiseSeconds)
    {
        float t = cycleTime / RiseSeconds;
        Out = t;
    }
    else if (cycleTime < RiseSeconds + HighPlateauSeconds)
    {
        Out = 1.0;
    }
    else if (cycleTime < RiseSeconds + HighPlateauSeconds + FallSeconds)
    {
        float t = (cycleTime - RiseSeconds - HighPlateauSeconds) / FallSeconds;
        Out = 1.0 - t;
    }
    else
    {
        Out = 0.0;
    }
}

void PlateauWave_half(half In, out half Out)
{
    const half RiseSeconds = 4.0;
    const half HighPlateauSeconds = 4.0;
    const half FallSeconds = 4.0;
    const half LowPlateauSeconds = 4.0;

    half cycleSeconds = LowPlateauSeconds + RiseSeconds + HighPlateauSeconds + FallSeconds;
    half cycleTime = fmod(In, cycleSeconds);

    if (cycleTime < RiseSeconds)
    {
        half t = cycleTime / RiseSeconds;
        Out = t;
    }
    else if (cycleTime < RiseSeconds + HighPlateauSeconds)
    {
        Out = 1.0;
    }
    else if (cycleTime < RiseSeconds + HighPlateauSeconds + FallSeconds)
    {
        half t = (cycleTime - RiseSeconds - HighPlateauSeconds) / FallSeconds;
        Out = 1.0 - t;
    }
    else
    {
        Out = 0.0;
    }
}

#endif

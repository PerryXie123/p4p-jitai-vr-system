using System;

namespace Assets.Scripts.SignalProcessing
{
    [Serializable]
    internal class VitalSnapshot
    {
        public float HeartRate;
        public float MeanNN;
        public float SDNN;
        public float RMSSD;
        public float PNN50;

        public string PrintVitals()
        {
            return $"HeartRate: {HeartRate}, MeanNN: {MeanNN}, SDNN: {SDNN}, RMSSD: {RMSSD}, PNN50: {PNN50}";
        }
    }
}

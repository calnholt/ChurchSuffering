using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Events
{
    public class JigglePulseConfig
    {
        public float PulseDurationSeconds { get; set; } = 0.5f;
        public float PulseScaleAmplitude { get; set; } = 0.2f;
        public float JiggleDegrees { get; set; } = 5f;
        public float PulseFrequencyHz { get; set; } = 1.7f;

        public static JigglePulseConfig Default => new JigglePulseConfig();
    }

    public class JigglePulseEvent
    {
        public Entity Target { get; set; }
        public JigglePulseConfig Config { get; set; } = JigglePulseConfig.Default;
    }
}







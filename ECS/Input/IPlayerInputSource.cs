using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Input
{
    public interface IPlayerInputSource
    {
        PlayerInputFrame Capture(
            bool isWindowActive,
            Rectangle renderDestination,
            int virtualWidth,
            int virtualHeight);

        void SetRumbleChannel(string channelId, float lowFrequency, float highFrequency);
        void ClearRumbleChannel(string channelId);
        void PlayRumblePulse(
            float lowFrequency,
            float highFrequency,
            float durationSeconds,
            RumbleGroup group = RumbleGroup.Default);
        void ClearRumbleGroup(RumbleGroup group);
        void TickRumble(float deltaSeconds);
    }
}

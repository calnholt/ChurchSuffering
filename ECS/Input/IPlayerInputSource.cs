using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Input
{
    public interface IPlayerInputSource
    {
        PlayerInputFrame Capture(
            bool isWindowActive,
            Rectangle renderDestination,
            int virtualWidth,
            int virtualHeight);

        void SetRumbleChannel(string channelId, RumbleMotorState motors);
        void ClearRumbleChannel(string channelId);
        void PlayRumblePattern(RumblePattern pattern, RumbleGroup group = RumbleGroup.Default);
        void ClearRumbleGroup(RumbleGroup group);
		void ClearAllRumble();
		void SetRumbleEnabled(bool enabled);
        void TickRumble(float deltaSeconds);
    }
}

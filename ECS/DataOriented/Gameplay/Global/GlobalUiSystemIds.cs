#nullable enable

using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Global;

public static class GlobalUiSystemIds
{
    public static readonly SystemId PlayerInput = new(4001);
    public static readonly SystemId ModalInputSuppression = new(4002);
    public static readonly SystemId UIInteraction = new(4003);
    public static readonly SystemId PauseMenuInput = new(4004);
    public static readonly SystemId SceneLifecycle = new(4010);
    public static readonly SystemId SceneLoadingCoordinator = new(4011);
    public static readonly SystemId EventQueue = new(4020);
    public static readonly SystemId TimerScheduler = new(4021);
    public static readonly SystemId HighlightSettings = new(4030);
    public static readonly SystemId HotKey = new(4031);
    public static readonly SystemId HotKeyProgressRing = new(4032);
    public static readonly SystemId MusicManager = new(4033);
    public static readonly SystemId SoundEffectManager = new(4034);
    public static readonly SystemId UIElementHighlight = new(4035);
}

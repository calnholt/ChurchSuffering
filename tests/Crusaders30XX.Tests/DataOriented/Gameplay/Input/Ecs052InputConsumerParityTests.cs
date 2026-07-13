#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Input;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Storage;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Input;

public sealed class Ecs052InputConsumerParityTests
{
    [Fact]
    public void Pause_uses_keyboard_escape_and_gamepad_start_but_not_back()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        var system = new PauseMenuInputSystem(world);
        EventRuntime events = Attach(world);
        var commands = new CommandBuffer();

        SetInput(world, PlayerInputDevice.KeyboardMouse, PlayerInputButton.Escape);
        Run(system, world, events, commands);
        Assert.True(IsPauseVisible(world, system.StateEntity));

        SetInput(world, PlayerInputDevice.Gamepad, PlayerInputButton.Back);
        Run(system, world, events, commands);
        Assert.True(IsPauseVisible(world, system.StateEntity));

        SetInput(world, PlayerInputDevice.Gamepad, PlayerInputButton.Start);
        Run(system, world, events, commands);
        Assert.False(IsPauseVisible(world, system.StateEntity));
    }

    [Fact]
    public void Pile_visibility_matches_guided_tutorial_sections()
    {
        World world = CreateWorld();
        Assert.True(BattlePileInputRules.IsDrawPileVisible(world));
        Assert.True(BattlePileInputRules.IsDiscardPileVisible(world));

        EntityId tutorial = GuidedTutorialCatalog.Materialize(world, 1);
        Assert.False(BattlePileInputRules.IsDrawPileVisible(world));
        Assert.False(BattlePileInputRules.IsDiscardPileVisible(world));

        ref GuidedTutorial state = ref world.Get<GuidedTutorial>(tutorial);
        state.Section = 8;
        Assert.True(BattlePileInputRules.IsDrawPileVisible(world));
        Assert.False(BattlePileInputRules.IsDiscardPileVisible(world));

        state.State = TutorialState.Complete;
        Assert.True(BattlePileInputRules.IsDrawPileVisible(world));
        Assert.True(BattlePileInputRules.IsDiscardPileVisible(world));
    }

    [Fact]
    public void Shoulder_buttons_open_close_and_switch_display_only_piles()
    {
        CardPileFixture fixture = CreateCardPileFixture();

        fixture.Press(PlayerInputButton.RightShoulder);
        CardListModal modal = fixture.ModalState;
        Assert.Equal((byte)1, modal.IsOpen);
        Assert.Equal(CardZone.DrawPile, modal.SourceZone);
        Assert.Equal(BattlePileInputRules.DisplayOnlyContext, modal.Context);
        Assert.Equal(2, fixture.ModalCards.Count);

        fixture.Press(PlayerInputButton.LeftShoulder);
        modal = fixture.ModalState;
        Assert.Equal((byte)1, modal.IsOpen);
        Assert.Equal(CardZone.DiscardPile, modal.SourceZone);
        Assert.Single(fixture.ModalCards.AsReadOnlySpan().ToArray());

        fixture.Press(PlayerInputButton.LeftShoulder);
        Assert.Equal((byte)0, fixture.ModalState.IsOpen);
    }

    [Fact]
    public void Shoulder_buttons_ignore_unrelated_modal_and_frozen_battle()
    {
        CardPileFixture fixture = CreateCardPileFixture();
        ref CardListModal modal = ref fixture.World.Get<CardListModal>(fixture.Modal);
        modal.IsOpen = 1;
        modal.SourceZone = CardZone.Hand;

        fixture.Press(PlayerInputButton.RightShoulder);
        Assert.Equal(CardZone.Hand, fixture.ModalState.SourceZone);

        modal.IsOpen = 0;
        EntityId enemy = fixture.World.Create(default);
        fixture.World.Add(enemy, new HP { Current = 0, Max = 20 });
        EntityId battle = fixture.World.Create(default);
        fixture.World.Add(battle, new BattleInfo { Enemy = enemy });
        fixture.World.Add(battle, new BattleStateInfo());
        fixture.World.Add(battle, new PhaseState { Current = CombatPhase.Block });

        fixture.Press(PlayerInputButton.RightShoulder);
        Assert.Equal((byte)0, fixture.ModalState.IsOpen);
    }

    [Fact]
    public void Primary_and_secondary_card_actions_route_only_for_hand_and_phase()
    {
        var assignments = new RecordingConsumer<AssignCardAsBlockRequested>();
        var plays = new RecordingConsumer<PlayCardRequested>();
        var pledges = new RecordingConsumer<PledgeCardRequested>();
        InputRouteFixture fixture = CreateInputRouteFixture(new MetaGameRouteConsumers()
            .Add(assignments)
            .Add(plays)
            .Add(pledges));

        fixture.SetPhase(CombatPhase.Block);
        fixture.Action(fixture.HandCard, UIElementEventType.CardClicked);
        Assert.Equal(1, assignments.Count);
        Assert.Equal(fixture.HandCard, assignments.Last.Card);
        Assert.Equal(0, plays.Count);

        fixture.SetPhase(CombatPhase.Action);
        fixture.Action(fixture.HandCard, UIElementEventType.CardClicked);
        fixture.Action(fixture.HandCard, UIElementEventType.PledgeCard);
        Assert.Equal(1, assignments.Count);
        Assert.Equal(1, plays.Count);
        Assert.Equal(fixture.Player, plays.Last.Player);
        Assert.Equal(1, pledges.Count);

        fixture.Action(fixture.DrawCard, UIElementEventType.CardClicked);
        fixture.Action(fixture.DrawCard, UIElementEventType.PledgeCard);
        Assert.Equal(1, plays.Count);
        Assert.Equal(1, pledges.Count);
    }

    [Fact]
    public void Booster_close_is_swallowed_until_all_rewards_are_revealed()
    {
        InputRouteFixture fixture = CreateInputRouteFixture();
        EntityId overlay = fixture.World.Create(default);
        DynamicBufferHandle<BoosterRewardEntry> rewards =
            fixture.World.CreateDynamicBuffer<BoosterRewardEntry>(overlay, 2);
        fixture.World.GetDynamicBuffer(rewards).Add(default);
        fixture.World.GetDynamicBuffer(rewards).Add(default);
        fixture.World.Add(overlay, new BoosterPackOpeningOverlayState
        {
            Rewards = rewards,
            RevealedCount = 1,
            Modal = MetaModalKind.BoosterPack,
            Open = 1,
        });

        fixture.Action(overlay, UIElementEventType.BoosterPackOpeningClose);
        Assert.Equal((byte)1, fixture.World.Get<BoosterPackOpeningOverlayState>(overlay).Open);

        fixture.World.Get<BoosterPackOpeningOverlayState>(overlay).RevealedCount = 2;
        fixture.Action(overlay, UIElementEventType.BoosterPackOpeningClose);
        Assert.Equal((byte)0, fixture.World.Get<BoosterPackOpeningOverlayState>(overlay).Open);
    }

    [Fact]
    public void Skip_tutorial_action_resolves_the_running_tutorial_from_a_child_control()
    {
        InputRouteFixture fixture = CreateInputRouteFixture();
        EntityId tutorial = GuidedTutorialCatalog.Materialize(fixture.World, 3);
        fixture.MetaEvents.TutorialStarted.Publish(new(tutorial, 3));
        fixture.Events.DrainBarrier();
        EntityId child = fixture.World.Create(default);

        fixture.Action(child, UIElementEventType.SkipTutorial);

        Assert.Equal(TutorialState.Skipped, fixture.World.Get<GuidedTutorial>(tutorial).State);
    }

    private static CardPileFixture CreateCardPileFixture()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world, SceneGroup.Battle);
        var events = new CardGameplayEventHub();
        CardGameplayComposition composition = CardGameplayComposition.Create(world, events);
        EventRuntime runtime = new(new EventRoutingEndpoint(composition.GetRoutes()));
        world.AttachEventRuntime(runtime);
        EntityId owner = world.Create(default);
        EntityId deck = CardGameplayFactory.CreateDeck(world, owner, 52);
        _ = CardGameplayFactory.CreateCard(world, deck, CardId.Hammer, CardZone.DrawPile);
        _ = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.DrawPile);
        _ = CardGameplayFactory.CreateCard(world, deck, CardId.Mantlet, CardZone.DiscardPile);
        BattlePileInputSystem pile = Assert.IsType<BattlePileInputSystem>(composition.Systems[1]);
        CardListModalSystem cardList = Assert.IsType<CardListModalSystem>(composition.CompatibilitySystems[23]);
        return new CardPileFixture(world, runtime, pile, cardList.Modal);
    }

    private static InputRouteFixture CreateInputRouteFixture(MetaGameRouteConsumers? recorders = null)
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world, SceneGroup.Battle);
        var metaEvents = new MetaGameEventHub();
        MetaGameComposition meta = MetaGameComposition.Create(world, metaEvents, recorders);
        var globalEvents = new GlobalUiEventHub();
        IEventRoute[] globalRoutes = globalEvents.BuildRoutes(world, meta.CrossDomainRoutes.RegisterGlobal());
        IEventRoute[] routes = new IEventRoute[globalRoutes.Length + meta.Routes.Length];
        globalRoutes.CopyTo(routes, 0);
        meta.Routes.CopyTo(routes.AsSpan(globalRoutes.Length));
        EventRuntime runtime = new(new EventRoutingEndpoint(routes));
        world.AttachEventRuntime(runtime);

        EntityId player = world.Create(default);
        EntityId deck = CardGameplayFactory.CreateDeck(world, player, 73);
        EntityId hand = CardGameplayFactory.CreateCard(world, deck, CardId.Hammer, CardZone.Hand);
        EntityId draw = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.DrawPile);
        EntityId battle = world.Create(default);
        world.Add(battle, new BattleInfo { Player = player, Deck = deck });
        world.Add(battle, new PhaseState { Current = CombatPhase.Action });
        return new InputRouteFixture(world, runtime, globalEvents, metaEvents, battle, player, hand, draw);
    }

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());

    private static EventRuntime Attach(World world)
    {
        var events = new EventRuntime(new EventRoutingEndpoint());
        world.AttachEventRuntime(events);
        return events;
    }

    private static void SetInput(World world, PlayerInputDevice device, PlayerInputButton button)
    {
        ulong pressed = PlayerInputFrame.Mask(button);
        ref PlayerInputState input = ref world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>());
        input.Frame = new PlayerInputFrame(
            1, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, 0f, 0f, 0f,
            pressed, pressed, 0, device, true, device, device == PlayerInputDevice.Gamepad);
    }

    private static void Run(
        IGameSystem system,
        World world,
        EventRuntime events,
        CommandBuffer commands)
    {
        var context = new SystemContext(world, commands, events, 0, TimeSpan.Zero, SceneGroup.Battle);
        system.Update(ref context);
        commands.Playback(world);
    }

    private static bool IsPauseVisible(World world, EntityId state) =>
        (world.Get<PauseMenuOverlay>(state).Flags & PresentationFlags.Visible) != 0;

    private sealed class CardPileFixture
    {
        private readonly CommandBuffer commands = new();
        public CardPileFixture(World world, EventRuntime events, BattlePileInputSystem pile, EntityId modal)
        {
            World = world;
            Events = events;
            Pile = pile;
            Modal = modal;
        }
        public World World { get; }
        public EventRuntime Events { get; }
        public BattlePileInputSystem Pile { get; }
        public EntityId Modal { get; }
        public CardListModal ModalState => World.Get<CardListModal>(Modal);
        public DynamicBuffer<ModalCardEntry> ModalCards => World.GetDynamicBuffer(ModalState.Cards);
        public void Press(PlayerInputButton button)
        {
            SetInput(World, PlayerInputDevice.Gamepad, button);
            Run(Pile, World, Events, commands);
            Events.DrainBarrier();
        }
    }

    private sealed class InputRouteFixture
    {
        public InputRouteFixture(
            World world,
            EventRuntime events,
            GlobalUiEventHub globalEvents,
            MetaGameEventHub metaEvents,
            EntityId battle,
            EntityId player,
            EntityId handCard,
            EntityId drawCard)
        {
            World = world;
            Events = events;
            GlobalEvents = globalEvents;
            MetaEvents = metaEvents;
            Battle = battle;
            Player = player;
            HandCard = handCard;
            DrawCard = drawCard;
        }
        public World World { get; }
        public EventRuntime Events { get; }
        public GlobalUiEventHub GlobalEvents { get; }
        public MetaGameEventHub MetaEvents { get; }
        public EntityId Battle { get; }
        public EntityId Player { get; }
        public EntityId HandCard { get; }
        public EntityId DrawCard { get; }
        public void SetPhase(CombatPhase phase) => World.Get<PhaseState>(Battle).Current = phase;
        public void Action(EntityId entity, UIElementEventType action)
        {
            GlobalEvents.UiAction.Publish(new(entity, action, PlayerInputDevice.Gamepad));
            Events.DrainBarrier();
        }
    }

    private sealed class RecordingConsumer<T> : IEventConsumer<T> where T : unmanaged
    {
        public int Count { get; private set; }
        public T Last { get; private set; }
        public void Consume(in T value, ref EventDispatchContext context)
        {
            Count++;
            Last = value;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Centralized sound effect manager. Listens for PlaySfxEvent
    /// and plays the corresponding SoundEffect. Supports multiple simultaneous instances.
    /// </summary>
    [DebugTab("Sound Effects")]
    public class SoundEffectManagerSystem : Core.System
    {
        private readonly ContentManager _content;
        private readonly Dictionary<SfxTrack, SoundEffect> _soundCache = new();
        private readonly List<SoundEffectInstance> _activeInstances = new();
        private readonly Dictionary<SfxTrack, SoundEffectInstance> _loopingInstances = new();
        private int _sfxVolumeLevel;
        [DebugEditable(DisplayName = "Mute")]
        public bool Mute { get; set; } = false;

        public SoundEffectManagerSystem(EntityManager entityManager, ContentManager content) : base(entityManager)
        {
            _content = content;
            _sfxVolumeLevel = SaveCache.GetSfxVolumeLevel();
            EventManager.Subscribe<PlaySfxEvent>(OnPlaySfx);
            EventManager.Subscribe<StopSfxEvent>(OnStopSfx);
            EventManager.Subscribe<AudioSettingsChangedEvent>(OnAudioSettingsChanged);
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return Enumerable.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Mute)
            {
                StopAllLoopingInstances();
            }

            // Clean up stopped instances
            for (int i = _activeInstances.Count - 1; i >= 0; i--)
            {
                var instance = _activeInstances[i];
                if (instance.State == SoundState.Stopped)
                {
                    instance.Dispose();
                    _activeInstances.RemoveAt(i);
                }
            }
        }

        private void OnPlaySfx(PlaySfxEvent evt)
        {
            if (Mute) return;
            if (evt == null) return;

            try
            {
                var track = evt.Track;
                if (track == SfxTrack.None) return;

                var soundEffect = ResolveSoundEffect(track);
                if (soundEffect == null) return;

                if (evt.Loop)
                {
                    PlayLooping(track, soundEffect, evt);
                    return;
                }

                // Debug: log music volume before SFX
                float musicVolBefore = MediaPlayer.Volume;

                // Create and configure instance
                var instance = soundEffect.CreateInstance();
                instance.Volume = ApplyUserVolume(evt.Volume);
                instance.Pitch = MathHelper.Clamp(evt.Pitch, -1f, 1f);
                instance.Pan = MathHelper.Clamp(evt.Pan, -1f, 1f);

                instance.Play();
                _activeInstances.Add(instance);

                // Debug: log music volume after SFX
                float musicVolAfter = MediaPlayer.Volume;
                if (Math.Abs(musicVolBefore - musicVolAfter) > 0.001f)
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: Music volume changed during SFX play: {musicVolBefore} -> {musicVolAfter}");
                }
            }
            catch { }
        }

        private void PlayLooping(SfxTrack track, SoundEffect soundEffect, PlaySfxEvent evt)
        {
            StopLoopingInstance(track);

            var instance = soundEffect.CreateInstance();
            instance.IsLooped = true;
            instance.Volume = ApplyUserVolume(evt.Volume);
            instance.Pitch = MathHelper.Clamp(evt.Pitch, -1f, 1f);
            instance.Pan = MathHelper.Clamp(evt.Pan, -1f, 1f);
            instance.Play();
            _loopingInstances[track] = instance;
        }

        private void OnStopSfx(StopSfxEvent evt)
        {
            if (evt == null) return;
            StopLoopingInstance(evt.Track);
        }

        private void StopLoopingInstance(SfxTrack track)
        {
            if (track == SfxTrack.None) return;
            if (!_loopingInstances.TryGetValue(track, out var instance)) return;
            instance.Stop();
            instance.Dispose();
            _loopingInstances.Remove(track);
        }

        private void StopAllLoopingInstances()
        {
            foreach (var track in _loopingInstances.Keys.ToList())
            {
                StopLoopingInstance(track);
            }
        }

        private void OnAudioSettingsChanged(AudioSettingsChangedEvent evt)
        {
            if (evt == null) return;
            _sfxVolumeLevel = Math.Clamp(evt.SfxVolumeLevel, 0, 100);
        }

        private float ApplyUserVolume(float authoredVolume)
        {
            float scalar = _sfxVolumeLevel / (float)SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL;
            return MathHelper.Clamp(authoredVolume * scalar, 0f, 1f);
        }

        private SoundEffect ResolveSoundEffect(SfxTrack track)
        {
            if (track == SfxTrack.None) return null;
            if (_soundCache.TryGetValue(track, out var cached) && cached != null) return cached;

            string assetName = track switch
            {
                SfxTrack.SwordAttack => "SFX/Sword Attack 2",
                SfxTrack.SwordImpact => "SFX/Sword Impact Hit 2",
                SfxTrack.SwordUnsheath => "SFX/Sword Unsheath 2",
                SfxTrack.SwordWhoosh => "SFX/SFX_Whoosh_Sword_01",
                SfxTrack.Equip => "SFX/SFX_Equip_01",
                SfxTrack.BashShield => "SFX/SFX_Bash_Shield_01",
                SfxTrack.CardHover => "SFX/card_hand_hover",
                SfxTrack.ApplyCard => "SFX/apply-card",
                SfxTrack.CoinBag => "SFX/Coin Bag 3-1",
                SfxTrack.CashRegister => "SFX/Cash Register 1-2",
                SfxTrack.Firebuff => "SFX/Firebuff 1",
                SfxTrack.BagHandle => "SFX/Bag Handle 1-5",
                SfxTrack.Interface => "SFX/Interface",
                SfxTrack.Confirm => "SFX/Confirm",
                SfxTrack.PhaseChange => "SFX/Confirm",
                SfxTrack.Transition => "SFX/Transition",
                SfxTrack.Prayer => "SFX/Prayer",
                SfxTrack.GainAegis => "SFX/GainAegis",
                SfxTrack.EnemyAttackIntro => "SFX/EnemyAttackIntro",
                SfxTrack.ActiveDialogue => "SFX/active-dialogue",
                SfxTrack.ApplyBrittle => "SFX/apply-brittle",
                SfxTrack.ApplyCurse => "SFX/apply-curse",
                SfxTrack.ApplyScorched => "SFX/apply-scorched",
                SfxTrack.ApplyThorns => "SFX/apply-thorns",
                SfxTrack.ApplyFrozen => "SFX/apply-frozen",
                SfxTrack.ClimbMenuEnter => "SFX/climb-menu-enter",
                SfxTrack.ClimbWidgetLeave => "SFX/climb-widget-leave",
                SfxTrack.OpenInventory => "SFX/open-inventory",
                SfxTrack.CloseInventory => "SFX/close-inventory",
                SfxTrack.DrawCard => "SFX/draw-card",
                SfxTrack.EnemyDeath => "SFX/enemy-death",
                SfxTrack.GainCourage => "SFX/gain-courage",
                SfxTrack.GainTemperance => "SFX/gain-temperance",
                SfxTrack.MedalActivated => "SFX/medal-activated",
                SfxTrack.PledgeCard => "SFX/pledge-card",
                SfxTrack.Purchase => "SFX/purchase",
                SfxTrack.SaintInfo => "SFX/saint-info",
                SfxTrack.Temperance => "SFX/temperance",
                SfxTrack.TakeReward => "SFX/take-reward",
                SfxTrack.UpgradeCard => "SFX/upgrade-card",
                SfxTrack.BoosterPackReveal => "SFX/booster-pack-reveal",
                SfxTrack.DeckShuffle => "SFX/deck-shuffle",
                SfxTrack.ShieldBlock => "SFX/shield-block",
				SfxTrack.GemDrop => "SFX/gem-drop",
				SfxTrack.ClimbPointsTier => "SFX/climb-points-award/climb-points-tier",
				SfxTrack.ClimbPointsTotal => "SFX/climb-points-award/climb-points-total",
				SfxTrack.GameOver => "SFX/game-over",
				SfxTrack.AchievementReveal => "SFX/achievement-reveal",
                _ => null
            };

            if (string.IsNullOrEmpty(assetName)) return null;

            try
            {
                var soundEffect = _content.Load<SoundEffect>(assetName);
                _soundCache[track] = soundEffect;
                return soundEffect;
            }
            catch
            {
                return null;
            }
        }
    }
}

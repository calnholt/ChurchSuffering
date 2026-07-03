using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace Crusaders30XX.ECS.Systems
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
        private int _sfxVolumeLevel;
        [DebugEditable(DisplayName = "Mute")]
        public bool Mute { get; set; } = false;

        public SoundEffectManagerSystem(EntityManager entityManager, ContentManager content) : base(entityManager)
        {
            _content = content;
            _sfxVolumeLevel = SaveCache.GetSfxVolumeLevel();
            EventManager.Subscribe<PlaySfxEvent>(OnPlaySfx);
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

using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Loadouts;
using System;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Data.Tutorials;

namespace Crusaders30XX.ECS.Factories
{
    /// <summary>
    /// Factory for creating common game entities
    /// </summary>
    public static class EntityFactory
    {
        
        /// <summary>
        /// Creates a player entity
        /// </summary>
        public static Entity CreatePlayer(
            World world,
            string name = "Player",
            LoadoutDefinition loadoutOverride = null)
        {
            var entity = world.CreateEntity(name);
            
            var player = new Player{};
            
            var transform = new Transform
            {
                Position = new Vector2(100, 300),
                Scale = Vector2.One
            };
            
            var loadout = loadoutOverride;
            if (loadout == null)
            {
                LoadoutDefinitionCache.TryGet("loadout_1", out loadout);
                loadout ??= SaveCache.GetLoadout("loadout_1");
            }
            loadout ??= new LoadoutDefinition { id = "loadout_1", name = "loadout_1" };

            var sprite = new Sprite
            {
                TexturePath = "player",
                IsVisible = true
            };

            var portraitInfo = new PortraitInfo {
                CurrentScale = 0,
                Owner = entity,
                TextureHeight = 0,
                TextureWidth = 0,
            };
            var equippedTemperanceAbility = new EquippedTemperanceAbility {
                AbilityId = loadout.temperanceId,
                Owner = entity
            };
            
            world.AddComponent(entity, player);
            world.AddComponent(entity, transform);
            world.AddComponent(entity, sprite);
            world.AddComponent(entity, portraitInfo);
            world.AddComponent(entity, equippedTemperanceAbility);
            world.AddComponent(entity, ParallaxLayer.GetCharacterParallaxLayer());
            // Equip default weapon (not in deck)
            world.AddComponent(entity, new EquippedWeapon { WeaponId = loadout.weaponId });
            if (!string.IsNullOrWhiteSpace(loadout.headId))
            {
                var equipHeadEntity = world.CreateEntity("Equip_Head");
                var equipment = EquipmentFactory.Create(loadout.headId);
                equipment.Initialize(world.EntityManager, equipHeadEntity);
                world.AddComponent(equipHeadEntity, new Transform { Position = new Vector2(0, 0), ZOrder = 10001 });
                world.AddComponent(equipHeadEntity, new UIElement { IsInteractable = true });
                world.AddComponent(equipHeadEntity, new EquippedEquipment { EquippedOwner = entity, Equipment = equipment });
                TagPersistAcrossScenes(world.EntityManager, equipHeadEntity);
            }
            if (!string.IsNullOrWhiteSpace(loadout.legsId))
            {
                var equipLegsEntity = world.CreateEntity("Equip_Legs");
                var equipment = EquipmentFactory.Create(loadout.legsId);
                equipment.Initialize(world.EntityManager, equipLegsEntity);
                world.AddComponent(equipLegsEntity, new Transform { Position = new Vector2(0, 0), ZOrder = 10001 });
                world.AddComponent(equipLegsEntity, new UIElement { IsInteractable = true });
                world.AddComponent(equipLegsEntity, new EquippedEquipment { EquippedOwner = entity, Equipment = equipment });
                TagPersistAcrossScenes(world.EntityManager, equipLegsEntity);
            }
            if (!string.IsNullOrWhiteSpace(loadout.armsId))
            {
                var equipArmsEntity = world.CreateEntity("Equip_Arms");
                var equipment = EquipmentFactory.Create(loadout.armsId);
                equipment.Initialize(world.EntityManager, equipArmsEntity);
                world.AddComponent(equipArmsEntity, new Transform { Position = new Vector2(0, 0), ZOrder = 10001 });
                world.AddComponent(equipArmsEntity, new UIElement { IsInteractable = true });
                world.AddComponent(equipArmsEntity, new EquippedEquipment { EquippedOwner = entity, Equipment = equipment });
                TagPersistAcrossScenes(world.EntityManager, equipArmsEntity);
            }
            if (!string.IsNullOrWhiteSpace(loadout.chestId))
            {
                var equipChestEntity = world.CreateEntity("Equip_Chest");
                var equipment = EquipmentFactory.Create(loadout.chestId);
                equipment.Initialize(world.EntityManager, equipChestEntity);
                world.AddComponent(equipChestEntity, new Transform { Position = new Vector2(0, 0), ZOrder = 10001 });
                world.AddComponent(equipChestEntity, new UIElement { IsInteractable = true });
                world.AddComponent(equipChestEntity, new EquippedEquipment { EquippedOwner = entity, Equipment = equipment });
                TagPersistAcrossScenes(world.EntityManager, equipChestEntity);
            }
            // Parallax handled by UI root in EquipmentDisplaySystem
            // Attach Courage resource component by default (optional mechanics can read presence)
            world.AddComponent(entity, new Courage { Amount = 0 });
            // Attach Temperance resource component by default
            world.AddComponent(entity, new Temperance { Amount = 0 });
            // Attach Action Points component by default
            world.AddComponent(entity, new ActionPoints { Current = 0 });
            // Attach HP component
            world.AddComponent(entity, new HP { Max = 20, Current = 20, UnscarredMax = 20 });
            // Equip default medals (can equip multiple later). For now, just st_luke.
            foreach (var medalId in loadout.medalIds)
            {
                var medalEntity = world.CreateEntity($"Medal_{medalId}");
                var medal = MedalFactory.Create(medalId);
                medal.Initialize(world.EntityManager, medalEntity);
                world.AddComponent(medalEntity, new EquippedMedal { EquippedOwner = entity, Medal = medal });
                world.AddComponent(medalEntity, ParallaxLayer.GetUIParallaxLayer());
                world.AddComponent(medalEntity, new UIElement { IsInteractable = false });
                world.AddComponent(medalEntity, new Transform { Position = Vector2.Zero, ZOrder = 10001 });
                TagPersistAcrossScenes(world.EntityManager, medalEntity);
            }
            // Attach starting Intellect and MaxHandSize stats
            world.AddComponent(entity, new Intellect { Value = 4 });
            world.AddComponent(entity, new MaxHandSize { Value = MaxHandSize.MAX_HAND_SIZE + (StateSingleton.IsPledgeEnabled ? 0 : 1) });

            var st = new BattleStateInfo { Owner = entity };
            world.AddComponent(entity, st);

            world.AddComponent(entity, new AppliedPassives());
            // Pre-create Weapon tooltip hover entity (bounds updated by EquippedWeaponDisplaySystem)
            var weaponTooltip = world.CreateEntity("UI_WeaponTooltip");
            world.AddComponent(weaponTooltip, new Transform { Position = Vector2.Zero, ZOrder = 10001 });
            world.AddComponent(weaponTooltip, new UIElement { Bounds = new Rectangle(0, 0, 1, 1), Tooltip = "Weapon" });

            TagPersistAcrossScenes(world.EntityManager, entity);
            TagPersistAcrossScenes(world.EntityManager, weaponTooltip);

            return entity;
        }

        private static void TagPersistAcrossScenes(EntityManager entityManager, Entity entity)
        {
            if (entity == null || entity.HasComponent<DontDestroyOnLoad>()) return;
            entityManager.AddComponent(entity, new DontDestroyOnLoad());
        }
        
        /// <summary>
        /// Creates a deck entity
        /// </summary>
        public static Entity CreateDeck(World world, string name = "Deck")
        {
            var entity = world.CreateEntity(name);
            
            var deck = new Deck {};
            
            world.AddComponent(entity, deck);
            
            return entity;
        }
        
        /// <summary>
        /// Creates a game state entity
        /// </summary>
        public static Entity CreateGameState(World world)
        {
            var entity = world.CreateEntity("GameState");
            
			var phaseState = world.CreateEntity("PhaseState");
			var ps = new PhaseState { Main = MainPhase.StartBattle, Sub = SubPhase.StartBattle, TurnNumber = 1 };
			world.AddComponent(phaseState, ps);


            // Ensure a Battlefield world component exists with a default location
            world.AddComponent(entity, new Battlefield { Location = BattleLocation.Desert });


            // Ensure BattleInfo singleton exists
            var biEntity = world.EntityManager.GetEntitiesWithComponent<BattleInfo>().FirstOrDefault();
			if (biEntity == null)
			{
				biEntity = world.CreateEntity("BattleInfo");
				world.AddComponent(biEntity, new BattleInfo { TurnNumber = 1 });
			}
            return entity;
        }

        public static Entity CreateCardGeometrySettings(World world)
        {
            var entity = world.CreateEntity("CardGeometrySettings");
            world.AddComponent(entity, new CardGeometrySettings
            {
                CardWidth = CardGeometrySettings.DefaultWidth,
                CardHeight = CardGeometrySettings.DefaultHeight,
                CardOffsetYExtra = CardGeometrySettings.DefaultOffsetYExtra,
                CardGap = CardGeometrySettings.DefaultGap,
                CardCornerRadius = CardGeometrySettings.DefaultCornerRadius,
                HighlightBorderThickness = CardGeometrySettings.DefaultHighlightBorderThickness
            });
            return entity;
        }
        
        [Obsolete("Use RunDeckService.EnsureRunDeck for run deck cards.")]
        public static List<Entity> CreateDeckFromLoadout(EntityManager entityManager)
        {
			var deckEntity = RunDeckService.EnsureRunDeck(entityManager);
			var deck = deckEntity?.GetComponent<Deck>();
			return deck?.Cards?.ToList() ?? new List<Entity>();
        }

        // Dynamic description generation removed; we now use JSON 'text' exclusively

		private static CardData.CardColor ParseColor(string color)
        {
            if (string.IsNullOrEmpty(color)) return CardData.CardColor.White;
            switch (color.Trim().ToLowerInvariant())
            {
                case "red": return CardData.CardColor.Red;
                case "black": return CardData.CardColor.Black;
                case "white":
                default: return CardData.CardColor.White;
            }
        }

        private static string ColorToKeyString(CardData.CardColor color)
        {
            return color switch
            {
                CardData.CardColor.Red => "Red",
                CardData.CardColor.Black => "Black",
                _ => "White"
            };
        }

        private static CardData.CardRarity ParseRarity(string rarity)
        {
            if (string.IsNullOrEmpty(rarity)) return CardData.CardRarity.Common;
            switch (rarity.Trim().ToLowerInvariant())
            {
                case "uncommon": return CardData.CardRarity.Uncommon;
                case "rare": return CardData.CardRarity.Rare;
                case "legendary": return CardData.CardRarity.Legendary;
                case "common":
                default: return CardData.CardRarity.Common;
            }
        }

        private static CardData.CostType ParseCostType(string cost)
        {
            if (string.IsNullOrEmpty(cost)) return CardData.CostType.NoCost;
            switch (cost.Trim().ToLowerInvariant())
            {
                case "red": return CardData.CostType.Red;
                case "white": return CardData.CostType.White;
                case "black": return CardData.CostType.Black;
                case "any": return CardData.CostType.Any;
                default: return CardData.CostType.NoCost;
            }
        }

        // Helper: create card entity from CardDefinition id and color
        public static Entity CreateCardFromDefinition(
            EntityManager entityManager,
            string cardId,
            CardData.CardColor color,
            bool allowWeapons = false,
            int index = 0,
            string cardKey = null,
            bool persistForRun = false,
            bool suppressStatDeltaDisplay = false,
            bool isUpgraded = false,
            string runDeckEntryId = null)
        {
            var card = CardFactory.Create(cardId);
            if (card == null) return null;
            if (card.IsWeapon && !allowWeapons) return null; // only non-weapons for deck/library
            if (!isUpgraded && !string.IsNullOrWhiteSpace(cardKey)
                && RunDeckService.TryParseCardKey(cardKey, out _, out _, out var keyIsUpgraded))
            {
                isUpgraded = keyIsUpgraded;
            }
            card.IsUpgraded = isUpgraded;
            string name = card.DisplayName ?? cardId;
            var entity = entityManager.CreateEntity($"Card_{name}_{color}_{index}");

            var cardData = new CardData
            {
                Card = card,
                Color = color
            };
            
            try
            {
                cardData.Card?.Initialize(entityManager, entity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Initialize for card {cardId}: {ex}");
                throw;
            }

            var transform = new Transform { Position = new Vector2(-1000, -1000), Scale = Vector2.One };
            var sprite = new Sprite { TexturePath = string.Empty, IsVisible = true };
            var uiElement = new UIElement
            {
                Bounds = new Rectangle(-1000, -1000, 250, 350),
                IsInteractable = true,
                TooltipPosition = TooltipPosition.Above,
                TooltipOffsetPx = 30,
                EventType = UIElementEventType.CardClicked,
                SecondaryEventType = UIElementEventType.PledgeCard,
            };

            entityManager.AddComponent(entity, cardData);
            entityManager.AddComponent(entity, transform);
            entityManager.AddComponent(entity, sprite);
            entityManager.AddComponent(entity, uiElement);
            entityManager.AddComponent(entity, ParallaxLayer.GetUIParallaxLayer());
            entityManager.AddComponent(entity, new PositionTween { Speed = 12f });
            entityManager.AddComponent(entity, new Hint { Text = card.GetCardHint(color) });
            if (persistForRun)
            {
                string key = cardKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = RunDeckService.BuildCardKey(cardId, color, isUpgraded);
                }
                entityManager.AddComponent(entity, new RunDeckCard
                {
                    EntryId = runDeckEntryId ?? string.Empty,
                    CardKey = key,
                });
                entityManager.AddComponent(entity, new DontDestroyOnLoad());
            }
            else
            {
                entityManager.AddComponent(entity, new DontDestroyOnReload());
            }
            var modifiedBlock = new ModifiedBlock { Modifications = new List<Modification>() };
            entityManager.AddComponent(entity, modifiedBlock);
            if (color == CardData.CardColor.Black)
            {
                modifiedBlock.Modifications.Add(new Modification { Delta = 1, Reason = "Black card" });
            }
            if (card.Type == CardType.Attack)
            {
                entityManager.AddComponent(entity, new ModifiedDamage { Modifications = new List<Modification>() });
            }
            if (suppressStatDeltaDisplay)
            {
                entityManager.AddComponent(entity, new SuppressStatDeltaDisplay { Owner = entity });
            }
            ConfigureCardTooltip(entityManager, entity, uiElement, card);
            return entity;
        }

        private static void ConfigureCardTooltip(
            EntityManager entityManager,
            Entity entity,
            UIElement uiElement,
            CardBase card,
            CardData.CardColor? cardColor = null)
        {
            if (uiElement == null || card == null) return;

            string displayText = card.GetDisplayText();
            uiElement.Tooltip = string.Empty;
            uiElement.TooltipKeywordSource = displayText ?? string.Empty;
            uiElement.TooltipType = TooltipType.Text;
            uiElement.TooltipPosition = TooltipPosition.Above;
            uiElement.TooltipOffsetPx = 30;

            if (string.IsNullOrWhiteSpace(card.CardTooltip)) return;

            entityManager.AddComponent(entity, new CardTooltip
            {
                CardId = card.CardTooltip,
                CardColor = cardColor,
            });
            uiElement.TooltipType = TooltipType.Card;
        }

        public static Entity CreateEnemyFromId(World world, string enemyId, EntityManager entityManager)
        {
            var def = EnemyFactory.Create(enemyId);
            if (def == null)
            {
                throw new InvalidOperationException($"Cannot spawn enemy: unknown enemy ID '{enemyId ?? string.Empty}'.");
            }

            var existingEnemy = entityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
            if (existingEnemy != null)
            {
                entityManager.DestroyEntity(existingEnemy.Id);
            }
            def.EntityManager = entityManager;
            bool isGuidedTutorial = GuidedTutorialService.IsActive(entityManager) && def.IsTutorialOnly;

            var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null || deck.Cards.Count == 0)
            {
                throw new InvalidOperationException("Cannot spawn enemy: player deck is missing or empty.");
            }
            float deckHealthWeight = RunDeckService.CalculateEnemyHealthDeckWeight(
                entityManager,
                deck.Cards.Count);
			if (isGuidedTutorial)
			{
				var tutorial = GuidedTutorialService.GetState(entityManager);
				int hp = GuidedTutorialDefinitions.GetSection(tutorial.Section).EnemyHp;
				def.MaxHealth = hp;
				def.CurrentHealth = hp;
			}
            else
            {
                def.ApplyHealthFromDeckWeight(deckHealthWeight);
                ApplyWayStationEnemyHealthModifier(def);
            }
            if (def.MaxHealth <= 0)
            {
                def.MaxHealth = 1;
                def.CurrentHealth = 1;
            }

            var enemyEntity = world.CreateEntity($"Enemy");
            // TODO: Add equipment HP modifier
            // var numEquippedEquipment = world.EntityManager.GetEntitiesWithComponent<EquippedEquipment>().Count();
            // int equipmentHpModifier = numEquippedEquipment * 3;
            // def.MaxHealth += equipmentHpModifier;
            var enemy = new Enemy { Id = def.Id, Name = def.Id.ToKey(), MaxHealth = def.MaxHealth, CurrentHealth = def.CurrentHealth, EnemyBase = def };
            var enemyTransform = new Transform { Position = new Vector2(world.EntityManager.GetEntitiesWithComponent<Player>().Any() ? 1200 : 1000, 260), Scale = Vector2.One };
            world.AddComponent(enemyEntity, enemy);
            world.AddComponent(enemyEntity, enemyTransform);
            world.AddComponent(enemyEntity, new UIElement { Tooltip = def.Name ?? def.Id.ToKey(), IsInteractable = false , TooltipPosition = TooltipPosition.Above });
            world.AddComponent(enemyEntity, new HP { Max = enemy.MaxHealth, Current = enemy.CurrentHealth });
            world.AddComponent(enemyEntity, new PortraitInfo { TextureWidth = 0, TextureHeight = 0, CurrentScale = 1f });
            world.AddComponent(enemyEntity, new EnemyArsenal { AttackIds = [.. def.GetAttackIds(world.EntityManager, 0)] });
            world.AddComponent(enemyEntity, new AttackIntent());
            world.AddComponent(enemyEntity, new AppliedPassives());
            if (isGuidedTutorial)
            {
                world.AddComponent(enemyEntity, new TutorialEnemy());
            }
            
            bool isRootQuest = false;
            var queued = world.EntityManager.GetEntity("QueuedEvents")?.GetComponent<QueuedEvents>();
            if (queued != null && queued.QuestIndex == 0)
            {
                isRootQuest = true;
            }

            if (!isRootQuest && !isGuidedTutorial)
            {
                world.AddComponent(enemyEntity, new Threat { Amount = 0 });
            }
            world.AddComponent(enemyEntity, ParallaxLayer.GetCharacterParallaxLayer());

            var playerEntity = world.EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();

            // if (def.OnStartOfBattle != null)
            // {
            //     def.OnStartOfBattle(world.EntityManager);
            // }

            // Apply quest modifications if any
            // if (modifications != null && modifications.Count > 0)
            // {
            //     foreach (var mod in modifications)
            //     {
            //         if (string.IsNullOrEmpty(mod.Type)) continue;

            //         if (mod.Type.Equals("HP", StringComparison.OrdinalIgnoreCase))
            //         {
            //             // Apply HP modification
            //             enemy.MaxHealth += mod.Delta;
            //             enemy.CurrentHealth += mod.Delta;
            //             var hpComponent = enemyEntity.GetComponent<HP>();
            //             if (hpComponent != null)
            //             {
            //                 hpComponent.Max += mod.Delta;
            //                 hpComponent.Current += mod.Delta;
            //             }
            //             Console.WriteLine($"[EntityFactory] Applied HP modification: +{mod.Delta} (new HP: {enemy.MaxHealth})");
            //         }
            //         else if (mod.Type.Equals("Armor", StringComparison.OrdinalIgnoreCase))
            //         {
            //             // Apply Armor modification via passive
            //             Enum.TryParse<AppliedPassiveType>("Armor", true, out var armorType);
            //             EventManager.Publish(new ApplyPassiveEvent { Target = enemyEntity, Delta = mod.Delta, Type = armorType });
            //             Console.WriteLine($"[EntityFactory] Applied Armor modification: +{mod.Delta}");
            //         }
            //     }
            // }

            // Pre-create Threat tooltip hover entity (bounds updated by ThreatDisplaySystem)
            var threatTooltip = world.CreateEntity("UI_ThreatTooltip");
            world.AddComponent(threatTooltip, new ThreatTooltipAnchor());
            world.AddComponent(threatTooltip, new Transform { Position = Vector2.Zero, ZOrder = 10001 });
            world.AddComponent(threatTooltip, new UIElement { Bounds = new Rectangle(0, 0, 1, 1), Tooltip = "Threat\n\nAt the start of the enemy's turn, it gains X aggression equal to its current threat level.\nWhenever you attack an enemy, it reduces their threat by one.\nEnemy's gain one threat at the end of their turn." });

            return enemyEntity;
        }

        private static void ApplyWayStationEnemyHealthModifier(EnemyBase def)
        {
            if (def == null) return;
            float modifier = WayStationRunSetupSingleton.EnemyHealthModifier;
            if (modifier <= 0f) modifier = 1f;

            def.MaxHealth = Math.Max(1, (int)Math.Round(def.MaxHealth * modifier));
            def.CurrentHealth = Math.Max(1, (int)Math.Round(def.CurrentHealth * modifier));
            def.CurrentHealth = Math.Min(def.CurrentHealth, def.MaxHealth);
        }

		/// <summary>
		/// Create UI entities for each for-sale item in a shop.
		/// </summary>
		public static List<Entity> CreateForSale(EntityManager entityManager, IEnumerable<ForSaleItemDefinition> defs, string shopName)
		{
			var result = new List<Entity>();
			if (defs == null) return result;
			// Shop ownership is loadout-based; future random-shop PR may refine purchase rules.
			int idx = 0;
			foreach (var fs in defs)
			{
				if (fs == null || string.IsNullOrWhiteSpace(fs.id)) { idx++; continue; }
				string id = fs.id;
				string type = fs.type ?? "Card";
				ForSaleItemType itemType = ForSaleItemType.Card;
				switch (type.Trim().ToLowerInvariant())
				{
					case "medal": itemType = ForSaleItemType.Medal; break;
					case "equipment": itemType = ForSaleItemType.Equipment; break;
					case "weapon": itemType = ForSaleItemType.Weapon; break;
					case "card":
					default: itemType = ForSaleItemType.Card; break;
				}

				string displayName = id;
                var card = CardFactory.Create(id);
				try
				{
					if ((itemType == ForSaleItemType.Card || itemType == ForSaleItemType.Weapon) && card != null)
					{
						displayName = string.IsNullOrWhiteSpace(card.Name) ? id : card.Name;
					}
					else if (itemType == ForSaleItemType.Medal && MedalFactory.Create(id) is { } medal)
					{
						displayName = string.IsNullOrWhiteSpace(medal.Name) ? id : medal.Name;
					}
					else if (itemType == ForSaleItemType.Equipment)
					{
						var equipment = EquipmentFactory.Create(id);
						displayName = string.IsNullOrWhiteSpace(equipment.Name) ? id : equipment.Name;
					}
				}
				catch { }

				var e = entityManager.CreateEntity($"ShopItem_{id}_{idx}");
				entityManager.AddComponent(e, new Transform { Position = new Vector2(-1000, -1000), ZOrder = 10002 });
				entityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());
                var uiElement = new UIElement
                {
                    Bounds = new Rectangle(-1000, -1000, 1, 1),
                    IsInteractable = true,
                    TooltipPosition = TooltipPosition.Above,
                };
                if ((itemType == ForSaleItemType.Card || itemType == ForSaleItemType.Weapon) && card != null)
                {
                    ConfigureCardTooltip(entityManager, e, uiElement, card);
                }
                else if (itemType == ForSaleItemType.Medal)
                {
                    var medal = MedalFactory.Create(id);
                    uiElement.Tooltip = medal == null ? string.Empty : $"{medal.Name}\n\n{medal.Text}";
                }
                else if (itemType == ForSaleItemType.Equipment)
                {
                    var equipment = EquipmentFactory.Create(id);
                    uiElement.Tooltip = EquipmentService.GetTooltipText(
                        equipment,
                        EquipmentTooltipType.Shop);
                }
				// Always attach UIElement for hover/click regardless of item type
				entityManager.AddComponent(e, uiElement);
				entityManager.AddComponent(e, new OwnedByScene { Scene = SceneId.Shop });
				entityManager.AddComponent(e, new ForSaleItem
				{
					Id = id,
					ItemType = itemType,
					Price = fs.price,
					IsPurchased = SaveCache.IsItemOwned(id, itemType),
					DisplayName = displayName,
					SourceShopName = shopName ?? string.Empty
				});
				result.Add(e);
				idx++;
			}
			return result;
		}

		public static List<Entity> CreateForSaleFromRunShop(
			EntityManager entityManager,
			RunMapShop shop)
		{
			var result = new List<Entity>();
			if (shop?.items == null) return result;

			for (int slotIndex = 0; slotIndex < shop.items.Count; slotIndex++)
			{
				var item = shop.items[slotIndex];
				if (item == null || string.IsNullOrWhiteSpace(item.cardId)) continue;

				string id = item.cardId;
				var e = entityManager.CreateEntity($"ShopItem_{shop.id}_{slotIndex}");
				entityManager.AddComponent(e, new Transform { Position = new Vector2(-1000, -1000), ZOrder = 10002 });
				entityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());

				if (item.IsMedal)
				{
					string displayName = id;
					var medal = MedalFactory.Create(id);
					if (medal != null)
					{
						displayName = string.IsNullOrWhiteSpace(medal.Name) ? id : medal.Name;
					}

					var uiElement = new UIElement
					{
						Bounds = new Rectangle(-1000, -1000, 1, 1),
						IsInteractable = !item.isPurchased,
						Tooltip = medal == null ? string.Empty : $"{medal.Name}\n\n{medal.Text}",
						TooltipPosition = TooltipPosition.Above,
					};
					entityManager.AddComponent(e, new ForSaleItem
					{
						Id = id,
						ItemType = ForSaleItemType.Medal,
						Price = item.price,
						IsPurchased = item.isPurchased,
						DisplayName = displayName,
						SourceShopName = shop.id,
						ShopId = shop.id,
						ShopSlotIndex = slotIndex,
						DisplayRotationDeg = item.displayRotationDeg,
					});
					entityManager.AddComponent(e, uiElement);
				}
				else if (item.IsEquipment)
				{
					string displayName = id;
					var equipment = EquipmentFactory.Create(id);
					if (equipment != null)
					{
						displayName = string.IsNullOrWhiteSpace(equipment.Name) ? id : equipment.Name;
					}

					var uiElement = new UIElement
					{
						Bounds = new Rectangle(-1000, -1000, 1, 1),
						IsInteractable = !item.isPurchased,
						Tooltip = EquipmentService.GetTooltipText(equipment, EquipmentTooltipType.Shop),
						TooltipPosition = TooltipPosition.Above,
					};
					entityManager.AddComponent(e, new ForSaleItem
					{
						Id = id,
						ItemType = ForSaleItemType.Equipment,
						Price = item.price,
						IsPurchased = item.isPurchased || SaveCache.IsItemOwned(id, ForSaleItemType.Equipment),
						DisplayName = displayName,
						SourceShopName = shop.id,
						ShopId = shop.id,
						ShopSlotIndex = slotIndex,
						DisplayRotationDeg = item.displayRotationDeg,
					});
					entityManager.AddComponent(e, uiElement);
				}
				else
				{
					string displayName = id;
					var card = CardFactory.Create(id);
					if (card != null)
					{
						displayName = string.IsNullOrWhiteSpace(card.Name) ? id : card.Name;
					}

					var color = ParseCardColor(item.color);
					var uiElement = new UIElement
					{
						Bounds = new Rectangle(-1000, -1000, 1, 1),
						IsInteractable = !item.isPurchased,
						TooltipPosition = TooltipPosition.Above,
					};
					ConfigureCardTooltip(entityManager, e, uiElement, card, color);
					entityManager.AddComponent(e, new ForSaleItem
					{
						Id = id,
						ItemType = ForSaleItemType.Card,
						Price = item.price,
						IsPurchased = item.isPurchased,
						DisplayName = displayName,
						SourceShopName = shop.id,
						ShopId = shop.id,
						ShopSlotIndex = slotIndex,
						CardColor = color,
						DisplayRotationDeg = item.displayRotationDeg,
					});
					entityManager.AddComponent(e, uiElement);
				}

				entityManager.AddComponent(e, new OwnedByScene { Scene = SceneId.Shop });
				result.Add(e);
			}

			return result;
		}

		private static CardData.CardColor ParseCardColor(string color)
		{
			if (string.Equals(color, "Red", StringComparison.OrdinalIgnoreCase))
				return CardData.CardColor.Red;
			if (string.Equals(color, "Black", StringComparison.OrdinalIgnoreCase))
				return CardData.CardColor.Black;
			return CardData.CardColor.White;
		}

        /// <summary>
        /// Clones a card entity, copying all gameplay-affecting components while creating fresh visual/animation state.
        /// </summary>
        public static Entity CloneEntity(EntityManager entityManager, Entity sourceEntity)
        {
            if (sourceEntity == null) return null;

            // Get the card data to construct a proper name
            var sourceCardData = sourceEntity.GetComponent<CardData>();
            if (sourceCardData == null) return null; // Only supports card entities for now

            string name = sourceCardData.Card?.DisplayName ?? "Card";
            var clonedEntity = entityManager.CreateEntity($"Card_{name}_{sourceCardData.Color}_Clone_{sourceEntity.Id}");

            // Deep copy CardData
            var clonedCard = CardFactory.Create(sourceCardData.Card.CardId);
            if (clonedCard != null)
            {
                clonedCard.IsUpgraded = sourceCardData.Card.IsUpgraded;
            }
            var clonedCardData = new CardData
            {
                Card = clonedCard,
                Color = sourceCardData.Color,
                Owner = clonedEntity
            };
            entityManager.AddComponent(clonedEntity, clonedCardData);
            clonedCardData.Card?.Initialize(entityManager, clonedEntity);

            // Deep copy ModifiedBlock (with all modifications)
            var sourceModifiedBlock = sourceEntity.GetComponent<ModifiedBlock>();
            if (sourceModifiedBlock != null)
            {
                var clonedModifiedBlock = new ModifiedBlock
                {
                    Owner = clonedEntity,
                    Modifications = new List<Modification>(sourceModifiedBlock.Modifications)
                };
                entityManager.AddComponent(clonedEntity, clonedModifiedBlock);
            }

            // Deep copy ModifiedDamage (with all modifications)
            var sourceModifiedDamage = sourceEntity.GetComponent<ModifiedDamage>();
            if (sourceModifiedDamage != null)
            {
                var clonedModifiedDamage = new ModifiedDamage
                {
                    Owner = clonedEntity,
                    Modifications = new List<Modification>(sourceModifiedDamage.Modifications)
                };
                entityManager.AddComponent(clonedEntity, clonedModifiedDamage);
            }

            // Copy persistent card statuses
            if (sourceEntity.HasComponent<Frozen>())
            {
                entityManager.AddComponent(clonedEntity, new Frozen { Owner = clonedEntity });
            }
            if (sourceEntity.HasComponent<Brittle>())
            {
                entityManager.AddComponent(clonedEntity, new Brittle { Owner = clonedEntity });
            }
            if (sourceEntity.HasComponent<Scorched>())
            {
                entityManager.AddComponent(clonedEntity, new Scorched { Owner = clonedEntity });
            }
            if (sourceEntity.HasComponent<Thorned>())
            {
                entityManager.AddComponent(clonedEntity, new Thorned { Owner = clonedEntity });
            }
            if (sourceEntity.HasComponent<Colorless>())
            {
                entityManager.AddComponent(clonedEntity, new Colorless { Owner = clonedEntity });
            }
            if (sourceEntity.HasComponent<Cursed>())
            {
                entityManager.AddComponent(clonedEntity, new Cursed { Owner = clonedEntity });
            }
            var sourceCursedOriginal = sourceEntity.GetComponent<CursedOriginalCard>();
            if (sourceCursedOriginal != null)
            {
                entityManager.AddComponent(clonedEntity, new CursedOriginalCard
                {
                    Owner = clonedEntity,
                    CardId = sourceCursedOriginal.CardId,
                    Color = sourceCursedOriginal.Color,
                    IsUpgraded = sourceCursedOriginal.IsUpgraded,
                    IsStarter = sourceCursedOriginal.IsStarter,
                });
            }

            // Copy Hint
            var sourceHint = sourceEntity.GetComponent<Hint>();
            if (sourceHint != null)
            {
                entityManager.AddComponent(clonedEntity, new Hint 
                { 
                    Owner = clonedEntity,
                    Text = sourceHint.Text 
                });
            }

            // Copy CardTooltip
            var sourceCardTooltip = sourceEntity.GetComponent<CardTooltip>();
            if (sourceCardTooltip != null)
            {
                entityManager.AddComponent(clonedEntity, new CardTooltip
                {
					Owner = clonedEntity,
					CardId = sourceCardTooltip.CardId,
					TooltipScale = sourceCardTooltip.TooltipScale,
					CardColor = sourceCardTooltip.CardColor,
					IsUpgraded = sourceCardTooltip.IsUpgraded,
					CrossfadeUpgradePreview = sourceCardTooltip.CrossfadeUpgradePreview,
					PreviewRestrictionNames = new List<string>(sourceCardTooltip.PreviewRestrictionNames ?? new List<string>()),
				});
            }

            // Copy DontDestroyOnReload
            if (sourceEntity.HasComponent<DontDestroyOnReload>())
            {
                entityManager.AddComponent(clonedEntity, new DontDestroyOnReload { Owner = clonedEntity });
            }

            // Create fresh Transform (off-screen position)
            var sourceTransform = sourceEntity.GetComponent<Transform>();
            entityManager.AddComponent(clonedEntity, new Transform
            {
                Owner = clonedEntity,
                Position = new Vector2(-1000, -1000),
                Rotation = 0f,
                Scale = sourceTransform?.Scale ?? Vector2.One,
                ZOrder = 0
            });

            // Create fresh UIElement (no hover state, fresh bounds)
            var sourceUIElement = sourceEntity.GetComponent<UIElement>();
            var clonedUIElement = new UIElement
            {
                Owner = clonedEntity,
                Bounds = new Rectangle(-1000, -1000, 250, 350),
                IsHovered = false,
                IsClicked = false,
                IsInteractable = true,
                Tooltip = sourceUIElement?.Tooltip ?? "",
                TooltipKeywordSource = sourceUIElement?.TooltipKeywordSource ?? "",
                TooltipType = sourceUIElement?.TooltipType ?? TooltipType.Text,
                TooltipPosition = sourceUIElement?.TooltipPosition ?? TooltipPosition.Above,
                TooltipOffsetPx = sourceUIElement?.TooltipOffsetPx ?? 30,
                EventType = UIElementEventType.None,
                LayerType = sourceUIElement?.LayerType ?? UILayerType.Default,
                IsPreventDefaultClick = false,
                IsHidden = false,
                ShowHoverHighlight = sourceUIElement?.ShowHoverHighlight ?? true
            };
            entityManager.AddComponent(clonedEntity, clonedUIElement);

            // Create fresh Sprite
            var sourceSprite = sourceEntity.GetComponent<Sprite>();
            entityManager.AddComponent(clonedEntity, new Sprite
            {
                Owner = clonedEntity,
                TexturePath = sourceSprite?.TexturePath ?? string.Empty,
                SourceRectangle = null,
                Tint = Color.White,
                IsVisible = true
            });

            // Create fresh ParallaxLayer (fresh animation state)
            entityManager.AddComponent(clonedEntity, ParallaxLayer.GetUIParallaxLayer());
            entityManager.AddComponent(clonedEntity, new PositionTween { Speed = 12f });

            // Note: Explicitly excluding transient state components:
            // - AnimatingHandToDiscard
            // - AnimatingHandToZone
            // - AnimatingHandToDrawPile
            // - CardToDiscardFlight
            // - SelectedForPayment
            // - MarkedForSpecificDiscard
            // - MarkedForReturnToDeck
            // - TooltipOverrideBackup
            // - OwnedByScene (auto-added by EntityManager)

            return clonedEntity;
        }
    }
} 

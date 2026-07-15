using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Objects.Cards;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

internal readonly record struct CardBaseRenderModel(
    string CardId,
    string DisplayName,
    string DisplayText,
    string Costs,
    CardData.CardColor Color,
	CardData.CardColor? SecondaryColor,
    CardType Type,
    int PrintedDamage,
    int PrintedBlock,
    int EffectiveDamage,
    int EffectiveBlock,
    bool IsFreeAction,
    bool IsWeapon,
    bool IsToken,
    bool IsUpgraded,
    bool IsColorless,
    bool SuppressStatDelta,
    bool AlternateTreatsAsAttack,
    int AlternateAttackDamage,
    bool AlternateIsFreeAction,
    int StyleFingerprint,
    float Scale,
    float Rotation,
    int PhysicalWidth,
    int PhysicalHeight);

internal sealed record CachedCardSurface(Texture2D Texture, Rectangle LogicalBounds);

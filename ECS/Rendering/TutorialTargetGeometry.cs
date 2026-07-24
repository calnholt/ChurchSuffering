using System;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Rendering;

public readonly record struct TutorialTargetGeometry(Rectangle Bounds, float Rotation)
{
	public bool IsEmpty => Bounds.Width <= 0 || Bounds.Height <= 0;

	public Rectangle AxisAlignedBounds
	{
		get
		{
			if (IsEmpty || MathF.Abs(Rotation) < 0.0001f)
				return Bounds;

			float halfWidth = Bounds.Width * 0.5f;
			float halfHeight = Bounds.Height * 0.5f;
			float cosine = MathF.Abs(MathF.Cos(Rotation));
			float sine = MathF.Abs(MathF.Sin(Rotation));
			float rotatedHalfWidth = halfWidth * cosine + halfHeight * sine;
			float rotatedHalfHeight = halfWidth * sine + halfHeight * cosine;
			float centerX = Bounds.X + halfWidth;
			float centerY = Bounds.Y + halfHeight;
			int left = (int)MathF.Floor(centerX - rotatedHalfWidth);
			int top = (int)MathF.Floor(centerY - rotatedHalfHeight);
			int right = (int)MathF.Ceiling(centerX + rotatedHalfWidth);
			int bottom = (int)MathF.Ceiling(centerY + rotatedHalfHeight);
			return new Rectangle(left, top, right - left, bottom - top);
		}
	}

	public static TutorialTargetGeometry AxisAligned(Rectangle bounds) => new(bounds, 0f);
}

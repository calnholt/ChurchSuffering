using System;
using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.ECS.Data.VisualEffects
{
	internal readonly struct VisualEffectVariation
	{
		private readonly uint _seed;
		public int Seed => unchecked((int)_seed);

		public VisualEffectVariation(ActiveVisualEffect effect)
			: this(effect?.RequestId ?? Guid.Empty)
		{
		}

		public VisualEffectVariation(Guid requestId)
		{
			var bytes = requestId.ToByteArray();
			uint hash = 2166136261u;
			for (int i = 0; i < bytes.Length; i++)
			{
				hash = (hash ^ bytes[i]) * 16777619u;
			}
			_seed = hash;
		}

		public float Range(int channel, float min, float max)
		{
			uint value = Mix(_seed + unchecked((uint)channel * 0x9E3779B9u));
			float unit = (value & 0x00FFFFFFu) / 16777215f;
			return min + unit * (max - min);
		}

		public int Range(int channel, int minInclusive, int maxExclusive)
		{
			if (maxExclusive <= minInclusive) return minInclusive;
			return minInclusive + (int)MathF.Floor(Range(channel, 0f, 1f) * (maxExclusive - minInclusive));
		}

		private static uint Mix(uint value)
		{
			value ^= value >> 16;
			value *= 0x7FEB352Du;
			value ^= value >> 15;
			value *= 0x846CA68Bu;
			value ^= value >> 16;
			return value;
		}
	}
}

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

internal sealed class EffectParameterCache
{
	private readonly Dictionary<string, EffectParameter> _parameters = new();

	public EffectParameterCache(Effect effect)
	{
		if (effect == null) return;
		for (int i = 0; i < effect.Parameters.Count; i++)
		{
			EffectParameter parameter = effect.Parameters[i];
			_parameters[parameter.Name] = parameter;
		}
	}

	public void Set(string name, float value)
	{
		if (_parameters.TryGetValue(name, out EffectParameter parameter)) parameter.SetValue(value);
	}

	public void Set(string name, Vector2 value)
	{
		if (_parameters.TryGetValue(name, out EffectParameter parameter)) parameter.SetValue(value);
	}

	public void Set(string name, Vector3 value)
	{
		if (_parameters.TryGetValue(name, out EffectParameter parameter)) parameter.SetValue(value);
	}

	public void Set(string name, Matrix value)
	{
		if (_parameters.TryGetValue(name, out EffectParameter parameter)) parameter.SetValue(value);
	}
}

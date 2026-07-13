#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;

namespace Crusaders30XX.ECS.DataOriented.Integration.Host;

public interface IHostAudioRequestSink
{
    void Dispatch(in AudioPlaybackRequest request);
}

public interface IHostShaderRequestSink
{
    void Dispatch(in ShaderEffectRequest request);
}

public interface IHostRumbleRequestSink
{
    void Dispatch(in RumblePlaybackRequest request);
}

/// <summary>
/// Drains the immutable request spans once per monotonically increasing host frame. The root owns
/// queue reset; this adapter owns external dispatch and never mutates gameplay state.
/// </summary>
public sealed class PresentationRequestDrainAdapter
{
    private long lastFrame = -1;

    public int Drain(
        long frame,
        PresentationRequestQueues requests,
        IHostAudioRequestSink audio,
        IHostShaderRequestSink shaders,
        IHostRumbleRequestSink rumble)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(shaders);
        ArgumentNullException.ThrowIfNull(rumble);
        if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
        if (frame < lastFrame)
            throw new InvalidOperationException("Host presentation frames must be drained in increasing order.");
        if (frame == lastFrame) return 0;

        lastFrame = frame;
        var count = 0;
        ReadOnlySpan<AudioPlaybackRequest> audioValues = requests.Audio;
        for (var index = 0; index < audioValues.Length; index++)
        {
            audio.Dispatch(in audioValues[index]);
            count++;
        }

        ReadOnlySpan<ShaderEffectRequest> shaderValues = requests.Shaders;
        for (var index = 0; index < shaderValues.Length; index++)
        {
            shaders.Dispatch(in shaderValues[index]);
            count++;
        }


        ReadOnlySpan<RumblePlaybackRequest> rumbleValues = requests.Rumble;
        for (var index = 0; index < rumbleValues.Length; index++)
        {
            rumble.Dispatch(in rumbleValues[index]);
            count++;
        }
        return count;
    }
}

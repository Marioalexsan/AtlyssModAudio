using NAudio.Wave;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public static class AudioClipLoader
{
    public static readonly string[] SupportedLoadExtensions = [
        ".wav",
        ".ogg",
        ".mp3"
    ];

    public static readonly string[] SupportedStreamExtensions = [
        ".wav",
        ".ogg",
        ".mp3"
    ];

    public static readonly string[] SupportedExtensions = SupportedLoadExtensions.Concat(SupportedStreamExtensions).Distinct().ToArray();

    /// <summary>
    /// Creates an empty clip with the given name and duration.
    /// </summary>
    public static AudioClip GenerateEmptyClip(string name, int samples)
    {
        var clip = AudioClip.Create(name, samples, 1, 44100, false);
        clip.SetData(new float[samples], 0);
        return clip;
    }

    /// <summary>
    /// Loads an audio clip in its entirety from the disk.
    /// </summary>
    public static AudioClip LoadFromFile(string clipName, string path, float volumeModifier)
    {
        using var stream = GetStream(path);
        stream.VolumeModifier = volumeModifier;

        var clip = AudioClip.Create(clipName, stream.TotalFrames, stream.ChannelsPerFrame, stream.Frequency, false);

        var totalSamples = stream.TotalFrames * stream.ChannelsPerFrame;

        var buffer = new float[totalSamples];

        stream.ReadSamples(buffer);
        clip.SetData(buffer, 0);
        return clip;
    }

    /// <summary>
    /// Streams an audio clip from the disk.
    /// </summary>
    public static AudioClip StreamFromFile(string clipName, string path, float volumeModifier, out IAudioStream openedStream)
    {
        var stream = openedStream = GetStream(path);
        stream.VolumeModifier = volumeModifier;

        return AudioClip.Create(clipName, stream.TotalFrames, stream.ChannelsPerFrame, stream.Frequency, true, stream.ReadSamples, stream.SetSamplePosition);
    }

    private static IAudioStream GetStream(string path)
    {
        if (path.EndsWith(".ogg"))
            return new OggStream(File.OpenRead(path));

        if (path.EndsWith(".mp3"))
            return new Mp3Stream(File.OpenRead(path));

        if (path.EndsWith(".wav"))
            return new WavStream(File.OpenRead(path));

        throw new NotImplementedException("The given file format isn't supported for streaming.");
    }
}

public interface IAudioStream : IDisposable
{
    float VolumeModifier { get; set; }

    int TotalFrames { get; }
    int ChannelsPerFrame { get; }
    int Frequency { get; }

    void ReadSamples(float[] samples); // Unity seems to be calling this with float[4096] (at least it did in 2021.3)
    void SetSamplePosition(int newPosition);
}

public class OggStream(Stream stream) : IAudioStream
{
    private readonly NVorbis.VorbisReader _reader = new NVorbis.VorbisReader(stream);

    public float VolumeModifier { get; set; } = 1f;

    public int TotalFrames => (int)_reader.TotalSamples;
    public int ChannelsPerFrame => _reader.Channels;
    public int Frequency => _reader.SampleRate;

    public void ReadSamples(float[] samples)
    {
        _reader.ReadSamples(samples, 0, samples.Length);
        Utils.MultiplyFloatArray(samples, VolumeModifier);
    }

    public void SetSamplePosition(int newPosition)
    {
        _reader.SamplePosition = newPosition;
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}

public class WavStream : IAudioStream
{
    public WavStream(Stream stream)
    {
        _reader = new WaveFileReader(stream);
        _provider = _reader.ToSampleProvider();
    }
    private readonly WaveFileReader _reader;
    private readonly ISampleProvider _provider;

    public float VolumeModifier { get; set; } = 1f;

    public int TotalFrames => (int)_reader.SampleCount;
    public int ChannelsPerFrame => _reader.WaveFormat.Channels;
    public int Frequency => _reader.WaveFormat.SampleRate;

    public void ReadSamples(float[] samples)
    {
        _provider.Read(samples, 0, samples.Length);
        Utils.MultiplyFloatArray(samples, VolumeModifier);
    }

    public void SetSamplePosition(int newPosition)
    {
        _reader.Position = newPosition * _reader.BlockAlign;
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}

public class Mp3Stream : IAudioStream
{
    public Mp3Stream(Stream stream)
    {
        _reader = new Mp3FileReader(stream);
        _provider = _reader.ToSampleProvider();
    }
    private readonly Mp3FileReader _reader;
    private readonly ISampleProvider _provider;

    public float VolumeModifier { get; set; } = 1f;

    public int TotalFrames => (int)(_reader.Length * 8 / ChannelsPerFrame / _reader.WaveFormat.BitsPerSample);
    public int ChannelsPerFrame => _reader.WaveFormat.Channels;
    public int Frequency => _reader.WaveFormat.SampleRate;

    public void ReadSamples(float[] samples)
    {
        _provider.Read(samples, 0, samples.Length);
        Utils.MultiplyFloatArray(samples, VolumeModifier);
    }

    public void SetSamplePosition(int newPosition)
    {
        _reader.Position = newPosition * _reader.BlockAlign;
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}
using NAudio.Wave;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public static class AudioClipLoader
{
    public struct LoadResult
    {
        public LoadResult()
        {
            Samples = [];
        }
        
        public float[] Samples;
        public int TotalFrames;
        public int ChannelsPerFrame;
        public int Frequency;
        public IAudioStream? OpenStream;

        public AudioClip CreateFromResult(string clipName)
        {
            if (OpenStream == null)
            {
                var clip = AudioClip.Create(clipName, TotalFrames, ChannelsPerFrame, Frequency, false);
                clip.SetData(Samples, 0);
                return clip;
            }
            
            return AudioClip.Create(clipName, TotalFrames, ChannelsPerFrame, Frequency, true, OpenStream.ReadSamples, OpenStream.SetSamplePosition);
        }
    }

    public static readonly string[] SupportedExtensions = [
        ".wav",
        ".ogg",
        ".mp3"
    ];

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
    /// Loads or streams an audio clip from disk.
    /// If it's streamed, openedStream will have the resulting stream to keep alive.
    /// The decision to stream / load is done based on a memory cutoff corresponding to 20 seconds of stereo audio @ 44100Hz.
    /// If non-null, useStreamingIfTrue overrides this behaviour and streams if true, or loads in memory if false.
    /// </summary>
    public static LoadResult CreateFromFile(string clipName, string path, float volumeModifier, bool? useStreaming = null)
    {
        var stream = GetStream(path);
        stream.VolumeModifier = volumeModifier;

        if (useStreaming == null)
        {
            long approxUncompressedSizeBytes = stream.TotalFrames * stream.ChannelsPerFrame * 4;
            useStreaming = approxUncompressedSizeBytes > ModAudio.AudioStreamingLimitBytes.Value;
        }
        
        if (useStreaming.Value)
        {
            return new LoadResult()
            {
                ChannelsPerFrame = stream.ChannelsPerFrame,
                Frequency = stream.Frequency,
                TotalFrames = stream.TotalFrames,
                OpenStream = stream
            };
        }
        else
        {
            using var openedStream = stream;
            
            var totalSamples = stream.TotalFrames * stream.ChannelsPerFrame;

            var buffer = new float[totalSamples];

            stream.ReadSamples(buffer);
            
            return new LoadResult()
            {
                ChannelsPerFrame = stream.ChannelsPerFrame,
                Frequency = stream.Frequency,
                TotalFrames = stream.TotalFrames,
                Samples = buffer
            };
        }
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
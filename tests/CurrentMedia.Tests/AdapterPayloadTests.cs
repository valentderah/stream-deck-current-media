using System.Text.Json;
using CurrentMedia.Mac;
using Xunit;

namespace CurrentMedia.Tests;

public class AdapterPayloadTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly long T0Micros = T0.ToUnixTimeMilliseconds() * 1000;

    private static MediaState Parse(string payloadJson, DateTimeOffset now)
    {
        var message = JsonSerializer.Deserialize(
            "{\"type\":\"data\",\"diff\":false,\"payload\":" + payloadJson + "}",
            AdapterJsonContext.Default.AdapterStreamMessage);

        return message!.Payload!.ToMediaState("", now);
    }

    [Fact]
    public void ToMediaState_MicrosPayload_ReadsPositionAndDurationInSeconds()
    {
        var state = Parse(
            "{\"title\":\"Song\",\"playing\":true,\"playbackRate\":1," +
            "\"durationMicros\":200500000,\"elapsedTimeMicros\":30250000," +
            $"\"timestampEpochMicros\":{T0Micros}}}",
            T0);

        Assert.Equal(30.25, state.Position, 6);
        Assert.Equal(200.5, state.Duration, 6);
    }

    [Fact]
    public void ToMediaState_WithTimestamp_AnchorsThePositionToIt()
    {
        var state = Parse(
            "{\"title\":\"Song\",\"playing\":true,\"playbackRate\":1," +
            "\"durationMicros\":200000000,\"elapsedTimeMicros\":30000000," +
            $"\"timestampEpochMicros\":{T0Micros + 500_000}}}",
            T0.AddSeconds(40));

        Assert.Equal(T0.AddSeconds(0.5), state.PositionCapturedAt);
        Assert.Equal(69.5, PlaybackClock.PositionAt(MediaTimeline.From(state, T0.AddSeconds(40)), T0.AddSeconds(40)), 6);
    }

    [Fact]
    public void ToMediaState_WithoutTimestamp_AnchorsThePositionToNow()
    {
        var state = Parse(
            "{\"title\":\"Song\",\"playing\":true,\"playbackRate\":1," +
            "\"durationMicros\":200000000,\"elapsedTimeMicros\":30000000}",
            T0.AddSeconds(40));

        Assert.Equal(T0.AddSeconds(40), state.PositionCapturedAt);
    }

    [Fact]
    public void ToMediaState_SecondsPayload_StillReadsPositionAndDuration()
    {
        var state = Parse(
            "{\"title\":\"Song\",\"playing\":true,\"playbackRate\":1,\"duration\":200.5,\"elapsedTime\":30.25}",
            T0);

        Assert.Equal(30.25, state.Position, 6);
        Assert.Equal(200.5, state.Duration, 6);
    }

    [Fact]
    public void ToMediaState_CopiesThePlaybackRate()
    {
        var state = Parse(
            "{\"title\":\"Song\",\"playing\":true,\"playbackRate\":1.75," +
            "\"durationMicros\":200000000,\"elapsedTimeMicros\":30000000}",
            T0);

        Assert.Equal(1.75, state.PlaybackRate);
    }

    [Fact]
    public void ToMediaState_PlayingWithoutARate_AssumesNormalSpeed()
    {
        var state = Parse(
            "{\"title\":\"Song\",\"playing\":true," +
            "\"durationMicros\":200000000,\"elapsedTimeMicros\":30000000}",
            T0);

        Assert.Equal("Playing", state.Status);
        Assert.Equal(1, MediaTimeline.From(state, T0).Rate);
    }
}

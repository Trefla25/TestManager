using System.Collections.Immutable;
using eHub.Contracts;
using FluentAssertions;

namespace eHub.Tests.Connectors.PacketTransfer.UI;

public class PacketsFilterTests
{
    private const string MinDate = "min";
    private const string MaxDate = "max";
    private const string FirstDate = "2025-01-01";
    private const string SecondDate = "2025-03-02";
    private const string ThirdDate = "2025-12-10";
    private const string FourthDate = "2026-06-21";

    [Theory]
    [InlineData(FirstDate, SecondDate, ThirdDate, FourthDate, ThirdDate, SecondDate)] // No overlap
    [InlineData(FirstDate, ThirdDate, SecondDate, FourthDate, SecondDate, ThirdDate)] // Overlap inside
    [InlineData(FirstDate, SecondDate, FirstDate, SecondDate, FirstDate, SecondDate)] // Exact match
    [InlineData(null, null, null, null, null, null)] // Both unbounded
    [InlineData(null, null, ThirdDate, FourthDate, ThirdDate, FourthDate)] // First unbounded
    [InlineData(FirstDate, SecondDate, null, null, FirstDate, SecondDate)] // Second unbounded
    [InlineData(null, SecondDate, FirstDate, ThirdDate, FirstDate, SecondDate)] // First start unbounded
    [InlineData(FirstDate, ThirdDate, SecondDate, null, SecondDate, ThirdDate)] // Second end unbounded
    [InlineData(FirstDate, null, null, null, FirstDate, null)] // First end unbounded, Second unbounded
    [InlineData(null, null, null, FourthDate, null, FourthDate)] // First unbounded, Second end unbounded
    [InlineData(FirstDate, null, null, FourthDate, FirstDate, FourthDate)] // First end unbounded, Second start unbounded
    [InlineData(MinDate, MaxDate, FirstDate, FourthDate, FirstDate, FourthDate)] // Extreme bounds
    [InlineData(FirstDate, FourthDate, MinDate, MaxDate, FirstDate, FourthDate)] // Extreme bounds
    public void FilterIntersect_WithDateRanges_ReturnsExpectedIntersection(
        string? start, string? end,
        string? otherStart, string? otherEnd,
        string? expectedStart, string? expectedEnd)
    {
        // Arrange
        var dateStart = ParseDate(start);
        var dateEnd = ParseDate(end);
        var otherDateStart = ParseDate(otherStart);
        var otherDateEnd = ParseDate(otherEnd);
        var expectedDateStart = ParseDate(expectedStart);
        var expectedDateEnd = ParseDate(expectedEnd);

        var filter = new ConnectorPacketsFilterDto(dateStart, dateEnd);
        var otherFilter = new ConnectorPacketsFilterDto(otherDateStart, otherDateEnd);
        
        // Act
        var result = filter.Intersect(otherFilter);
        
        // Assert
        result.DateTimeStart.Should().Be(expectedDateStart);
        result.DateTimeEnd.Should().Be(expectedDateEnd);
    }

    [Theory]
    [InlineData(10L, 20L, 50L, 100L, 50L, 20L)] // No overlap (disjoint ranges)
    [InlineData(10L, 50L, 20L, 100L, 20L, 50L)] // Overlap inside (partial overlap)
    [InlineData(10L, 20L, 10L, 20L, 10L, 20L)] // Exact match (identical ranges)
    [InlineData(null, null, null, null, null, null)] // Both unbounded (no limits on either side)
    [InlineData(null, null, 50L, 100L, 50L, 100L)] // First unbounded, second bounded
    [InlineData(10L, 20L, null, null, 10L, 20L)] // First bounded, second unbounded
    [InlineData(null, 20L, 10L, 50L, 10L, 20L)] // First start unbounded (no lower bound)
    [InlineData(10L, 50L, 20L, null, 20L, 50L)] // Second end unbounded (no upper bound for second)
    [InlineData(10L, null, null, null, 10L, null)] // First end unbounded, second fully unbounded
    [InlineData(null, null, null, 100L, null, 100L)] // First unbounded, second end unbounded
    [InlineData(10L, null, null, 100L, 10L, 100L)] // First end unbounded, second start unbounded
    [InlineData(long.MinValue, long.MaxValue, 10L, 100L, 10L, 100L)] // Extreme bounds
    [InlineData(10L, 100L, long.MinValue, long.MaxValue, 10L, 100L)] // Extreme bounds
    public void FilterIntersect_WithIdRanges_ReturnsExpectedIntersection(
        long? min, long? max,
        long? otherMin, long? otherMax,
        long? expectedMin, long? expectedMax)
    {
        // Arrange
        var filter = new ConnectorPacketsFilterDto(MinId: min, MaxId: max);
        var otherFilter = new ConnectorPacketsFilterDto(MinId: otherMin, MaxId: otherMax);
        
        // Act
        var result = filter.Intersect(otherFilter);
        
        // Assert
        result.MinId.Should().Be(expectedMin);
        result.MaxId.Should().Be(expectedMax);
    }

    [Theory]
    [InlineData(null, null, null)] // Both unbounded (no channel restrictions)
    [InlineData(null, "A,B", "A,B")] // First unbounded, second restricted (yield second's channels)
    [InlineData("A,B", null, "A,B")] // First restricted, second unbounded (yield first's channels)
    [InlineData("A,B", "B,C", "B")] // Overlap in channels (common channel "B")
    [InlineData("A,B", "C,D", "")] // No overlap in channels (no common channel)
    [InlineData("A,B", "A,B", "A,B")] // Exact same channel set
    [InlineData("", "", "")] //Both filters allow no channels
    [InlineData("", "A,B", "")] // One filter allows no channels, other has some (no intersection)
    [InlineData("", null, "")] // One filter no channels, other unbounded (intersection is none)
    public void FilterIntersect_WithChannels_ReturnsExpectedIntersection(
        string? channels, string? otherChannels, string? expectedChannels)
    {
        // Arrange
        var channelSet = channels != "" ? channels?.Split(',').ToImmutableArray() : [];
        var otherChannelSet = otherChannels != "" ? otherChannels?.Split(',').ToImmutableArray() : [];
        var expectedSet = expectedChannels != "" ? expectedChannels?.Split(',').ToImmutableArray() : [];

        var filter = new ConnectorPacketsFilterDto(Channels: channelSet);
        var otherFilter = new ConnectorPacketsFilterDto(Channels: otherChannelSet);
        
        // Act
        var result = filter.Intersect(otherFilter);
        
        // Assert
        result.Channels.Should().BeEquivalentTo(expectedSet);
    }

    [Theory]
    [InlineData(FirstDate, SecondDate, ThirdDate, FourthDate, FirstDate, FourthDate)] // No overlap
    [InlineData(FirstDate, ThirdDate, SecondDate, FourthDate, FirstDate, FourthDate)] // Overlap inside
    [InlineData(FirstDate, SecondDate, FirstDate, SecondDate, FirstDate, SecondDate)] // Exact match
    [InlineData(null, null, null, null, null, null)] // Both unbounded
    [InlineData(null, null, ThirdDate, FourthDate, null, null)] // First unbounded
    [InlineData(FirstDate, SecondDate, null, null, null, null)] // Second unbounded
    [InlineData(null, SecondDate, FirstDate, ThirdDate, null, ThirdDate)] // First start unbounded
    [InlineData(FirstDate, ThirdDate, SecondDate, null, FirstDate, null)] // Second end unbounded
    [InlineData(FirstDate, null, null, null, null, null)] // First end unbounded, Second unbounded
    [InlineData(null, null, null, FourthDate, null, null)] // First unbounded, Second end unbounded
    [InlineData(FirstDate, null, null, FourthDate, null, null)] // First end unbounded, Second start unbounded
    [InlineData(MinDate, MaxDate, FirstDate, FourthDate, MinDate, MaxDate)] // Extreme bounds
    [InlineData(FirstDate, FourthDate, MinDate, MaxDate, MinDate, MaxDate)] // Extreme bounds
    public void FilterUnion_WithDateRanges_ReturnsExpectedUnion(
        string? start, string? end,
        string? otherStart, string? otherEnd,
        string? expectedStart, string? expectedEnd)
    {
        // Arrange
        var dateStart = ParseDate(start);
        var dateEnd = ParseDate(end);
        var otherDateStart = ParseDate(otherStart);
        var otherDateEnd = ParseDate(otherEnd);
        var expectedDateStart = ParseDate(expectedStart);
        var expectedDateEnd = ParseDate(expectedEnd);

        var filter = new ConnectorPacketsFilterDto(dateStart, dateEnd);
        var otherFilter = new ConnectorPacketsFilterDto(otherDateStart, otherDateEnd);
        
        // Act
        var result = filter.Union(otherFilter);
        
        // Assert
        result.DateTimeStart.Should().Be(expectedDateStart);
        result.DateTimeEnd.Should().Be(expectedDateEnd);
    }

    [Theory]
    [InlineData(10L, 20L, 50L, 100L, 10L, 100L)] // No overlap (disjoint ranges)
    [InlineData(10L, 50L, 20L, 100L, 10L, 100L)] // Overlap inside (partial overlap)
    [InlineData(10L, 20L, 10L, 20L, 10L, 20L)] // Exact match (identical ranges)
    [InlineData(null, null, null, null, null, null)] // Both unbounded (no limits on either side)
    [InlineData(null, null, 50L, 100L, null, null)] // First unbounded, second bounded
    [InlineData(10L, 20L, null, null, null, null)] // First bounded, second unbounded
    [InlineData(null, 20L, 10L, 50L, null, 50L)] // First start unbounded (no lower bound)
    [InlineData(10L, 50L, 20L, null, 10L, null)] // Second end unbounded (no upper bound for second)
    [InlineData(10L, null, null, null, null, null)] // First end unbounded, second fully unbounded
    [InlineData(null, null, null, 100L, null, null)] // First unbounded, second end unbounded
    [InlineData(10L, null, null, 100L, null, null)] // First end unbounded, second start unbounded
    [InlineData(long.MinValue, long.MaxValue, 10L, 100L, long.MinValue, long.MaxValue)] // Extreme bounds
    [InlineData(10L, 100L, long.MinValue, long.MaxValue, long.MinValue, long.MaxValue)] // Extreme bounds
    public void FilterUnion_WithIdRanges_ReturnsExpectedUnion(
        long? min, long? max,
        long? otherMin, long? otherMax,
        long? expectedMin, long? expectedMax)
    {
        // Arrange
        var filter = new ConnectorPacketsFilterDto(MinId: min, MaxId: max);
        var otherFilter = new ConnectorPacketsFilterDto(MinId: otherMin, MaxId: otherMax);
        
        // Act
        var result = filter.Union(otherFilter);
        
        // Assert
        result.MinId.Should().Be(expectedMin);
        result.MaxId.Should().Be(expectedMax);
    }

    [Theory]
    [InlineData(null, null, null)] // Both unbounded (remains unbounded)
    [InlineData(null, "A,B", null)] // One unbounded, one restricted (unbounded wins in union)
    [InlineData("A,B", null, null)] // One restricted, one unbounded (unbounded wins in union)
    [InlineData("A,B", "B,C", "A,B,C")] // Overlap in channels (union of sets)
    [InlineData("A,B", "C,D", "A,B,C,D")] // Disjoint channel sets (all channels from both)
    [InlineData("A,B", "A,B", "A,B")] // Exact same channel set (union is the same set)
    [InlineData("", "", "")] // Both allow no channels (union still no channels)
    [InlineData("", "A,B", "A,B")] // One allows no channels, other has some (union yields some)
    [InlineData("", null, null)] // One allows no channels, other unbounded (union is unbounded)
    public void FilterUnion_WithChannels_ReturnsExpectedUnion(
        string? channels, string? otherChannels, string? expectedChannels)
    {
        // Arrange
        var channelSet = channels != "" ? channels?.Split(',').ToImmutableArray() : [];
        var otherChannelSet = otherChannels != "" ? otherChannels?.Split(',').ToImmutableArray() : [];
        var expectedSet = expectedChannels != "" ? expectedChannels?.Split(',').ToImmutableArray() : [];

        var filter = new ConnectorPacketsFilterDto(Channels: channelSet);
        var otherFilter = new ConnectorPacketsFilterDto(Channels: otherChannelSet);
        
        // Act
        var result = filter.Union(otherFilter);
        
        // Assert
        result.Channels.Should().BeEquivalentTo(expectedSet);
    }

    private static DateTime? ParseDate(string? s) => s switch
    {
        null => null,
        "min" => DateTime.MinValue,
        "max" => DateTime.MaxValue,
        _ => DateTime.Parse(s)
    };
}

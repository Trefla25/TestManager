namespace eHub.PlugIn.Configuration;

/// <summary>
/// Provides fluent methods to configure Packet Transfer options.
/// </summary>
public interface IPacketTransferBuilder
{
    /// <summary>
    /// Sets the file system path to the database used for packet storage.
    /// </summary>
    /// <param name="path">The database file path.</param>
    /// <returns>This <see cref="IPacketTransferBuilder"/> for chaining.</returns>
    IPacketTransferBuilder SetDbPath(string path);
    /// <summary>
    /// Adds a SQLite pragma setting to the database connection.
    /// </summary>
    /// <param name="pragma">The name of the SQLite pragma to set.</param>
    /// <param name="value">The value to assign to the pragma.</param>
    /// <returns>This <see cref="IPacketTransferBuilder"/> for chaining.</returns>
    IPacketTransferBuilder AddSqlitePragma(string pragma, string value);
    /// <summary>
    /// Adds a channel group, identified by a unique name, and provides a fluent
    /// interface for further configuration.
    /// </summary>
    /// <param name="name">A unique name identifying the channel group.</param>
    /// <param name="configure">
    /// An action that receives an <see cref="IChannelGroupBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IPacketTransferBuilder"/> for chaining.</returns>
    IPacketTransferBuilder AddChannelGroup(string name, Action<IChannelGroupBuilder> configure);
}

/// <summary>
/// Provides fluent methods to configure a specific channel group.
/// </summary>
public interface IChannelGroupBuilder
{
    /// <summary>
    /// Configures this channel group to process packets concurrently.
    /// </summary>
    /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder UseConcurrentProcessing();
    /// <summary>
    /// Configures this channel group to process packets sequentially.
    /// </summary>
    /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder UseSequentialProcessing();
    /// <summary>
                                                       /// Sets how frequently the database is polled for new packets in this channel group.
                                                       /// </summary>
                                                       /// <param name="interval">The polling interval.</param>
                                                       /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder SetDbPollInterval(TimeSpan interval);
    /// <summary>
    /// Sets the maximum number of packets processed per cycle in this channel group.
    /// </summary>
    /// <param name="count">The number of packets to process each time.</param>
    /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder SetPacketsPerCycle(int count);
    /// <summary>
    /// Sets how frequently the cleaner process checks for packets that need to be removed.
    /// </summary>
    /// <param name="interval">The cleaner interval.</param>
    /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder SetCleanerInterval(TimeSpan interval);
    /// <summary>
    /// Adds a single channel to this group. Channels in the same group will be
    /// processed together, with the constraints defined in this group.
    /// </summary>
    /// <param name="channel">The channel identifier to add.</param>
    /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder AddChannel(string channel);
    /// <summary>
    /// Sets or replaces the list of channels that belong to this group. Channels
    /// should not overlap with other groups.
    /// </summary>
    /// <param name="channels">A collection of channel identifiers.</param>
    /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder SetChannels(IEnumerable<string> channels);
    /// <summary>
    /// Sets the default retention period for any packet status that doesn't have 
    /// a specific retention rule. If <see langword="null"/> is provided, all unspecified statuses 
    /// are kept indefinitely.
    /// </summary>
    /// <param name="retention">The default retention duration, or <see langword="null"/> to keep indefinitely.</param>
    /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder SetDefaultPacketRetention(TimeSpan? retention);
    /// <summary>
    /// Defines or updates a retention rule for a specific <see cref="PacketStatus"/>.
    /// If a retention period is set, packets with that status will be automatically removed
    /// after the specified duration. If <see langword="null"/> is provided, packets of this status 
    /// will be retained indefinitely.
    /// </summary>
    /// <param name="status">The packet status for which this rule applies.</param>
    /// <param name="retention">
    /// The duration to keep packets with the given status, or <see langword="null"/> to keep them indefinitely.
    /// </param>
    /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder AddPacketRetentionRule(PacketStatus status, TimeSpan? retention);
    /// <summary>
    /// Disables packet resending (reprocessing) for this channel group.
    /// By default, resending is enabled; call this method to prevent any packet from being resent.
    /// </summary>
    /// <returns>This <see cref="IChannelGroupBuilder"/> for chaining.</returns>
    IChannelGroupBuilder DisableResend();
}

using System.Runtime.Loader;
using System.Text;
using eHub.Config;
using eHub.Database;
using eHub.Database.Models;
using eHub.PlugIn;
using eHub.Scripting.Connectors;
using eHub.Scripting.Connectors.Db;
using eHub.Scripting.Connectors.Features;
using eHub.Scripting.Connectors.Metrics;
using eHub.Scripting.Connectors.Services;
using eHub.Tests.Helper;
using ElementLogic.Configuration.Client;
using eMessenger.Tests;
using ePlugin.Engine.Client;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace eHub.Tests.Connectors.PacketTransfer.Db;

[TestClass]
public class PacketRepositoryTests
{
    private static readonly PluginFiles EmptyPluginFiles = new(new NullFileProvider(), new NullFileProvider(), new NullFileProvider(), new NullFileProvider(), new NullFileProvider());
    private readonly PluginData _pluginData = new("DefaultTest", [], EmptyPluginFiles, NullLoggerFactory.Instance, AssemblyLoadContext.Default, NullPluginScopeDependencyResolver.Instance);

    private AwaitablePacketsConnector _testConnector = default!;
    private PacketTransferFeature _packetTransferCore = default!;

    private SqliteConnection _connection = default!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = default!;

    private List<Packet> _packets = [];

    [TestInitialize]
    public async Task TestInitialize()
    {
        // We need at least one connection to the database to keep it alive

        _connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await _connection.OpenAsync();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(_connection);
        var messenger = TestingMessenger.CreateScoped();
        _testConnector = new AwaitablePacketsConnector();

        var registry = Substitute.For<IEffortlessConfigurationRegistry>();
        var metadata = new ConnectorMetadata(new(), "Test", "TestConnector");
        var connectorTemplate = new ConnectorTemplate()
        {
            Enabled = true,
            Type = "TestConnector",
            PacketTransfer = new()
            {
                DbPath = _connection.DataSource,
                ChannelGroups = { }
            }
        };

        var packetDtoService = new PacketDtoService(NullLogger<PacketDtoService>.Instance, _testConnector, metadata, connectorTemplate);
        var filterRunnerProvider = Substitute.For<IFilterRunnerProvider>();
        var metrics = new ConnectorMetrics(Mocks.MeterFactory, metadata);

        _packetTransferCore = new PacketTransferFeature(
            loggerFactory.CreateLogger<PacketTransferFeature>(),
            _dbContextFactory,
            messenger,
            _testConnector,
            filterRunnerProvider,
            registry,
            packetDtoService,
            metadata,
            connectorTemplate,
            _pluginData,
            metrics);
        _testConnector.PacketRepository = new PacketRepository(_packetTransferCore);

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public async Task Create_SingleAdd_Success()
    {
        var samplePacket = new PacketData(Encoding.UTF8.GetBytes("SamplePacket"), "B", PacketStatus.InProgress, 12);

        var timeBefore = DateTime.Now;

        await _testConnector.PacketRepository.Create().Add(samplePacket).CreateAsync();

        var timeAfter = DateTime.Now;

        // Query the in-memory database
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var packetsInDb = await dbContext.Packet.ToArrayAsync();

        //Test that the data added in the Db correpsonds to the expectations
        packetsInDb.Should().NotHaveCount(0, "The packet was not added in the Db");

        packetsInDb[0].Id.Should().NotBe(0, "Packet should not have Id equal to 0 after being added to the Db");
        packetsInDb[0].BinaryData.SequenceEqual(samplePacket.BinaryData.ToArray()).Should().BeTrue("The binary data doesn't match after being added to the Db");
        packetsInDb[0].Metadata.Should().BeNull("Packet should have an empty Metadata before being added to the Db");
        packetsInDb[0].Channel.Should().Be(samplePacket.Channel, "Packet Channel doesn't match after being added to the Db");
        packetsInDb[0].Status.Should().Be(samplePacket.Status, "Packet Status doesn't match after being added to the Db");
        packetsInDb[0].DynamicField.Should().BeNull("Packet should have an empty DynamicField after being added to the Db");
        packetsInDb[0].ParentId.Should().Be(samplePacket.ParentId, "Packet ParentId doesn't match after being added to the Db");
        packetsInDb[0].RetryCount.Should().Be(0, "Packet should have a RetryCount equal to 0 after being added to the Db");
        (packetsInDb[0].DateCreated >= timeBefore && packetsInDb[0].DateCreated <= timeAfter).Should().BeTrue("Packet should be created in the Dd recently");
        packetsInDb[0].DateChanged.Should().BeNull("Packet should have an empty DateChanged after being added to the Db");
    }

    [TestMethod]
    public async Task Create_AddRange_Success()
    {
        var packets = new PacketData[]
        {
            new(Encoding.UTF8.GetBytes("Packet1"), "B", PacketStatus.InProgress, 12),
            new(Encoding.UTF8.GetBytes("Packet2"), "A", PacketStatus.Enqueued, 32),
            new(Encoding.UTF8.GetBytes("Packet3"), "A", PacketStatus.FatalError, 12)
        };

        var timeBefore = DateTime.Now;

        await _testConnector.PacketRepository.Create().AddRange(packets.AsEnumerable()).CreateAsync();

        var timeAfter = DateTime.Now;

        // Query the in-memory database
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var packetsInDb = await dbContext.Packet.ToArrayAsync();

        //Test that the packets were added as expected
        packetsInDb.Should().HaveCount(3, "The packets were not added correctly into the Db");

        for (var i = 0; i < packetsInDb.Length; i++)
        {
            var expected = packets[i];
            var actual = packetsInDb[i];

            actual.Id.Should().NotBe(0, $"Packet {i} should not have an Id equal to 0 after being added to the Db");
            actual.BinaryData.SequenceEqual(expected.BinaryData.ToArray()).Should().BeTrue($"Packet {i} BinaryData does not match after being added to the Db");
            actual.Metadata.Should().BeNull($"Packet {i} should have an empty Metadata after being added to the Db");
            actual.Channel.Should().Be(expected.Channel, $"Packet {i} Channel doesn't match after being added to the Db");
            actual.Status.Should().Be(expected.Status, $"Packet {i} Status does not match after being added to the Db");
            actual.DynamicField.Should().BeNull($"Packet {i} should have an empty DynamicField after being added to the Db");
            actual.ParentId.Should().NotBeNull($"Packet {i} ParentId does not match after being added to the Db");
            actual.RetryCount.Should().Be(0, $"Packet {i} should have a RetryCount equal to 0 after being added to the Db");
            (actual.DateCreated >= timeBefore && actual.DateCreated <= timeAfter).Should().BeTrue($"Packet {i} was not created recently");
            actual.DateChanged.Should().BeNull($"Packet {i} should have an empty DateChanged after being added to the Db");
        }
    }

    [TestMethod]
    public async Task Query_NoPacketsWithCondition_NoResults()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.SaveChangesAsync();

        var packetsInDb = await _testConnector.PacketRepository
            .Query()
            .Where(p => p.Id == 2)
            .GetAsync();

        packetsInDb.Should().BeEmpty("Query should have not returned anything");

        packetsInDb.Should().Equal([]);
    }

    [TestMethod]
    public async Task Query_WithBadCondition_NoResults()
    {
        _packets =
        [
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet0"), Channel = "A", Status = PacketStatus.InProgress, ParentId = 12 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.FatalError, ParentId = 1 }
        ];

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddRangeAsync(_packets);
        await dbContext.SaveChangesAsync();

        var packetsInDb = await _testConnector.PacketRepository
                                   .Query()
                                   .Where(p => p.Channel == "C")
                                   .GetAsync();

        packetsInDb.Should().BeEmpty("The Db query should not return anything");

        packetsInDb.Should().Equal([]);
    }

    [TestMethod]
    public async Task Query_WithCondition_SingleResult()
    {
        _packets =
        [
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet0"), Channel = "A", Status = PacketStatus.InProgress, ParentId = 12 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.FatalError, ParentId = 1 }
        ];

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddRangeAsync(_packets);
        await dbContext.SaveChangesAsync();

        var packetsInDb = await _testConnector.PacketRepository
                                   .Query()
                                   .Where(p => p.Channel == "A")
                                   .GetAsync();

        packetsInDb.Should().ContainSingle("The query did not return the expected number of packets");

        packetsInDb[0].Channel.Should().Be(_packets[0].Channel, "Packet Channel does not match after being added to the Db");
        _packets[0].BinaryData.SequenceEqual(packetsInDb[0].BinaryData.ToArray()).Should().BeTrue("Packet BinaryData does not match after being added to the Db");
        packetsInDb[0].Status.Should().Be(_packets[0].Status, "Packet Status doesn't match after being added to the Db");
        packetsInDb[0].ParentId.Should().Be(_packets[0].ParentId, "Packet ParentId doesn't match after being added to the Db");
    }

    [TestMethod]
    public async Task Query_WithCondition_MultipleResults()
    {
        _packets =
        [
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet0"), Channel = "A", Status = PacketStatus.InProgress, ParentId = 12 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "A", Status = PacketStatus.FatalError, ParentId = 1 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet2"), Channel = "A", Status = PacketStatus.InProgress, ParentId = 123 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet3"), Channel = "B", Status = PacketStatus.Processed, ParentId = 33 }
        ];

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        dbContext.Packet.AddRange(_packets);
        await dbContext.SaveChangesAsync();

        var packetsInDb = await _testConnector.PacketRepository
                                   .Query()
                                   .Where(p => p.Channel == "A")
                                   .GetAsync();

        packetsInDb = [.. packetsInDb.OrderBy(p => p.Id)];
        packetsInDb.Should().HaveCount(3, "The query did not return the expected number of packets");

        for (var i = 0; i < packetsInDb.Length; i++)
        {
            var expected = _packets[i];
            var actual = packetsInDb[i];

            expected.BinaryData.SequenceEqual(actual.BinaryData.ToArray()).Should().BeTrue($"Packet {i} BinaryData does not match after being added to the Db");
            actual.Channel.Should().Be(expected.Channel, $"Packet {i} Channel does not match after being added to the Db");
            actual.Status.Should().Be(expected.Status, $"Packet {i} Status does not match after being added to the Db");
            actual.ParentId.Should().Be(expected.ParentId, $"Packet {i} ParentId does not match after being added to the Db");
        }
    }

    [TestMethod]
    public async Task Query_NoPackets_NoResults()
    {
        _packets = [];

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddRangeAsync(_packets);
        await dbContext.SaveChangesAsync();

        var packetsInDb = await _testConnector.PacketRepository
                                   .Query()
                                   .GetAsync();

        packetsInDb.Should().BeEmpty("Query should have not returned anything");

        packetsInDb.Should().Equal([]);
    }

    [TestMethod]
    public async Task Query_WithoutCondition_AllPacketsReturned()
    {
        _packets =
        [
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet0"), Channel = "B", Status = PacketStatus.InProgress, ParentId = 12 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "A", Status = PacketStatus.FatalError, ParentId = 1 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet2"), Channel = "C", Status = PacketStatus.Enqueued, ParentId = 123 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet3"), Channel = "A", Status = PacketStatus.Processed, ParentId = 33 }
        ];

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        dbContext.Packet.AddRange(_packets);
        await dbContext.SaveChangesAsync();

        var packetsInDb = await _testConnector.PacketRepository
                                   .Query()
                                   .GetAsync();

        packetsInDb.Should().HaveCount(4, "Query did not return all packets from the Db");

        for (var i = 0; i < packetsInDb.Length; i++)
        {
            var expected = _packets[i];
            var actual = packetsInDb[i];

            expected.BinaryData.SequenceEqual(actual.BinaryData.ToArray()).Should().BeTrue($"Packet {i} BinaryData does not match after being added to the Db");
            actual.Channel.Should().Be(expected.Channel, $"Packet {i} Channel does not match after being added to the Db");
            actual.Status.Should().Be(expected.Status, $"Packet {i} Status does not match after being added to the Db");
            actual.ParentId.Should().Be(expected.ParentId, $"Packet {i} ParentId does not match after being added to the Db");
        }
    }

    [TestMethod]
    public async Task Update_Id_Success()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Processed, ParentId = 33 });

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.Id, 2)
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 2)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate.Should().NotBeNull("Packet id was not updated");

        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packets BinaryData before and after Update of Id does not match");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packets Channel before and after Update of Id does not match");
        packetsAfterUpdate[0].Status.Should().Be(packetsBeforeUpdate[0].Status, "Packets Status before and after Update of Id does not match");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packets ParentId before and after Update of Id does not match");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packets RetryCount before and after Update of Id does not match");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packets DateCreated before and after Update of Id does not match");
    }

    [TestMethod]
    public async Task Update_BinaryData_Fails()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "A", Status = PacketStatus.Processed, ParentId = 33 });

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        dbContext.Packet.Add(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        var action = async () => await _testConnector.PacketRepository.Update()
                .Set(p => p.BinaryData, Encoding.UTF8.GetBytes("Packet22"))
                .Where(p => p.Id == 1)
                .ExecuteAsync();

        await action.Should().ThrowAsync<InvalidOperationException>();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packet Id before and after Update of BinaryData does not match");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packet Channel before and after Update of BinaryData does not match");
        packetsAfterUpdate[0].Status.Should().Be(packetsBeforeUpdate[0].Status, "Packet Status before and after Update of BinaryData does not match");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packet ParentId before and after Update of BinaryData does not match");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packet RetryCount before and after Update of BinaryData does not match");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packet DateCreated before and after Update of BinaryData does not match");

        packetsAfterUpdate[0].BinaryData.SequenceEqual([.. Encoding.UTF8.GetBytes("Packet22")]).Should().BeFalse("Packet BinaryData should not have been updated");
    }

    [TestMethod]
    public async Task Update_Metadata_Success()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Processed, ParentId = 33 });
        _packets[0].Metadata = "Before";

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        var x = await _testConnector.PacketRepository.Update()
            .Set(p => p.Metadata, "After")
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packets Id before and after Update of Metadata does not match");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packets BinaryData before and after Update of Metadata does not match");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packets Channel before and after Update of Metadata does not match");
        packetsAfterUpdate[0].Status.Should().Be(packetsBeforeUpdate[0].Status, "Packets Status before and after Update of Metadata does not match");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packets ParentId before and after Update of Metadata does not match");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packets RetryCount before and after Update of Metadata does not match");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packets DateCreated before and after Update of Metadata does not match");

        packetsAfterUpdate[0].Metadata.Should().Be("After", "Packet Metadata was not updated");
    }

    [TestMethod]
    public async Task Update_Status_Success()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Enqueued, ParentId = 33 });

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.Status, PacketStatus.Processed)
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packets Id before and after Update of Status does not match");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packets BinaryData before and after Update of Status does not match");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packets Channel before and after Update of Status does not match");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packets ParentId before and after Update of Status does not match");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packets RetryCount before and after Update of Status does not match");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packets DateCreated before and after Update of Status does not match");

        packetsAfterUpdate[0].Status.Should().Be(PacketStatus.Processed, "Packet Status was not updated");
    }

    [TestMethod]
    public async Task Update_Channel_Success()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Processed, ParentId = 33 });

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.Channel, "A")
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packet Id before and after Update of Channel does not match");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packet BinaryData before and after Update of Channel does not match");
        packetsAfterUpdate[0].Status.Should().Be(packetsBeforeUpdate[0].Status, "Packet Status before and after Update of Channel does not match");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packet ParentId before and after Update of Channel does not match");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packet RetryCount before and after Update of Channel does not match");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packet DateCreated before and after Update of Channel does not match");

        packetsAfterUpdate[0].Channel.Should().Be("A", "Packet Channel was not updated");
    }

    [TestMethod]
    public async Task Update_DynamicField_Success()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Processed, ParentId = 33 });
        _packets[0].DynamicField = "Before";

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.DynamicField, "After")
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packets Id before and after Update of DynamicField does not match");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packets BinaryData before and after Update of DynamicField does not match");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packets Channel before and after Update of DynamicField does not match");
        packetsAfterUpdate[0].Status.Should().Be(packetsBeforeUpdate[0].Status, "Packets Status before and after Update of IDynamicFieldd does not match");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packets ParentID before and after Update of DynamicField does not match");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packets RetryCount before and after Update of IDynamicFieldd does not match");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packets DateCreated before and after Update of DynamicField does not match");

        packetsAfterUpdate[0].DynamicField.Should().Be("After", "Packet DynamicField was not updated");
    }

    [TestMethod]
    public async Task Update_ParentId_Success()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Processed, ParentId = 33 });

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        var x = await _testConnector.PacketRepository.Update()
            .Set(p => p.ParentId, 2)
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packets Id before and after Update of ParentId does not match");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packets BinaryData before and after Update of ParentId does not match");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packets Channel before and after Update of ParentId does not match");
        packetsAfterUpdate[0].Status.Should().Be(packetsBeforeUpdate[0].Status, "Packets Status before and after Update of ParentId does not match");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packets RetryCount before and after Update of ParentId does not match");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packets DateCreated before and after Update of ParentId does not match");

        packetsAfterUpdate[0].ParentId.Should().Be(2, "ParentId was not updated");
    }

    [TestMethod]
    public async Task Update_RetryCount_Success()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Processed, ParentId = 33 });

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.RetryCount, 2)
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packets Id before and after Update of RetryCount does not match");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packets BinaryData before and after Update of RetryCount does not match");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packets Channel before and after Update of RetryCount does not match");
        packetsAfterUpdate[0].Status.Should().Be(packetsBeforeUpdate[0].Status, "Packets Status before and after Update of RetryCount does not match");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packets ParentId before and after Update of RetryCount does not match");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packets DateCreated before and after Update of RetryCount does not match");

        packetsAfterUpdate[0].RetryCount.Should().Be(2, "Packet RetryCount was not updated");
    }

    [TestMethod]
    public async Task Update_DateCreated_Fails()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Processed, ParentId = 33 });
        _packets[0].DateCreated = DateTime.Now;

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        var action = async () => await _testConnector.PacketRepository.Update()
            .Set(p => p.DateCreated, DateTime.MinValue)
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        await action.Should().ThrowAsync<InvalidOperationException>();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packets Id before and after Update of DateCreated does not match");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packets BinaryData before and after Update of DateCreated does not match");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packets Channel before and after Update of DateCreated does not match");
        packetsAfterUpdate[0].Status.Should().Be(packetsBeforeUpdate[0].Status, "Packets Status before and after Update of DateCreated does not match");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packets ParentID before and after Update of DateCreated does not match");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packets RetryCount before and after Update of DateCreated does not match");

        packetsAfterUpdate[0].DateChanged.Should().NotBe(DateTime.MinValue, "Packet DateCreated should not have been updated");
    }

    [TestMethod]
    public async Task Update_DateChanged_Fails()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Processed, ParentId = 33 });
        _packets[0].DateChanged = DateTime.Now;


        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        var action = async () => await _testConnector.PacketRepository.Update()
            .Set(p => p.DateChanged, DateTime.MinValue)
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        await action.Should().ThrowAsync<InvalidOperationException>();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packet Id before and after Update of DateChanged does not match");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packet BinaryData before and after Update of DateChanged does not match");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packet Channel before and after Update of DateChanged does not match");
        packetsAfterUpdate[0].Status.Should().Be(packetsBeforeUpdate[0].Status, "Packet Status before and after Update of DateChanged does not match");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packet ParentId before and after Update of DateChanged does not match");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packet RetryCount before and after Update of DateChanged does not match");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packet DateCreated before and after Update of DateChanged does not match");

        packetsAfterUpdate[0].DateChanged.Should().NotBe(DateTime.MinValue, "Packet DateChanged should not have been updated");
    }

    [TestMethod]
    public async Task Update_AllFields_Success()
    {
        _packets.Add(new Packet { BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "B", Status = PacketStatus.Enqueued, ParentId = 33 });
        _packets[0].Metadata = "Before";
        _packets[0].DynamicField = "Before";
        _packets[0].DateCreated = DateTime.Now;
        _packets[0].DateChanged = DateTime.Now;

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddAsync(_packets[0]);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.Metadata, "After")
            .Set(p => p.Channel, "A")
            .Set(p => p.Status, PacketStatus.Processed)
            .Set(p => p.DynamicField, "After")
            .Set(p => p.ParentId, 2)
            .Set(p => p.RetryCount, 2)
            .Where(p => p.Id == 1)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Id == 1)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packet Id should not have been updated");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packet BinaryData should not have been updated");
        packetsAfterUpdate[0].Metadata.Should().Be("After", "Packet Metadata was not updated");
        packetsAfterUpdate[0].Channel.Should().Be("A", "Packet Channel was not updated");
        packetsAfterUpdate[0].Status.Should().Be(PacketStatus.Processed, "Packet Status was not updated");
        packetsAfterUpdate[0].DynamicField.Should().Be("After", "Packet DynamicField was not updated");
        packetsAfterUpdate[0].ParentId.Should().Be(2, "Packet ParentId was not updated");
        packetsAfterUpdate[0].RetryCount.Should().Be(2, "Packet RetryCount was not updated");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packet DateCreated should not have been updated");
        packetsAfterUpdate[0].DateChanged.Should().BeOnOrAfter(packetsBeforeUpdate[0].DateChanged!.Value, "Packet DateChanged should not have been updated");
    }

    [TestMethod]
    public async Task Update_NoPacketsWithoutCondition_Unchanged()
    {
        _packets = [];

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddRangeAsync(_packets);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.Status, PacketStatus.Processed)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate.Should().BeEmpty("There should be no packets to be updated since the Db is empty");
    }

    [TestMethod]
    public async Task Update_WithoutCondition_AllPacketsUpdated()
    {
        _packets =
        [
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet0"), Channel = "A", Status = PacketStatus.InProgress, ParentId = 12 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "A", Status = PacketStatus.Enqueued, ParentId = 1 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet2"), Channel = "D", Status = PacketStatus.FatalError, ParentId = 123 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet3"), Channel = "C", Status = PacketStatus.Processed, ParentId = 33 }
        ];

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddRangeAsync(_packets);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.Status, PacketStatus.Processed)
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsBeforeUpdate.Should().HaveCount(_packets.Count, "The packets to be updated should be all, since there is no Where");
        packetsAfterUpdate.Should().HaveCount(_packets.Count, "The number of packets modified should be the same as the ampunt of total packets in the Db");

        for (var i = 0; i < packetsAfterUpdate.Length; i++)
        {
            packetsAfterUpdate[i].Status.Should().Be(PacketStatus.Processed, $"Packet {i} Status was not updated");

            packetsAfterUpdate[i].Id.Should().Be(packetsBeforeUpdate[i].Id, $"Packet {i} Id before and after Update of Status does not match");
            packetsBeforeUpdate[i].BinaryData.SequenceEqual(packetsAfterUpdate[i].BinaryData).Should().BeTrue($"Packet {i} BinaryData before and after Update of Status does not match");
            packetsAfterUpdate[i].Channel.Should().Be(packetsBeforeUpdate[i].Channel, $"Packet {i} Channel before and after Update of Status does not match");
            packetsAfterUpdate[i].ParentId.Should().Be(packetsBeforeUpdate[i].ParentId, $"Packet {i} ParentId before and after Update of Status does not match");
            packetsAfterUpdate[i].RetryCount.Should().Be(packetsBeforeUpdate[i].RetryCount, $"Packet {i} RetryCount before and after Update of Status does not match");
            packetsAfterUpdate[i].DateCreated.Should().Be(packetsBeforeUpdate[i].DateCreated, $"Packet {i} DateCreated before and after Update of Status does not match");
        }
    }

    [TestMethod]
    public async Task Update_WithConditionAndNoSet_Unchanged()
    {
        _packets =
        [
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet0"), Channel = "A", Status = PacketStatus.InProgress, ParentId = 12 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "A", Status = PacketStatus.Enqueued, ParentId = 1 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet2"), Channel = "D", Status = PacketStatus.FatalError, ParentId = 123 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet3"), Channel = "C", Status = PacketStatus.Processed, ParentId = 33 }
        ];

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddRangeAsync(_packets);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Channel == "A")
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository
            .Update()
            .Where(p => p.Channel == "A")
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Channel == "A")
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        for (var i = 0; i < packetsAfterUpdate.Length; i++)
        {
            var expected = packetsBeforeUpdate[i];
            var actual = packetsAfterUpdate[i];

            actual.Status.Should().Be(expected.Status, $"Packet {i} Status was updated");
            actual.Id.Should().Be(expected.Id, $"Packet {i} Id was updated");
            expected.BinaryData.SequenceEqual(actual.BinaryData).Should().BeTrue($"Packet {i} BinaryData was updated");
            actual.Channel.Should().Be(expected.Channel, $"Packet {i} Channel was updated");
            actual.ParentId.Should().Be(expected.ParentId, $"Packet {i}ParentID was updated");
            actual.RetryCount.Should().Be(expected.RetryCount, $"Packet {i} RetryCount was updated");
            actual.DateCreated.Should().Be(expected.DateCreated, $"Packet {i} DateCreated was updated");
        }
    }

    [TestMethod]
    public async Task Update_WithCondition_NoUpdates()
    {
        _packets = [];

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddRangeAsync(_packets);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Channel == "A")
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.Status, PacketStatus.Processed)
            .Where(p => p.Channel == "A")
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Channel == "A")
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsBeforeUpdate.Should().Equal([], "There should be no packets that have to be updated, since the Db is empty");
        packetsAfterUpdate.Should().Equal([], "The result of Updating no packets should be empty");
    }

    [TestMethod]
    public async Task Update_WithCondition_SingleUpdate()
    {
        _packets =
        [
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet0"), Channel = "B", Status = PacketStatus.InProgress, ParentId = 12 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "A", Status = PacketStatus.FatalError, ParentId = 1 }
        ];

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddRangeAsync(_packets);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Channel == "A")
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.Status, PacketStatus.Processed)
            .Where(p => p.Channel == "A")
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Channel == "A")
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        packetsAfterUpdate[0].Status.Should().Be(PacketStatus.Processed, "Status was not updated for the extracted packet");

        packetsAfterUpdate[0].Id.Should().Be(packetsBeforeUpdate[0].Id, "Packet Id before and after Update of Status does not match.");
        packetsBeforeUpdate[0].BinaryData.SequenceEqual(packetsAfterUpdate[0].BinaryData).Should().BeTrue("Packet BinaryData before and after Update of Status does not match.");
        packetsAfterUpdate[0].Channel.Should().Be(packetsBeforeUpdate[0].Channel, "Packet Channel before and after Update of Status does not match.");
        packetsAfterUpdate[0].ParentId.Should().Be(packetsBeforeUpdate[0].ParentId, "Packet ParentID before and after Update of Status does not match.");
        packetsAfterUpdate[0].RetryCount.Should().Be(packetsBeforeUpdate[0].RetryCount, "Packet RetryCount before and after Update of Status does not match.");
        packetsAfterUpdate[0].DateCreated.Should().Be(packetsBeforeUpdate[0].DateCreated, "Packet DateCreated before and after Update of Status does not match.");
    }

    [TestMethod]
    public async Task Update_WithCondition_MultipleUpdates()
    {
        _packets =
        [
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet0"), Channel = "A", Status = PacketStatus.InProgress, ParentId = 12 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet1"), Channel = "A", Status = PacketStatus.Enqueued, ParentId = 1 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet2"), Channel = "D", Status = PacketStatus.FatalError, ParentId = 123 },
            new Packet{ BinaryData = Encoding.UTF8.GetBytes("Packet3"), Channel = "C", Status = PacketStatus.Processed, ParentId = 33 }
        ];

        var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await dbContext.Packet.AddRangeAsync(_packets);
        await dbContext.SaveChangesAsync();

        var packetsBeforeUpdate = await dbContext
            .Packet
            .Where(p => p.Channel == "A")
            .OrderBy(p => p.Id)
            .ToArrayAsync();

        await dbContext.DisposeAsync();

        await _testConnector.PacketRepository.Update()
            .Set(p => p.Status, PacketStatus.Processed)
            .Where(p => p.Channel == "A")
            .ExecuteAsync();

        dbContext = await _dbContextFactory.CreateDbContextAsync();

        var packetsAfterUpdate = await dbContext
            .Packet
            .Where(p => p.Channel == "A")
            .ToArrayAsync();

        packetsAfterUpdate = [.. packetsAfterUpdate.OrderBy(p => p.Id)];
        await dbContext.DisposeAsync();

        for (var i = 0; i < packetsAfterUpdate.Length; i++)
        {
            packetsAfterUpdate[i].Status.Should().Be(PacketStatus.Processed, $"Packet {i} Status was not updated");

            packetsAfterUpdate[i].Id.Should().Be(packetsBeforeUpdate[i].Id, $"Packet {i} Id before and after Update of Status does not match.");
            packetsBeforeUpdate[i].BinaryData.SequenceEqual(packetsAfterUpdate[i].BinaryData).Should().BeTrue($"Packet {i} BinaryData before and after Update of Status does not match.");
            packetsAfterUpdate[i].Channel.Should().Be(packetsBeforeUpdate[i].Channel, $"Packet {i} Channel before and after Update of Status does not match.");
            packetsAfterUpdate[i].ParentId.Should().Be(packetsBeforeUpdate[i].ParentId, $"Packet {i} ParentID before and after Update of Status does not match.");
            packetsAfterUpdate[i].RetryCount.Should().Be(packetsBeforeUpdate[i].RetryCount, $"Packet {i} RetryCount before and after Update of Status does not match.");
            packetsAfterUpdate[i].DateCreated.Should().Be(packetsBeforeUpdate[i].DateCreated, $"Packet {i} DateCreated before and after Update of Status does not match.");
        }
    }

    [TestCleanup]
    public async Task TestCleaup()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}

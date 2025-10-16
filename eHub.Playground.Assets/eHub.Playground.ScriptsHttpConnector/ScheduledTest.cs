using eScheduler.Library.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eHub.Playground.ScriptsHttpConnector;
public class ScheduledTest(ILogger<ScheduledTest> logger) : IScheduledTask
{
    public ConcurrentDictionary<string, object> SharedData { get; set; } = [];

    public Task ExecuteAsync(CancellationToken? cancellationToken = null)
    {
        logger.LogInformation("Logging task executed. 2");
        return Task.CompletedTask;
    }
}

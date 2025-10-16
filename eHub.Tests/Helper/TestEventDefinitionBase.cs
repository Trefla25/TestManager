using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace eHub.Tests.Helper;

public class TestEventDefinitionBase(
    ILoggingOptions loggingOptions,
    EventId eventId,
    LogLevel level,
    string eventIdCode)
    : EventDefinitionBase(loggingOptions, eventId, level, eventIdCode)
{
}

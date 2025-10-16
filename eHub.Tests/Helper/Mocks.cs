using System.Diagnostics.Metrics;

namespace eHub.Tests.Helper;

public static class Mocks
{
    public static IMeterFactory MeterFactory { get; } = new MockMeterFactory();

    private class MockMeterFactory : IMeterFactory
    {
        public void Dispose() { }
        public Meter Create(MeterOptions options) => new Meter(options);
    }
}

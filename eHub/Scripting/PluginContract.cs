using System.Runtime.CompilerServices;
using eHub.PlugIn;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace eHub.Scripting;

public static class PluginContract
{
    public static void EnsureContractAssemblyLoaded()
    {
        // Load IConnector interface
        new DummyContract().Run(default).Wait();
        // Load InjectAttribute
        Void(new InjectAttribute());
        // Load IOptions
        Void(Options.Create<object>(null!));
        // Get Assembly Microsoft.AspNetCore.Http.Results loaded
        Void(Results.Ok());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Void<T>(T _) { }

#pragma warning disable CS0067
    private class DummyContract : IConnector
    {
        event UpdateStatusDelegate? IConnector.UpdateStatus { add { } remove { } }
        public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
    }
#pragma warning restore CS0067
}

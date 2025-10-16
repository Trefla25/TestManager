namespace eHub.PlugIn;

/// <summary>
/// Marks the class or constructor parameter as the target to be mapped from the Connector config.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class ScriptOptionsAttribute : Attribute { }

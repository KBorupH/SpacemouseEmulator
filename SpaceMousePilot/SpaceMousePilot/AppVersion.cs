using System.Reflection;

namespace SpaceMousePilot;

public static class AppVersion
{
    /// <summary>
    /// Reads the version from the assembly's InformationalVersion attribute,
    /// which is set from the Version property in the .csproj.
    /// </summary>
    public static string Current { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            .Split('+')[0]  // strip git commit hash dotnet appends
        ?? "0.0.0";

}

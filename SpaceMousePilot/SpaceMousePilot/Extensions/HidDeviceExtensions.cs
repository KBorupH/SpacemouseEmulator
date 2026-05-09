using HidSharp;

namespace SpaceMousePilot.Extensions;

internal static class HidDeviceExtensions
{
    internal static string SafeName(this HidDevice d)
    {
        try
        {
            return d.GetFriendlyName();
        }
        catch
        {
            return "<unknown>";
        }
    }
}

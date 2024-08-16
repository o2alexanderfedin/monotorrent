// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
namespace Ozone.MonoTorrent.ConsoleApp;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class ActionAttribute (string title, ConsoleKey key, bool isDelimiter = false)
    : Attribute
{
    public bool IsDelimiter => isDelimiter;
    public string Title => title;
    public ConsoleKey Key => key;

    public ActionAttribute ()
        : this ("", ConsoleKey.None, isDelimiter: true)
    {
    }
}

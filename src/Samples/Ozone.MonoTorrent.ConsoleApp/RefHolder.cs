// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
namespace Ozone.MonoTorrent.ConsoleApp;

public sealed class RefHolder<T>
{
    public T Target { get; set; } = default!;
}

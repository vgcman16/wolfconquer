using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace WolfConquer.Core.Packets;

public delegate ValueTask PacketHandler<in TContext>(
    TContext context,
    PacketFrame frame,
    CancellationToken cancellationToken);

public sealed class PacketHandlerRegistry<TContext>
{
    private readonly Dictionary<ushort, PacketHandler<TContext>> _handlers;

    private PacketHandlerRegistry(Dictionary<ushort, PacketHandler<TContext>> handlers)
    {
        _handlers = handlers;
    }

    public int Count => _handlers.Count;

    public IEnumerable<ushort> Opcodes => _handlers.Keys;

    public static PacketHandlerRegistry<TContext> Empty { get; } = new(new Dictionary<ushort, PacketHandler<TContext>>());

    public static PacketHandlerRegistry<TContext> FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return FromTypes(assembly.GetTypes());
    }

    public static PacketHandlerRegistry<TContext> FromTypes(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        var handlers = new Dictionary<ushort, PacketHandler<TContext>>();
        foreach (var type in types)
        {
            ArgumentNullException.ThrowIfNull(type);
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attribute = method.GetCustomAttribute<PacketHandlerAttribute>(inherit: false);
                if (attribute is null)
                {
                    continue;
                }

                var handler = CreateHandler(method, attribute.Opcode);
                if (!handlers.TryAdd(attribute.Opcode, handler))
                {
                    throw new InvalidOperationException(
                        $"Multiple packet handlers are registered for opcode {attribute.Opcode}.");
                }
            }
        }

        return new PacketHandlerRegistry<TContext>(handlers);
    }

    public bool TryGetHandler(ushort opcode, [NotNullWhen(true)] out PacketHandler<TContext>? handler)
    {
        return _handlers.TryGetValue(opcode, out handler);
    }

    public ValueTask HandleAsync(
        TContext context,
        PacketFrame frame,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetHandler(frame.Header.Opcode, out var handler))
        {
            throw new InvalidOperationException($"No packet handler is registered for opcode {frame.Header.Opcode}.");
        }

        return handler(context, frame, cancellationToken);
    }

    private static PacketHandler<TContext> CreateHandler(MethodInfo method, ushort opcode)
    {
        try
        {
            var handler = method.CreateDelegate<PacketHandler<TContext>>();
            return (context, frame, cancellationToken) => context is IPacketHandlerInvocation<TContext> invocation
                ? invocation.InvokeHandlerAsync(handler, frame, cancellationToken)
                : handler(context, frame, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Packet handler for opcode {opcode} must match signature ValueTask Handler({typeof(TContext).Name}, PacketFrame, CancellationToken).",
                exception);
        }
    }
}

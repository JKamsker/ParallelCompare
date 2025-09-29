using Spectre.Console.Cli;
using System.Diagnostics.CodeAnalysis;

namespace FsEqual.Tool.Infrastructure;

internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly Dictionary<Type, Func<object>> _registrations = new();

    public ITypeResolver Build() => new TypeResolver(_registrations);

    public void Register(Type service, Type implementation)
    {
        _registrations[service] = () => CreateInstance(implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _registrations[service] = () => implementation;
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _registrations[service] = factory;
    }

    [return: NotNull]
    private static object CreateInstance(Type type)
    {
        return Activator.CreateInstance(type)
               ?? throw new InvalidOperationException($"Unable to create instance of {type.FullName}");
    }
}

internal sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly Dictionary<Type, Func<object>> _registrations;

    public TypeResolver(Dictionary<Type, Func<object>> registrations)
    {
        _registrations = registrations;
    }

    public object? Resolve(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        if (_registrations.TryGetValue(type, out var factory))
        {
            return factory();
        }

        if (!type.IsAbstract)
        {
            return Activator.CreateInstance(type);
        }

        return null;
    }

    public void Dispose()
    {
        _registrations.Clear();
    }
}

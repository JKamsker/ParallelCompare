using System;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace ParallelCompare.App.Infrastructure;

/// <summary>
/// Adapts the Spectre.Console dependency injection abstractions to <see cref="IServiceCollection"/>.
/// </summary>
public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeRegistrar"/> class.
    /// </summary>
    /// <param name="services">Service collection used to resolve dependencies.</param>
    public TypeRegistrar(IServiceCollection services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public ITypeResolver Build()
    {
        return new TypeResolver(_services.BuildServiceProvider());
    }

    /// <inheritdoc />
    public void Register(Type service, Type implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    /// <inheritdoc />
    public void RegisterInstance(Type service, object implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    /// <inheritdoc />
    public void RegisterLazy(Type service, Func<object> factory)
    {
        _services.AddSingleton(service, _ => factory());
    }

    private sealed class TypeResolver : ITypeResolver, IDisposable
    {
        private readonly ServiceProvider _provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeResolver"/> class.
        /// </summary>
        /// <param name="provider">Provider used to resolve services.</param>
        public TypeResolver(ServiceProvider provider)
        {
            _provider = provider;
        }

        /// <inheritdoc />
        public object? Resolve(Type? type)
        {
            return type is null ? null : _provider.GetService(type);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}

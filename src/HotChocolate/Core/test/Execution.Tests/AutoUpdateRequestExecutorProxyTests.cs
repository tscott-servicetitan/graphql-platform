using System;
using System.Threading.Tasks;
using HotChocolate.StarWars;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotChocolate.Execution;

public class AutoUpdateRequestExecutorProxyTests
{
    [Fact]
    public async Task Ensure_Executor_Is_Cached()
    {
        // arrange
        var resolver =
            new ServiceCollection()
                .AddGraphQL()
                .AddStarWarsRepositories()
                .AddStarWarsTypes()
                .Services
                .BuildServiceProvider()
                .GetRequiredService<IRequestExecutorResolver>();

        var innerProxy = new RequestExecutorProxy(resolver, Schema.DefaultName);

        // act
        var proxy = await AutoUpdateRequestExecutorProxy.CreateAsync(innerProxy);
        var a = proxy.InnerExecutor;
        var b = proxy.InnerExecutor;

        // assert
        Assert.Same(a, b);
    }

    [Fact]
    public async Task Ensure_Executor_Is_Correctly_Swapped_When_Evicted()
    {
        // arrange
        var resolver =
            new ServiceCollection()
                .AddGraphQL()
                .AddStarWarsRepositories()
                .AddStarWarsTypes()
                .Services
                .BuildServiceProvider()
                .GetRequiredService<IRequestExecutorResolver>();
        var evicted = false;
        var updated = false;

        var innerProxy = new RequestExecutorProxy(resolver, Schema.DefaultName);
        innerProxy.ExecutorEvicted += (_, _) =>
        {
            evicted = true;
            updated = false;
        };
        innerProxy.ExecutorUpdated += (_, _) => updated = true;

        var proxy = await AutoUpdateRequestExecutorProxy.CreateAsync(innerProxy);

        // act
        var a = proxy.InnerExecutor;
        resolver.EvictRequestExecutor();

        var i = 0;
        var b = proxy.InnerExecutor;
        while (ReferenceEquals(a, b))
        {
            await Task.Delay(100);
            b = proxy.InnerExecutor;
            if (i++ > 10)
            {
                break;
            }
        }

        // assert
        Assert.NotSame(a, b);
        Assert.True(evicted);
        Assert.True(updated);
    }

    [Fact]
    public async Task Ensure_Manual_Proxy_Is_Not_Null()
    {
        // arrange
        // act
        async Task Action() => await AutoUpdateRequestExecutorProxy.CreateAsync(null!);

        // assert
        await Assert.ThrowsAsync<ArgumentNullException>(Action);
    }
}

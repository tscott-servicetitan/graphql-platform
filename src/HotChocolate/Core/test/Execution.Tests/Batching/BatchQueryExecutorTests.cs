using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using HotChocolate.StarWars;
using HotChocolate.Tests;
using HotChocolate.Types;
using Snapshooter.Xunit;
using Xunit;
using static HotChocolate.Tests.TestHelper;

namespace HotChocolate.Execution.Batching;

public class BatchQueryExecutorTests
{
    [Fact]
    public async Task ExecuteExportScalar()
    {
        // arrange
        Snapshot.FullName();

        var executor = await CreateExecutorAsync(c => c
            .AddStarWarsTypes()
            .AddExportDirectiveType()
            .Services
            .AddStarWarsRepositories());

        // act
        var batch = new List<IQueryRequest>
        {
            QueryRequestBuilder.New()
                .SetQuery(
                    @"
                        query getHero {
                            hero(episode: EMPIRE) {
                                id @export
                            }
                        }")
                .Create(),
            QueryRequestBuilder.New()
                .SetQuery(
                    @"
                        query getHuman {
                            human(id: $id) {
                                name
                            }
                        }")
                .Create()
        };

        var batchResult = await executor.ExecuteBatchAsync(batch);

        // assert
        await batchResult.MatchSnapshotAsync();
    }

    [Fact]
    public async Task ExecuteExportScalarList()
    {
        // arrange
        Snapshot.FullName();

        var executor = await CreateExecutorAsync(c => c
            .AddStarWarsTypes()
            .AddExportDirectiveType()
            .Services
            .AddStarWarsRepositories());

        // act
        var batch = new List<IQueryRequest>
        {
            QueryRequestBuilder.New()
                .SetQuery(
                    @"
                        query getHero {
                            hero(episode: EMPIRE) {
                                friends {
                                    nodes {
                                        id @export(as: ""abc"")
                                    }
                                }
                            }
                        }")
                .Create(),
            QueryRequestBuilder.New()
                .SetQuery(
                    @"
                        query getCharacter {
                            character(characterIds: $abc) {
                                name
                            }
                        }")
                .Create()
        };

        var batchResult = await executor.ExecuteBatchAsync(batch);

        // assert
        await batchResult.MatchSnapshotAsync();
    }

    [Fact]
    public async Task ExecuteExportScalarList_ExplicitVariable()
    {
        // arrange
        Snapshot.FullName();

        var executor = await CreateExecutorAsync(c => c
            .AddStarWarsTypes()
            .AddExportDirectiveType()
            .Services
            .AddStarWarsRepositories());

        // act
        var batch = new List<IQueryRequest>
        {
            QueryRequestBuilder.New()
                .SetQuery(
                    @"
                        query getHero {
                            hero(episode: EMPIRE) {
                                friends {
                                    nodes {
                                        id @export(as: ""abc"")
                                    }
                                }
                            }
                        }")
                .Create(),
            QueryRequestBuilder.New()
                .SetQuery(
                    @"
                        query getCharacter($abc: [String!]!) {
                            character(characterIds: $abc) {
                                name
                            }
                        }")
                .Create()
        };

        var batchResult = await executor.ExecuteBatchAsync(batch);

        // assert
        await batchResult.MatchSnapshotAsync();
    }

    [Fact]
    public async Task ExecuteExportObject()
    {
        // arrange
        Snapshot.FullName();

        var executor = await CreateExecutorAsync(c => c
            .AddStarWarsTypes()
            .AddExportDirectiveType()
            .AddInMemorySubscriptions()
            .Services
            .AddStarWarsRepositories());

        // act
        var batch = new List<IQueryRequest>
        {
            QueryRequestBuilder.New()
                .SetQuery(
                    @"mutation firstReview {
                            createReview(
                                episode: NEW_HOPE
                                review: { commentary: ""foo"", stars: 4 })
                                    @export(as: ""r"") {
                                commentary
                                stars
                            }
                        }")
                .Create(),
            QueryRequestBuilder.New()
                .SetQuery(
                    @"
                        mutation secondReview {
                            createReview(
                                episode: EMPIRE
                                review: $r) {
                                commentary
                                stars
                            }
                        }")
                .Create()
        };

        var batchResult = await executor.ExecuteBatchAsync(batch);

        // assert
        await batchResult.MatchSnapshotAsync();
    }

    [Fact]
    public async Task ExecuteExportLeafList()
    {
        // arrange
        Snapshot.FullName();

        var executor = await CreateExecutorAsync(c => c
            .AddQueryType(d => d.Name("Query")
                .Field("foo")
                .Argument("bar", a => a.Type<ListType<StringType>>())
                .Type<ListType<StringType>>()
                .Resolve(ctx =>
                {
                    var list = ctx.ArgumentValue<List<string>>("bar");

                    if (list is null)
                    {
                        return new List<string>
                        {
                            "123",
                            "456"
                        };
                    }

                    list.Add("789");
                    return list;
                }))
            .AddExportDirectiveType());

        // act
        var batch = new List<IQueryRequest>
        {
            QueryRequestBuilder.New()
                .SetQuery(
                    @"{
                            foo @export(as: ""b"")
                        }")
                .Create(),
            QueryRequestBuilder.New()
                .SetQuery(
                    @"{
                            foo(bar: $b)
                        }")
                .Create()
        };

        var batchResult = await executor.ExecuteBatchAsync(batch);

        // assert
        await batchResult.MatchSnapshotAsync();
    }

    [Fact]
    public async Task ExecuteExportObjectList()
    {
        // arrange
        Snapshot.FullName();

        var executor = await CreateExecutorAsync(c => c
            .AddDocumentFromString(
                @"
                    type Query {
                        foo(f: [FooInput]) : [Foo]
                    }

                    type Foo {
                        bar: String!
                    }

                    input FooInput {
                        bar: String!
                    }")
            .AddResolver("Query", "foo", ctx =>
            {
                var list = ctx.ArgumentValue<List<object>>("f");

                if (list is null)
                {
                    return new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "bar" , "123" }
                        }
                    };
                }

                list.Add(new Dictionary<string, object>
                {
                    { "bar" , "456" }
                });
                return list;
            })
            .UseField(next => context =>
            {
                var o = context.Parent<object>();
                if (o is Dictionary<string, object> d
                    && d.TryGetValue(context.ResponseName, out var v))
                {
                    context.Result = v;
                }
                return next(context);
            })
            .AddExportDirectiveType());

        // act
        var batch = new List<IQueryRequest>
        {
            QueryRequestBuilder.New()
                .SetQuery(
                    @"{
                            foo @export(as: ""b"")
                            {
                                bar
                            }
                        }")
                .Create(),
            QueryRequestBuilder.New()
                .SetQuery(
                    @"{
                            foo(f: $b)
                            {
                                bar
                            }
                        }")
                .Create()
        };

        var batchResult = await executor.ExecuteBatchAsync(batch);

        // assert
        await batchResult.MatchSnapshotAsync();
    }

    [Fact]
    public async Task Add_Value_To_Variable_List()
    {
        // arrange
        Snapshot.FullName();

        var executor = await CreateExecutorAsync(c => c
            .AddQueryType(d => d.Name("Query")
                .Field("foo")
                .Argument("bar", a => a.Type<ListType<StringType>>())
                .Type<ListType<StringType>>()
                .Resolve(ctx =>
                {
                    var list = ctx.ArgumentValue<List<string>>("bar");
                    list.Add("789");
                    return list;
                }))
            .AddExportDirectiveType());

        // act
        var batch = new List<IQueryRequest>
        {
            QueryRequestBuilder.New()
                .SetQuery(
                    @"query foo1($b: [String]) {
                            foo(bar: $b) @export(as: ""b"")
                        }")
                .AddVariableValue("b", new[] { "123" })
                .Create(),
            QueryRequestBuilder.New()
                .SetQuery(
                    @"query foo2($b: [String]) {
                            foo(bar: $b)
                        }")
                .Create()
        };

        var batchResult = await executor.ExecuteBatchAsync(batch);

        // assert
        await batchResult.MatchSnapshotAsync();
    }

    [Fact]
    public async Task Convert_List_To_Single_Value_With_Converters()
    {
        // arrange
        Snapshot.FullName();

        var executor = await CreateExecutorAsync(c => c
            .AddQueryType(d =>
            {
                d.Name("Query");

                d.Field("foo")
                    .Argument("bar", a => a.Type<ListType<StringType>>())
                    .Type<ListType<StringType>>()
                    .Resolve(ctx =>
                    {
                        var list = ctx.ArgumentValue<List<string>>("bar");
                        list.Add("789");
                        return list;
                    });

                d.Field("baz")
                    .Argument("bar", a => a.Type<StringType>())
                    .Resolve(ctx => ctx.ArgumentValue<string>("bar"));
            })
            .AddExportDirectiveType());

        // act
        var batch = new List<IQueryRequest>
        {
            QueryRequestBuilder.New()
                .SetQuery(
                    @"query foo1($b1: [String]) {
                            foo(bar: $b1) @export(as: ""b2"")
                        }")
                .AddVariableValue("b1", new[] { "123" })
                .Create(),
            QueryRequestBuilder.New()
                .SetQuery(
                    @"query foo2($b2: String) {
                            baz(bar: $b2)
                        }")
                .Create()
        };

        var batchResult = await executor.ExecuteBatchAsync(batch);

        // assert
        await batchResult.MatchSnapshotAsync();
    }

    [Fact]
    public async Task Batch_Is_Null()
    {
        // arrange
        Snapshot.FullName();

        var executor = await CreateExecutorAsync(c => c
            .AddStarWarsTypes()
            .AddExportDirectiveType()
            .Services
            .AddStarWarsRepositories());

        // act
        Task Action() => executor.ExecuteBatchAsync(null!);

        // assert
        await Assert.ThrowsAsync<ArgumentNullException>(Action);
    }
}

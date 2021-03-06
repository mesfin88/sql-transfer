using System;
using MessageStorage.Extensions;
using MessageStorage.MultiClient.IntegrationTest.Fixtures.SecondaryMessageStorageSection;
using MessageStorage.Postgres.Extensions;
using MessageStorage.SqlServer.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestUtility.DbUtils;
using Xunit;

namespace MessageStorage.MultiClient.IntegrationTest.Fixtures;

[CollectionDefinition(TestServerFixture.FIXTURE_KEY)]
public class TestServerFixtureDefinition : ICollectionFixture<TestServerFixture>
{
}

public class TestServerFixture : IDisposable
{
    public const string FIXTURE_KEY = "PostgresIntegrationTest.TestServerFixtureKey";

    private readonly PostgresFixture _postgresFixture;
    private readonly SqlServerFixture _sqlServerFixture;
    private readonly IHost _testServer;

    public TestServerFixture()
    {
        _postgresFixture = new PostgresFixture();
        _sqlServerFixture = new SqlServerFixture();

        Db.SetPostgresConnectionStr(_postgresFixture.ConnectionStr);
        Db.SetSqlServerConnectionStr(_sqlServerFixture.ConnectionStr);


        IHostBuilder? hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.UseEnvironment("Test")
                   .ConfigureWebHost(builder =>
                    {
                        builder.UseTestServer();
                        builder.ConfigureServices(collection =>
                        {
                            collection.AddMessageStorage(optionBuilder =>
                            {
                                optionBuilder.RegisterHandlers(GetType().Assembly);
                                optionBuilder.UseSqlServer(_sqlServerFixture.ConnectionStr);
                            });
                            collection.AddMessageStorage<ISecondaryMessageStorageClient, SecondaryMessageStorageClient>(optionBuilder =>
                            {
                                optionBuilder.RegisterHandlers(GetType().Assembly);
                                optionBuilder.UsePostgres(_postgresFixture.ConnectionStr);
                            });
                        });
                        builder.Configure(_ => { });
                        builder.ConfigureLogging(loggingBuilder => loggingBuilder.AddConsole()
                                                                                 .SetMinimumLevel(LogLevel.Debug));
                    });
        _testServer = hostBuilder.Build();
        _testServer.Start();
    }

    public IServiceScope GetServiceScope()
    {
        IServiceScope? serviceScope = _testServer.Services.CreateScope();
        return serviceScope;
    }

    public void Dispose()
    {
        _testServer?.Dispose();
        _postgresFixture?.Dispose();
        _sqlServerFixture?.Dispose();
    }
}
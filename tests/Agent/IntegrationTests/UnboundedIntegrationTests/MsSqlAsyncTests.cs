// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests
{
    public abstract class MsSqlAsyncTestsBase
    {
        private readonly RemoteServiceFixtures.IMsSqlClientFixture _fixture;
        private readonly string _expectedTransactionName;

        public MsSqlAsyncTestsBase(RemoteServiceFixtures.IMsSqlClientFixture fixture, ITestOutputHelper output, string expectedTransactionName)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = expectedTransactionName;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(
                       instrumentationFilePath,
                        "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetMsSqlAsync();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedDatastoreCallCount = 4;

            //This value is dictated by the query and subsequent ExecuteScalarAsync that is being run as part of this test. In this case, we're running a query that returns a single row.
            //This results in a call to ReadAsync followed by a NextResultAsync and finally another ReadAsync. Therefore
            //the call count for the Iterate metric should be 3.
            var expectedIterateCallCount = 3;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allWeb", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MSSQL/{CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)}/default", callCount = expectedDatastoreCallCount},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/select", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/insert", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/delete", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount, metricScope = _expectedTransactionName}
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a web transaction so there should be no allOther metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allOther", callCount = 5 },

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 3, metricScope = _expectedTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", callCount = 1, metricScope = _expectedTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 1, metricScope = _expectedTransactionName }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/MSSQL/teammembers/select"
            };
            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "explain_plan"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "sql", parameterValue = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = ?"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "host", parameterValue = CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "port_path_or_id", parameterValue = "default"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "database_name", parameterValue = "NewRelic"}

            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = ?",
                    DatastoreMetricName = "Datastore/statement/MSSQL/teammembers/select",
                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = $"SELECT COUNT(*) FROM {_fixture.TableName} WITH(nolock)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.TableName}/select",

                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = $"INSERT INTO {_fixture.TableName} (FirstName, LastName, Email) VALUES(?, ?, ?)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.TableName}/insert",

                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = $"DELETE FROM {_fixture.TableName} WHERE Email = ?",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.TableName}/delete",

                    HasExplainPlan = true
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedTransactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_expectedTransactionName);
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );
        }
    }

    [NetFrameworkTest]
    public class MsSqlAsyncTests : MsSqlAsyncTestsBase, IClassFixture<RemoteServiceFixtures.MsSqlBasicMvcFixture>
    {
        public MsSqlAsyncTests(RemoteServiceFixtures.MsSqlBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/MVC/MsSqlController/MsSqlAsync")
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftDataSqlClientAsyncTests : MsSqlAsyncTestsBase, IClassFixture<RemoteServiceFixtures.MicrosoftDataSqlClientFixture>
    {
        public MicrosoftDataSqlClientAsyncTests(RemoteServiceFixtures.MicrosoftDataSqlClientFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/MVC/MicrosoftDataSqlClient/MsSqlAsync/{tableName}")
        {
        }
    }
}

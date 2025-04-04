// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Extensions.Parsing.ConnectionString
{
    public class IbmDb2ConnectionStringParser : IConnectionStringParser
    {
        private static readonly List<string> _hostKeys = new List<string> { "network address", "server", "hostname" };
        private static readonly List<string> _databaseNameKeys = new List<string> { "database" };

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public IbmDb2ConnectionStringParser(string connectionString)
        {
            _connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        public ConnectionInfo GetConnectionInfo(string utilizationHostName)
        {
            var host = ParseHost();
            if (host != null) host = ConnectionStringParserHelper.NormalizeHostname(host, utilizationHostName);
            var portPathOrId = ParsePortPathOrId();
            var databaseName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _databaseNameKeys)?.Value;

            return new ConnectionInfo(host, portPathOrId, databaseName);
        }

        private string ParseHost()
        {
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            var endOfHostname = host.IndexOf(StringSeparators.ColonChar);
            return endOfHostname == -1 ? host : host.Substring(0, endOfHostname);
        }

        private string ParsePortPathOrId()
        {
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            if (host.Contains(StringSeparators.ColonChar))
                return host.Substring(host.IndexOf(StringSeparators.ColonChar) + 1);

            return "default";
        }
    }
}

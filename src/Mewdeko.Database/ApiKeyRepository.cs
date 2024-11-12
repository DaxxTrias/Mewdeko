using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Mewdeko.Database.Models;
using Dapper;
using System.Linq;
using Serilog;

namespace Mewdeko.Database
{
    public class ApiKeyRepository
    {
        private readonly string _connectionString;

        public ApiKeyRepository(string connectionString)
        {
            Log.Information("ApiKeyRepository instantiated. constring: " + connectionString);
            _connectionString = connectionString;
        }

        public (string ApiKey, string ApiSecret) GetLatestApiKey()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                return connection.QueryFirstOrDefault<(string, string)>("SELECT ApiKey, ApiSecret FROM ApiKeys ORDER BY CreatedAt DESC LIMIT 1");
            }
        }

        public void UpsertUserAffiliates(List<UserAffiliate> userAffiliates)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                foreach (var affiliate in userAffiliates)
                {
                    connection.Execute("INSERT OR REPLACE INTO UserAffiliates (Id, Name, Email) VALUES (@Id, @Name, @Email)", affiliate);
                }
            }
        }

        public void UpsertGuildMembers(List<BMexMember> members)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                foreach (var member in members)
                {
                    connection.Execute("INSERT OR REPLACE INTO GuildMembers (Id, Name, Role) VALUES (@Id, @Name, @Role)", member);
                }
            }
        }
    }
}

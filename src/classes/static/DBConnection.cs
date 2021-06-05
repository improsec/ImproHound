using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver;

namespace ImproHound
{
    public static class DBConnection
    {
        private static IDriver driver = null;

        public static void Connect(string uri, string username, string password)
        {
            try
            {
                driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
            }
            catch (Exception err)
            {
                // Error
                throw err;
            }
        }

        public static async Task<List<IRecord>> Query(string query)
        {
            if (driver is null)
            {
                throw new ArgumentNullException("Connect to DB before you query");
            }
            else
            {
                IAsyncSession session = driver.AsyncSession();

                try
                {
                    return await session.WriteTransactionAsync(tx => RunCypherWithResults(tx, query));
                }
                catch (Exception err)
                {
                    // Error
                    throw err;
                }
                finally
                {
                    Console.WriteLine("Closed connection");
                    await session.CloseAsync();
                }
            }
        }

        private static async Task<List<IRecord>> RunCypherWithResults(IAsyncTransaction tx, string cypher, Dictionary<string, object> parameters = null)
        {
            IResultCursor result;

            if (parameters != null)
            {
                result = await tx.RunAsync(cypher, parameters);
            }
            else
            {
                result = await tx.RunAsync(cypher);
            }

            return await result?.ToListAsync();
        }
    }
    public enum DBAction
    {
        StartFromScratch, Continue, StartOver
    }
}

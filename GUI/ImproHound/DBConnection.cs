﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Neo4j.Driver;

namespace ImproHound
{

    public class DBConnection : IAsyncDisposable
    {
        private readonly IDriver _driver;
        private int timeout = 5000;

        public DBConnection(string uri, string username, string password)
        {
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
        }

        public async Task<List<IRecord>> Query(string query)
        {
            IAsyncSession session = _driver.AsyncSession();

            try
            {
                Task<List<IRecord>> task = session.WriteTransactionAsync(tx => RunCypherWithResults(tx, query));
                if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                {
                    return await task;
                }
                else
                {
                    // Timeout error
                    MessageBox.Show("No response in " + timeout + " ms.\nVerify DB URL and DB is running.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw new Exception();
                }

            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show("Error:\n" + err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception();
            }
            finally
            {
                Console.WriteLine("Closed connection");
                await session.CloseAsync();
            }
        }

        private async Task RunCypher(IAsyncTransaction tx, string cypher, Dictionary<string, object> parameters = null)
        {
            if (parameters != null)
            {
                await tx.RunAsync(cypher, parameters);
            }
            else
            {
                await tx.RunAsync(cypher);
            }
        }

        private async Task<List<IRecord>> RunCypherWithResults(IAsyncTransaction tx, string cypher, Dictionary<string, object> parameters = null)
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

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}

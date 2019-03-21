
namespace Microsoft.Azure.CosmosDB.Aggregations.CosmosDBHelper
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    using Newtonsoft.Json.Linq;

    public class CosmosDBHelper
    {
        /// <summary>
        /// Checks whether a collections exists. Creates a new collection if
        /// the collection does not exist.
        /// <para>WARNING: CreateCollectionIfNotExistsAsync will create a
        /// new collection with reserved throughput which has pricing
        /// implications. For details visit:
        /// https://azure.microsoft.com/en-us/pricing/details/cosmos-db/
        /// </para>
        /// </summary>
        /// <param name="databaseName">Name of database to create</param>
        /// <param name="collectionName">Name of collection to create within the specified database</param>
        /// <param name="throughput">Amount of throughput to provision for the collection to be created</param>
        /// <param name="partitionKey">Partition Key for the collection to be created</param>
        /// <returns>A Task to allow asynchronous execution</returns>
        public static async Task CreateCollectionIfNotExistsAsync(
            DocumentClient client,
            string databaseName,
            string collectionName,
            int throughput,
            string partitionKey,
            bool deleteExistingColl = false)
        {
            await client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseName });

            PartitionKeyDefinition pkDefn = null;

            Collection<string> paths = new Collection<string>();
            paths.Add(partitionKey);
            pkDefn = new PartitionKeyDefinition() { Paths = paths };

            try
            {
                DocumentCollection existingColl = await client.ReadDocumentCollectionAsync(string.Format("/dbs/{0}/colls/{1}", databaseName, collectionName));
                if (existingColl != null)
                {
                    if (!deleteExistingColl)
                    {
                        Console.WriteLine("Collection already present, returning...");
                    }
                    else
                    {
                        Console.WriteLine("Collection present. Deleting collection...");
                        await client.DeleteDocumentCollectionAsync(string.Format("/dbs/{0}/colls/{1}", databaseName, collectionName));
                        Console.WriteLine("Finished deleting the collection.");

                        await client.CreateDocumentCollectionAsync(
                            UriFactory.CreateDatabaseUri(databaseName),
                            new DocumentCollection { Id = collectionName, PartitionKey = pkDefn },
                            new RequestOptions { OfferThroughput = throughput });
                    }
                }
            }
            catch (DocumentClientException dce)
            {
                if ((int)dce.StatusCode == 404)
                {
                    Console.WriteLine("Collection not found, creating...");
                    try
                    {
                        await client.CreateDocumentCollectionAsync(
                            UriFactory.CreateDatabaseUri(databaseName),
                            new DocumentCollection { Id = collectionName, PartitionKey = pkDefn },
                            new RequestOptions { OfferThroughput = throughput });
                        Console.WriteLine("Successfully created new collection.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception thrown when attempting to create collection: {0} in database: {1}. Exception message: {2}",
                            collectionName,
                            databaseName,
                            ex.Message);
                    }

                }
                else

                    throw;
            }
        }

        /// <summary>
        /// Writes the randomly generated sample data into the specified Cosmos DB collection, using the BulkExecutor library
        /// </summary>
        /// <param name="documentsToImport"></param>
        /// <param name="databaseName">Name of the Azure Cosmos DB database to write the documents into</param>
        /// <param name="collectionName">Name of the Azure Cosmos DB collection to write the documents into</param>
        /// <param name="client">DocumentClient instance to interact with the Azure Cosmos DB service</param>
        /// <param name="maxRetriesOnRateLimitedCalls">Maximum number of retries to execute against the Cosmos DB container when rate limited</param>
        public static async Task WriteToCosmosDB(List<string> documentsToImport, string databaseName, string collectionName, DocumentClient client, int maxRetriesOnRateLimitedCalls)
        {
            Uri documentsFeedLink = UriFactory.CreateDocumentCollectionUri(databaseName, collectionName);

            foreach (string eachDocumentToWrite in documentsToImport)
            {
                try
                {
                    await client.UpsertDocumentAsync(
                        documentsFeedLink,
                        JObject.Parse(eachDocumentToWrite),
                        null,
                        false);

                    Console.WriteLine("Successfully wrote document to Cosmos DB collection");
                }
                catch (DocumentClientException ex)
                {
                    if ((int)ex.StatusCode == 429)
                    {
                        // Implement custom retry logic - retry another 10 times
                        int numRetries = 0;
                        bool success = false;

                        while (!success && numRetries < maxRetriesOnRateLimitedCalls)
                        {
                            Thread.Sleep((int)(ex.RetryAfter.TotalMilliseconds * 2));

                            try
                            {
                                await client.UpsertDocumentAsync(documentsFeedLink, eachDocumentToWrite, null, false);
                                success = true;
                            }
                            catch (DocumentClientException idex)
                            {
                                numRetries++;
                            }
                            catch (Exception iex)
                            {
                                numRetries++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception encountered when attempting to write document. Original exception was: {0}", ex.Message);
                }
            }
        }
    }
}

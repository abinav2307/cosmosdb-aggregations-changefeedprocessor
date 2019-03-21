
namespace Microsoft.Azure.CosmosDB.Aggregations.DataGenerator
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.CosmosDB.Aggregations.CosmosDBHelper;

    public abstract class BaseDataGenerator
    {
        /// <summary>
        /// Maximum number of custom retries on throttled requests
        /// </summary>
        private int MaxRetriesOnThrottles = 10;

        public abstract Task GenerateSampleData();

        /// <summary>
        /// Writes the randomly generated sample data into the specified Cosmos DB collection, using the BulkExecutor library
        /// </summary>
        /// <param name="documentsToImport"></param>
        /// <param name="databaseName">Name of the Azure Cosmos DB database to write the documents into</param>
        /// <param name="collectionName">Name of the Azure Cosmos DB collection to write the documents into</param>
        /// <param name="client">DocumentClient instance to interact with the Azure Cosmos DB service</param>
        public async Task WriteToCosmosDB(List<string> documentsToImport, string databaseName, string collectionName, DocumentClient client)
        {
            await CosmosDBHelper.WriteToCosmosDB(documentsToImport, databaseName, collectionName, client, this.MaxRetriesOnThrottles);
        }
    }
}

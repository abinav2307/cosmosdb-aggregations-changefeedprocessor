
namespace Microsoft.Azure.CosmosDB.Aggregations
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading.Tasks;

    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.CosmosDB.Aggregations.DataGenerator;
    using Microsoft.Azure.CosmosDB.Aggregations.ChangeFeed;

    class Program
    {
        public static void Main(string[] args)
        {
            DocumentClient documentClient = CreateDocumentClient();

            // List of 3 tasks - (1) Data Generator/Loader, (2) Aggregation Rules Generator, (3) Change Feed Processor to execute aggregations
            var tasks = new List<Task>();

            bool isGenerateAggregationRules = bool.Parse(ConfigurationManager.AppSettings["GenerateAggregationRules"]);
            if (isGenerateAggregationRules)
            {
                IDataGenerator aggregationRuleGenerator = new AggregationRuleGenerator(documentClient);
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    aggregationRuleGenerator.GenerateSampleData().Wait();
                }));
            }

            Task.WaitAll(tasks.ToArray());

            tasks = new List<Task>();

            bool isGenerateSampleData = bool.Parse(ConfigurationManager.AppSettings["GenerateSampleData"]);
            if (isGenerateSampleData)
            {
                IDataGenerator dataGenerator = new EmployeeSampleDataGenerator(documentClient);
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    dataGenerator.GenerateSampleData().Wait();
                }));
            }            

            bool isExecuteChangeFeed = bool.Parse(ConfigurationManager.AppSettings["ExecuteChangeFeedProcessor"]);
            if (isExecuteChangeFeed)
            {
                ChangeFeedExecutor changeFeedExecutor = new ChangeFeedExecutor(documentClient, "New Host");
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    changeFeedExecutor.ExecuteChangeFeedProcessor().Wait();
                }));
            }

            Task.WaitAll(tasks.ToArray());

            Console.WriteLine("Completed!");
            Console.ReadLine();
        }

        /// <summary>
        /// Creates an instance of the DocumentClient to interact with the Azure Cosmos DB service
        /// </summary>
        /// <returns></returns>
        private static DocumentClient CreateDocumentClient()
        {
            ConnectionPolicy ConnectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp
            };

            DocumentClient documentClient = null;
            try
            {
                string cosmosDBEndpointUri = ConfigurationManager.AppSettings["CosmosDBEndpointUri"];
                string accountKey = ConfigurationManager.AppSettings["CosmosDBAuthKey"];
                documentClient = new DocumentClient(new Uri(cosmosDBEndpointUri), accountKey, ConnectionPolicy);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception while creating DocumentClient. Original  exception message was: {0}", ex.Message);
            }

            return documentClient;
        }
    }
}

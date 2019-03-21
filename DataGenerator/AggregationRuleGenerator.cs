
namespace Microsoft.Azure.CosmosDB.Aggregations.DataGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.CosmosDB.Aggregations.Aggregator;
    using Microsoft.Azure.CosmosDB.Aggregations.CosmosDBHelper;

    using Newtonsoft.Json;
    
    internal sealed class AggregationRuleGenerator : BaseDataGenerator, IDataGenerator
    {
        /// <summary>
        /// The database containing the collection, into which the sample data will be ingested
        /// </summary>
        private string DatabaseName;

        /// <summary>
        /// The Cosmos DB collection into which the sample data will be ingested
        /// </summary>
        private string CollectionName;

        /// <summary>
        /// DocumentClient instance to be used for the ingestion
        /// </summary>
        private DocumentClient DocumentClient;

        /// <summary>
        /// Random number generator to assign a random title or department to a User entity in the sample data
        /// </summary>
        private static Random Random = new Random();

        /// <summary>
        /// List of properties to use as grouping terms for aggregation
        /// </summary>
        private List<string> AggregationProperties;

        /// <summary>
        /// List of properties to use as grouping terms for aggregation
        /// </summary>
        private List<string> AggregationGroupingProperties;

        /// <summary>
        /// Map keeping track of number of Aggregation Rules generated per partition Key
        /// </summary>
        private Dictionary<string, int> PartitionKeyToAggregationRuleCountMapper;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client">Instance of the Azure Cosmos DB SDK</param>
        public AggregationRuleGenerator(DocumentClient client)
        {
            this.DatabaseName = ConfigurationManager.AppSettings["AggregationRulesDatabaseName"];
            this.CollectionName = ConfigurationManager.AppSettings["AggregationRulesCollectionName"];
            this.AggregationProperties = ConfigurationManager.AppSettings["AggregationProperties"].Split(',').ToList();
            this.AggregationGroupingProperties = ConfigurationManager.AppSettings["AggregationGroupingField"].Split(',').ToList();
            this.PartitionKeyToAggregationRuleCountMapper = new Dictionary<string, int>();
            
            this.DocumentClient = client;

            bool recreateAggregationRulesCollection = bool.Parse(ConfigurationManager.AppSettings["RecreateAggregationRulesCollection"]);
            if (recreateAggregationRulesCollection)
            {
                int aggregationRulesCollectionThroughput = int.Parse(ConfigurationManager.AppSettings["AggregationRulesCollectionThroughput"]);
                string aggregationRulesCollectionPartitionKey = ConfigurationManager.AppSettings["AggregationRulesCollectionPartitionKey"];

                CosmosDBHelper.CreateCollectionIfNotExistsAsync(
                    this.DocumentClient,
                    this.DatabaseName,
                    this.CollectionName,
                    aggregationRulesCollectionThroughput,
                    aggregationRulesCollectionPartitionKey,
                    true).Wait();
            }
        }

        /// <summary>
        /// Generate sample aggregation rules
        /// </summary>
        /// <returns></returns>
        public override async Task GenerateSampleData()
        {
            List<string> documentsToImport = new List<string>();

            int numDistinctPartitionKeys = int.Parse(ConfigurationManager.AppSettings["NumDistinctPartitionKeys"]);
            int maxRulesPerPartitionKey = int.Parse(ConfigurationManager.AppSettings["MaxRulesPerPartitionKey"]);
            int batchSizeForWrites = int.Parse(ConfigurationManager.AppSettings["BatchSizeForWrites"]);

            int currentBatchCount = 0;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int batchNum = 1;

            for (int eachPKToGenerateRuleFor = 0; eachPKToGenerateRuleFor < numDistinctPartitionKeys; eachPKToGenerateRuleFor++)
            {
                // # of aggregation rules to generate for this model
                int numRulesToGenerate = AggregationRuleGenerator.Random.Next(1, maxRulesPerPartitionKey+1);

                Console.WriteLine("About to generate {0} aggregation rules for model.", numRulesToGenerate);

                for (int eachRuleToGenerate = 0; eachRuleToGenerate < numRulesToGenerate; eachRuleToGenerate++)
                {
                    AggregationRule sampleAggregationRule = this.GenerateSampleAggregationRule();
                    string sampleAggregationJson = JsonConvert.SerializeObject(sampleAggregationRule);
                    documentsToImport.Add(sampleAggregationJson);

                    currentBatchCount++;

                    if (currentBatchCount == batchSizeForWrites)
                    {
                        Console.WriteLine("Batch: {0}", batchNum);
                        batchNum++;

                        foreach (string aggregationRule in documentsToImport)
                        {
                            Console.WriteLine(aggregationRule);
                        }

                        await this.WriteToCosmosDB(documentsToImport, this.DatabaseName, this.CollectionName, this.DocumentClient);
                        currentBatchCount = 0;
                        documentsToImport = new List<string>();
                    }

                }
            }

            stopwatch.Stop();
            Console.WriteLine("Time taken = {0} milliseconds", stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Generate sample aggregation rules
        /// </summary>
        /// <returns></returns>
        private AggregationRule GenerateSampleAggregationRule()
        {
            AggregationRule aggregationRule = new AggregationRule();

            string firstName = Constants.GetRandomFirstName();
            string lastName = Constants.GetRandomLastName();
            
            while (this.PartitionKeyToAggregationRuleCountMapper.ContainsKey(firstName))
            {
                firstName = Constants.GetRandomFirstName();
            }

            int numLevelsForAggregationGroup = AggregationRuleGenerator.Random.Next(1, this.AggregationGroupingProperties.Count);
            List<string> groupings = new List<string>();
            while (groupings.Count < numLevelsForAggregationGroup)
            {
                int levelIndex = AggregationRuleGenerator.Random.Next(0, this.AggregationGroupingProperties.Count);
                if (!groupings.Contains(this.AggregationGroupingProperties[levelIndex]))
                {
                    groupings.Add(this.AggregationGroupingProperties[levelIndex]);
                }
            }

            string aggregationProperty =
                this.AggregationProperties[AggregationRuleGenerator.Random.Next(0, this.AggregationProperties.Count)];

            var aggregationTypes = Enum.GetValues(typeof(AggregationType));
            var aggregationComparisons = Enum.GetValues(typeof(AggregationComparison));

            aggregationRule.PartitionKey = firstName;
            aggregationRule.AggregationGrouping = groupings;
            aggregationRule.AggregationProperty = aggregationProperty;
            aggregationRule.AggregationType =
                (AggregationType)Enum.GetValues(typeof(AggregationType)).GetValue(AggregationRuleGenerator.Random.Next(0, aggregationTypes.Length));
            aggregationRule.AggregationComparison =
                (AggregationComparison)Enum.GetValues(typeof(AggregationComparison)).GetValue(AggregationRuleGenerator.Random.Next(0, aggregationComparisons.Length));
            aggregationRule.AggregationRuleThreshold = AggregationRuleGenerator.Random.Next(0, 100);

            return aggregationRule;
        }
    }
}

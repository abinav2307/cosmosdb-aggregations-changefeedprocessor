
namespace Microsoft.Azure.CosmosDB.Aggregations.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.CosmosDB.Aggregations.Aggregator;

    using Microsoft.Azure.Documents.Linq;

    public class DocumentFeedObserver : IChangeFeedObserver
    {
        /// <summary>
        /// Instance of the DocumentClient, used to push failed batches of backups to the Cosmos DB collection
        /// tracking failures during the backup process.
        /// </summary>
        private DocumentClient DocumentClient;

        /// <summary>
        /// Max number of retries on rate limited writes to the Cosmos DB collection capturing aggregated views
        /// </summary>
        private int MaxRetriesOnRateLimitedWritesToCosmosDB = 10;

        /// <summary>
        /// Azure Cosmos DB database within which to store the materialized, pre-aggregated data
        /// </summary>
        private string DatabaseName;

        /// <summary>
        /// Azure Cosmos DB collection which contains the aggregation rules
        /// </summary>
        private string AggregationRulesCollectionName;

        /// <summary>
        /// Azure Cosmos DB collection which contains the results of the aggregations specified by the rules
        /// </summary>
        private string AggregationResultsCollectionName;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentFeedObserver" /> class.
        /// </summary>
        public DocumentFeedObserver(DocumentClient client)
        {
            Console.WriteLine("Entering constructor for DocumentFeedObserver");

            this.DocumentClient = client;
            this.DatabaseName = ConfigurationManager.AppSettings["AggregationRulesDatabaseName"];
            this.AggregationRulesCollectionName = ConfigurationManager.AppSettings["AggregationRulesCollectionName"];
            this.AggregationResultsCollectionName = ConfigurationManager.AppSettings["AggregationResultsCollectionName"];

            bool recreateAggregationResultsCollection = bool.Parse(ConfigurationManager.AppSettings["RecreateAggregationResultsCollection"]);
            if(recreateAggregationResultsCollection)
            {
                int aggregationResultsCollectionThroughput = int.Parse(ConfigurationManager.AppSettings["AggregationResultsCollectionThroughput"]);
                string aggregationResultsCollectionPartitionKey = ConfigurationManager.AppSettings["AggregationResultsCollectionPartitionKey"];

                CosmosDBHelper.CosmosDBHelper.CreateCollectionIfNotExistsAsync(
                this.DocumentClient,
                this.DatabaseName,
                this.AggregationResultsCollectionName,
                aggregationResultsCollectionThroughput,
                aggregationResultsCollectionPartitionKey,
                true).Wait();
            }
        }

        /// <summary>
        /// Called when change feed observer is opened; this function prints out the
        /// observer's partition key id.
        /// </summary>
        /// <param name="context">The context specifying the partition for this observer, etc.</param>
        /// <returns>A Task to allow asynchronous execution</returns>
        public Task OpenAsync(IChangeFeedObserverContext context)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when change feed observer is closed; this function prints out the
        /// observer's partition key id and reason for shut down.
        /// </summary>
        /// <param name="context">The context specifying the partition for this observer, etc.</param>
        /// <param name="reason">Specifies the reason the observer is closed.</param>
        /// <returns>A Task to allow asynchronous execution</returns>
        public Task CloseAsync(IChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when there is a batch of changes to be processed.
        /// </summary>
        /// <param name="context">The context specifying the partition for this observer, etc.</param>
        /// <param name="docs">The documents changed.</param>
        /// <param name="cancellationToken">Token to signal that the parition processing is going to finish.</param>
        /// <returns>A Task to allow asynchronous execution</returns>
        public Task ProcessChangesAsync(IChangeFeedObserverContext context, IReadOnlyList<Document> docs, CancellationToken cancellationToken)
        {
            Console.WriteLine("0. Entering process changes async");
            this.ExecuteAggregations(docs).Wait();

            return Task.CompletedTask;
        }

        /// <summary>
        /// For a given AIModelId, ExtractionId combination, this method returns a list of all aggregation rules
        /// that exist.
        /// </summary>
        /// <param name="partitionKeyForAggregations"></param>
        /// <returns></returns>
        private async Task<List<AggregationRule>> FetchListOfAggregations(string partitionKeyForAggregations)
        {
            List<AggregationRule> listOfAggregations = new List<AggregationRule>();

            string aggregationRuleQuery =
                string.Concat("select * from c where c.partitionKey = '", partitionKeyForAggregations, "'");

            string collectionLink = UriFactory.CreateDocumentCollectionUri(this.DatabaseName, this.AggregationRulesCollectionName).ToString();

            try
            {
                var query = this.DocumentClient.CreateDocumentQuery<AggregationRule>(collectionLink,
                aggregationRuleQuery,
                new FeedOptions()
                {
                    MaxDegreeOfParallelism = -1,
                    MaxItemCount = 1000
                }).AsDocumentQuery();
                while (query.HasMoreResults)
                {
                    var result = await query.ExecuteNextAsync();
                    foreach (AggregationRule aggregationDocument in result)
                    {
                        listOfAggregations.Add(aggregationDocument);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception message is: " + ex.Message);
                Console.WriteLine("Stack trace is: {0}", ex.StackTrace);
            }

            Console.WriteLine("2. Successfully retrieved list of aggregations.");
            foreach (AggregationRule aggregationRule in listOfAggregations)
            {
                Console.WriteLine("2.1 Retrieved aggregation rule with id: {0}", aggregationRule.Id);
            }

            return listOfAggregations;
        }

        /// <summary>
        /// As a pre-aggregation step, this method buckets the changed documents by AIModelId and ExtractionId
        /// since all aggregations are performed at this level.
        /// </summary>
        /// <param name="docs">List of modified documents returned by ChangeFeedProcess</param>
        /// <returns></returns>
        private Dictionary<string, List<Document>> GroupDocumentsByAggregationGroup(IReadOnlyList<Document> docs)
        {
            Dictionary<string, List<Document>> documentsByAggregationGroup =
                new Dictionary<string, List<Document>>();

            foreach (Document eachDocument in docs)
            {
                string partitionKeyForAggregationRule = eachDocument.GetPropertyValue<string>("partitionKey");

                if (documentsByAggregationGroup.ContainsKey(partitionKeyForAggregationRule))
                {
                    documentsByAggregationGroup[partitionKeyForAggregationRule].Add(eachDocument);
                }
                else
                {
                    documentsByAggregationGroup[partitionKeyForAggregationRule] = new List<Document>();
                    documentsByAggregationGroup[partitionKeyForAggregationRule].Add(eachDocument);
                }
            }

            Console.WriteLine("1. Successfully retrieved grouping of documents by aggregation level.");
            foreach (string key in documentsByAggregationGroup.Keys)
            {
                List<Document> documentsPerKey = documentsByAggregationGroup[key];
                foreach (Document eachDocument in documentsPerKey)
                {
                    Console.WriteLine("Retrieved document id: {0}, for aggregation bucket: {1}", eachDocument.Id, key);
                }
            }

            return documentsByAggregationGroup;
        }

        private List<AggregationResult> ExecuteAggregationForModelAndExtraction(string partitionKey, AggregationRule aggregationDocument, List<Document> documentsToAggregate)
        {
            AggregationType aggregationRule = aggregationDocument.GetPropertyValue<AggregationType>("aggregationType");
            List<AggregationResult> aggregationResults = new List<AggregationResult>();

            switch (aggregationRule)
            {
                case AggregationType.SUM: SumAggregator sumAggregator = new SumAggregator();
                                          aggregationResults = sumAggregator.ExecuteAggregationRule(partitionKey, aggregationDocument, documentsToAggregate);
                                          return aggregationResults;

                case AggregationType.MAX: MaxAggregator maxAggregator = new MaxAggregator();
                                          aggregationResults = 
                                              maxAggregator.ExecuteAggregationRule(partitionKey, aggregationDocument, documentsToAggregate);
                                          return aggregationResults;

                case AggregationType.MIN:
                                         MinAggregator minAggregator = new MinAggregator();
                                         aggregationResults =
                                             minAggregator.ExecuteAggregationRule(partitionKey, aggregationDocument, documentsToAggregate);
                                         return aggregationResults;

                default: return aggregationResults;
            }
        }

        /// <summary>
        /// Executes the necessary aggregations to be performed on the list of modified documents returned by the Change Feed Processor
        /// </summary>
        /// <param name="modifiedOrCreatedDocuments">List of modified documents in Cosmos DB</param>
        /// <returns></returns>
        private async Task ExecuteAggregations(IReadOnlyList<Document> modifiedOrCreatedDocuments)
        {
            Console.WriteLine("Number of changed documents retrieved = {0}", modifiedOrCreatedDocuments.Count);

            // 1. As a pre-aggregation step, bucket the modified documents by AIModelId and ExtractionId
            Dictionary<string, List<Document>> aggregationBuckets = 
                GroupDocumentsByAggregationGroup(modifiedOrCreatedDocuments);

            Dictionary<string, List<AggregationResult>> aggregationResults = new Dictionary<string, List<AggregationResult>>();

            var tasks = new List<Task>();

            Parallel.ForEach(aggregationBuckets.Keys, eachAggregationKey => 
            {
                // 2.1 Fetch the list of aggregations to be executed for a given <AIModelId, ExtractionId>
                List<AggregationRule> aggregationRulesForModelAndExtraction = FetchListOfAggregations(eachAggregationKey).Result;

                foreach (AggregationRule aggregationDocument in aggregationRulesForModelAndExtraction)
                {
                    // Execute each aggregation for the <AIModelId><ExtractionId>
                    aggregationResults[eachAggregationKey] = ExecuteAggregationForModelAndExtraction(eachAggregationKey, aggregationDocument, aggregationBuckets[eachAggregationKey]);

                    Console.WriteLine("5. Size of aggregation results = {0}", aggregationResults[eachAggregationKey].Count);
                    Console.WriteLine("5.1 List of aggregation results for key: {0} = {1}", eachAggregationKey, aggregationResults[eachAggregationKey].Count);
                }

                Console.WriteLine("5.4 Entering here!");
                if (aggregationResults.ContainsKey(eachAggregationKey))
                {
                    this.WriteAggregatedDataToCosmosDB(
                    eachAggregationKey,
                    aggregationResults[eachAggregationKey]).Wait();
                }
            });

            Console.WriteLine("Done!");
        }

        /// <summary>
        /// Update the existing aggregations (if any) for the previously computed aggregations for this AIModelId and ExtractionId
        /// </summary>
        /// <param name="eachAIModelAndExtractionId">The AIModelId for which to calculate the aggregations</param>
        /// <param name="aggregationResults">The list of aggregated results corresponding to each of the Aggregation Rules for this AIModelId and ExtractionId</param>
        /// <returns></returns>
        private async Task WriteAggregatedDataToCosmosDB(
            string eachAIModelAndExtractionId,
            List<AggregationResult> aggregationResults)
        {
            Console.WriteLine("6. Size of aggregation results for key: {0} = {1}", eachAIModelAndExtractionId, aggregationResults.Count);

            foreach (AggregationResult newestAggregationResult in aggregationResults)
            {
                Console.WriteLine("6.1 Aggregation Result = {0}", newestAggregationResult.ToString());

                string aggregationResultId = newestAggregationResult.Id;
                Console.WriteLine("6.2 Aggregation Result Id = {0}", aggregationResultId);

                try
                {
                    Uri documentUri = UriFactory.CreateDocumentUri(this.DatabaseName, this.AggregationResultsCollectionName, aggregationResultId);

                    Console.WriteLine("6.3 Id of Aggregation = {0} and DocumentsUri = {1}", aggregationResultId, documentUri);
                    AggregationResult previouslyAggregatedDocument =
                        await this.DocumentClient.ReadDocumentAsync<AggregationResult>(
                            documentUri,
                            new RequestOptions { PartitionKey = new PartitionKey(newestAggregationResult.PartitionKey) });

                    Console.WriteLine("6.4 Successfully read document");

                    await this.UpsertAggregatedDocument(previouslyAggregatedDocument, newestAggregationResult);

                    Console.WriteLine("Successfully upserted aggregated document");
                }
                catch (DocumentClientException ex)
                {
                    if ((int)ex.StatusCode == 404)
                    {
                        await this.UpsertAggregatedDocument( null, newestAggregationResult);

                        Console.WriteLine("Previously aggregated document not found. Successfully upserted aggregated document");
                    }
                    else
                    {
                        Console.WriteLine("Received DocumentClientException when retrieving and upserting aggregated document. Original exception message was: {0}", ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Received Exception when retrieving and upserting aggregated document. Original exception message was: {0}. Stack trace is:", ex.Message, ex.StackTrace);
                }
                finally
                {
                    Console.WriteLine("Done with finally block");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="preAggregatedDocument">Previously aggregated document to be updated with the latest results</param>
        /// <param name="aggregationResult">Most recent aggregation results to merge into the previously aggregated results</param>
        /// <returns></returns>
        private async Task UpsertAggregatedDocument(
            AggregationResult preAggregatedDocument,
            AggregationResult aggregationResult)
        {
            Uri documentsFeedLink = UriFactory.CreateDocumentCollectionUri(this.DatabaseName, this.AggregationResultsCollectionName);

            try
            {
                Console.WriteLine("About to upsert Document");

                List<string> pass = new List<string>();
                List<string> fail = new List<string>();
                
                if (preAggregatedDocument != null)
                {
                    pass.AddRange(preAggregatedDocument.Pass);
                    fail.AddRange(preAggregatedDocument.Fail);
                }

                // Add newly successful ElementIds for the aggregation only if:
                // 1. It wasn't already part of the 'pass' list from previous runs
                // 2. If it was previously in the 'fail' list but is now successful (also remove from the 'fail' list for the aggregation)
                foreach (string newlyAggregatedSuccessfulElement in aggregationResult.Pass)
                {
                    if(!pass.Contains(newlyAggregatedSuccessfulElement) && !fail.Contains(newlyAggregatedSuccessfulElement))
                    {
                        pass.Add(newlyAggregatedSuccessfulElement);
                    }
                    else if(!pass.Contains(newlyAggregatedSuccessfulElement) && fail.Contains(newlyAggregatedSuccessfulElement))
                    {
                        fail.Remove(newlyAggregatedSuccessfulElement);
                        pass.Add(newlyAggregatedSuccessfulElement);
                    }
                }

                // Add newly failed ElementIds for the aggregation only if:
                // 1. It wasn't already part of the 'fail' list from previous runs
                // 2. If it was previously in the 'pass' list but now fails to meet the aggregation criteria (also remove from the 'pass' list for the aggregation)
                foreach (string newlyAggregatedFailedElement in aggregationResult.Fail)
                {
                    if(!pass.Contains(newlyAggregatedFailedElement) && !fail.Contains(newlyAggregatedFailedElement))
                    {
                        fail.Add(newlyAggregatedFailedElement);
                    }
                    else if (pass.Contains(newlyAggregatedFailedElement) && !fail.Contains(newlyAggregatedFailedElement))
                    {
                        pass.Remove(newlyAggregatedFailedElement);
                        fail.Add(newlyAggregatedFailedElement);
                    }
                }

                aggregationResult.Pass = pass;
                aggregationResult.Fail = fail;

                await this.DocumentClient.UpsertDocumentAsync(documentsFeedLink, aggregationResult);
            }
            catch (DocumentClientException ex)
            {
                if ((int)ex.StatusCode == 429)
                {
                    Console.WriteLine("Rate limited when storing aggregated view. Retrying...");

                    bool success = false;
                    int numRetries = 0;

                    while (!success && numRetries <= this.MaxRetriesOnRateLimitedWritesToCosmosDB)
                    {
                        try
                        {
                            await this.DocumentClient.UpsertDocumentAsync(documentsFeedLink, aggregationResult);
                            success = true;
                        }
                        catch (DocumentClientException iex)
                        {
                            numRetries++;
                        }
                        catch (Exception iex)
                        {
                            numRetries++;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Received exception message when upserting aggregated document: {0}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Exception encountered when attempting to upsert aggregated document. The original exception message was: {0}", ex.Message);
            }            
        }
    }
}

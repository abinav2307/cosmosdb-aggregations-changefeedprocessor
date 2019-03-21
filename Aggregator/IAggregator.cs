
namespace Microsoft.Azure.CosmosDB.Aggregations.Aggregator
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    interface IAggregator
    {
        /// <summary>
        /// Executes an aggregation rule to calculate the max value of the field corresponding to the desired grouping
        /// </summary>
        /// <param name="aggregationPartitionKey">AIModelId, ExtractionId pair to perform the aggregation for</param>
        /// <param name="documentsToAggregate">List of documents for a single AIModelId, ExtractionId pair, to perform the aggregation</param>
        /// <param name="criteriaForAggregation">List of fields witin the documents to be used for the aggregation</param>
        List<AggregationResult> ExecuteAggregationRule(string aggregationPartitionKey, AggregationRule aggregationRule, List<Document> documentsToAggregate);
    }
}

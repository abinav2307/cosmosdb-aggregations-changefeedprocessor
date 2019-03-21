
namespace Microsoft.Azure.CosmosDB.Aggregations.Aggregator
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    public abstract class BaseAggregator : IAggregator
    {
        /// <summary>
        /// Executes an aggregation rule to calculate the max value of the field corresponding to the desired grouping
        /// </summary>
        /// <param name="aggregationPartitionKey">AIModelId, ExtractionId pair to perform the aggregation for</param>
        /// <param name="documentsToAggregate">List of documents for a single AIModelId, ExtractionId pair, to perform the aggregation</param>
        /// <param name="criteriaForAggregation">List of fields witin the documents to be used for the aggregation</param>
        public abstract List<AggregationResult> ExecuteAggregationRule(string aggregationPartitionKey, AggregationRule aggregationRule, List<Document> documentsToAggregate);

        /// <summary>
        /// Create sub buckets within each [AIModelId][ExtractionId] based on the fields used in the aggregation criteria
        /// </summary>
        /// <param name="documentsToAggregate">List of documents for a single AIModelId, ExtractionId pair, to perform the aggregation</param>
        /// <param name="criteriaForAggregation">List of fields witin the documents to be used for the aggregation</param>
        /// <returns></returns>
        public Dictionary<string, List<Document>> ExtractAggregationBuckets(string aggregationPartitionKey, List<Document> documentsToAggregate, List<string> criteriaForAggregation)
        {
            Dictionary<string, List<Document>> aggregationBuckets = new Dictionary<string, List<Document>>();

            foreach (Document eachDocumentToAggregate in documentsToAggregate)
            {
                string aggregationGrouping = "";
                for (int index = 0; index < criteriaForAggregation.Count; index++)
                {
                    string aggregationCriteriaFieldValue = eachDocumentToAggregate.GetPropertyValue<string>(criteriaForAggregation[index]);
                    if (index == 0)
                    {
                        aggregationGrouping = string.Concat(aggregationGrouping, aggregationCriteriaFieldValue);
                    }
                    else
                    {
                        aggregationGrouping = string.Concat(aggregationGrouping, "_", aggregationCriteriaFieldValue);
                    }
                }

                if (aggregationBuckets.ContainsKey(aggregationGrouping))
                {
                    aggregationBuckets[aggregationGrouping].Add(eachDocumentToAggregate);
                }
                else
                {
                    aggregationBuckets[aggregationGrouping] = new List<Document>();
                    aggregationBuckets[aggregationGrouping].Add(eachDocumentToAggregate);
                }
            }

            return aggregationBuckets;
        }
    }
}

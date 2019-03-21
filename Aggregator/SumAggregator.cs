
namespace Microsoft.Azure.CosmosDB.Aggregations.Aggregator
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Documents;

internal sealed class SumAggregator : BaseAggregator
    {
        /// <summary>
        /// List of documents tha failed to meet the aggregation requirement
        /// </summary>
        public List<string> DocumentsFailedToMeetAggregationRule { get; set; }

        /// <summary>
        /// List of documents that successfully met the aggregation requirement
        /// </summary>
        public List<string> DocumentsSucceededAggregationRule { get; set; }

        public SumAggregator()
        {
            DocumentsFailedToMeetAggregationRule = new List<string>();
            DocumentsSucceededAggregationRule = new List<string>();
        }

        /// <summary>
        /// Executes an aggregation rule to calculate the max value of the field corresponding to the desired grouping
        /// </summary>
        /// <param name="aggregationPartitionKey">AIModelId, ExtractionId pair to perform the aggregation for</param>
        /// <param name="documentsToAggregate">List of documents for a single AIModelId, ExtractionId pair, to perform the aggregation</param>
        /// <param name="criteriaForAggregation">List of fields witin the documents to be used for the aggregation</param>
        public override List<AggregationResult> ExecuteAggregationRule(string aggregationPartitionKey, AggregationRule aggregationRule, List<Document> documentsToAggregate)
        {
            List<AggregationResult> aggregationResults = new List<AggregationResult>();

            List<string> criteriaForAggregation = aggregationRule.AggregationGrouping;

            // Fetch the mini aggregation batches within the list of documents to aggregate over
            Dictionary<string, List<Document>> aggregationBuckets = ExtractAggregationBuckets(aggregationPartitionKey, documentsToAggregate, criteriaForAggregation);

            Parallel.ForEach(aggregationBuckets.Keys, eachAggregationKey =>
            {
                List<Document> miniBucketsOfDocumentsToAggregate = aggregationBuckets[eachAggregationKey];

                double aggregatedValue = 0;
                List<string> documentIds = new List<string>();

                // Calculate the max value of the specified property at the aggregation level (Group By)
                foreach (Document eachDocument in miniBucketsOfDocumentsToAggregate)
                {
                    aggregatedValue += eachDocument.GetPropertyValue<double>(aggregationRule.AggregationProperty);
                    documentIds.Add(eachDocument.GetPropertyValue<string>("id"));                    
                }

                if (AggregationComparisonCalculator.IsAggregationComparisonMet(aggregatedValue, aggregationRule))
                {
                    DocumentsSucceededAggregationRule = documentIds;
                }
                else
                {
                    DocumentsFailedToMeetAggregationRule = documentIds;
                }

                AggregationResult aggregationResult = new AggregationResult();
                aggregationResult.Grouping = eachAggregationKey;
                aggregationResult.AggregationRuleResult = aggregatedValue;
                aggregationResult.Pass = DocumentsSucceededAggregationRule;
                aggregationResult.Fail = DocumentsFailedToMeetAggregationRule;
                aggregationResult.AggregationRule = aggregationRule;
                aggregationResult.PartitionKey = aggregationPartitionKey;
                aggregationResult.Id = aggregationRule.Id;

                aggregationResults.Add(aggregationResult);
            });

            return aggregationResults;
        }
    }
}

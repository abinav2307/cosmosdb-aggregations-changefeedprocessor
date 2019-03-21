
namespace Microsoft.Azure.CosmosDB.Aggregations.Aggregator
{
    internal sealed class AggregationComparisonCalculator
    {
        /// <summary>
        /// Determines if the result of the aggregation has met the aggregation conditions as specified in the Aggregation Rule
        /// </summary>
        /// <param name="aggregatedValue">Result of aggregation</param>
        /// <param name="aggregationRule">Aggregation Rule to assess if the result of the aggregation is successful</param>
        /// <returns></returns>
        public static bool IsAggregationComparisonMet(double aggregatedValue, AggregationRule aggregationRule)
        {
            bool isAggregationComparisonSuccessful = false;
            switch (aggregationRule.AggregationComparison)
            {
                case AggregationComparison.EQUALS:
                    if (aggregatedValue == aggregationRule.AggregationRuleThreshold)
                    {
                        isAggregationComparisonSuccessful = true;
                    }
                    else
                    {
                        isAggregationComparisonSuccessful = false;
                    }
                    break;
                                                    
                case AggregationComparison.GREATER_THAN:
                    if (aggregatedValue > aggregationRule.AggregationRuleThreshold)
                    {
                        isAggregationComparisonSuccessful = true;
                    }
                    else
                    {
                        isAggregationComparisonSuccessful = false;
                    }
                    break;

                case AggregationComparison.LESS_THAN: 
                    if (aggregatedValue < aggregationRule.AggregationRuleThreshold)
                    {
                        isAggregationComparisonSuccessful = true;
                    }
                    else
                    {
                        isAggregationComparisonSuccessful = false;
                    }
                    break;
            }

            return isAggregationComparisonSuccessful;
        }
    }
}

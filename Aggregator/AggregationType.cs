
namespace Microsoft.Azure.CosmosDB.Aggregations.Aggregator
{
    public enum AggregationType
    {
        /// <summary>
        /// Rule to calculate the sum across all values of the specified field
        /// </summary>
        SUM,

        /// <summary>
        /// Rule to calculate the min value across all values of the specified field
        /// </summary>
        MIN,

        /// <summary>
        /// Rule to calculate the max value across all values of the specified field
        /// </summary>
        MAX
    }
}

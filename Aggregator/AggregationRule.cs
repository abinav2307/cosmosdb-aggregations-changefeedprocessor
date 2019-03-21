
namespace Microsoft.Azure.CosmosDB.Aggregations.Aggregator
{
    using System.Collections.Generic;
    
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Microsoft.Azure.Documents;

    public class AggregationRule : Document
    {
        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }

        [JsonProperty(PropertyName = "aggregationGrouping")]
        public List<string> AggregationGrouping { get; set; }

        [JsonProperty(PropertyName ="aggregationProperty")]
        public string AggregationProperty { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "aggregationType")]
        public AggregationType AggregationType { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "aggregationComparison")]
        public AggregationComparison AggregationComparison { get; set; }

        [JsonProperty(PropertyName = "aggregationRuleThreshold")]
        public double AggregationRuleThreshold { get; set; }
    }
}

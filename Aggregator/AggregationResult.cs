
namespace Microsoft.Azure.CosmosDB.Aggregations.Aggregator
{
    using System;
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public class AggregationResult
    {
        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }

        [JsonProperty(PropertyName = "aggregationRule")]
        public AggregationRule AggregationRule { get; set; }

        [JsonProperty(PropertyName = "aggregationRuleResult")]
        public double AggregationRuleResult { get; set; }

        [JsonProperty(PropertyName = "pass")]
        public List<string> Pass { get; set; }

        [JsonProperty(PropertyName = "grouping")]
        public string Grouping { get; set; }

        [JsonProperty(PropertyName = "fail")]
        public List<string> Fail { get; set; }

        [JsonProperty(PropertyName = "id")]
        public String Id { get; set; }
    }
}

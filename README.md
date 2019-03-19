## Azure Cosmos DB Aggregated Materialized Views using Change Feed
This project includes a sample to store pre-aggregated results for aggregations to be executed against Cosmos DB containers. The aggregations are computed by leveraging the Cosmos DB Change Feed Processor library.

### This sample contains the following components:

* *Sample Data Generator* : Sample Data generator for the data to aggregate
* *Aggregation Rule Generator* : Data generator to generate sample aggregation rules, which define the parameters of the aggregations
* *Change Feed Processor Executor* : Data aggregation logic leveraging the Cosmos DB Change Feed Processor library for .NET


namespace Microsoft.Azure.CosmosDB.Aggregations.DataGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Configuration;
    using Newtonsoft.Json;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.CosmosDB.Aggregations.CosmosDBHelper;

    internal sealed class EmployeeSampleDataGenerator :  BaseDataGenerator, IDataGenerator
    {
        /// <summary>
        /// The average size of documents to generate and ingest into Cosmos DB
        /// </summary>
        public int DocSizeInKb { get; set; }

        /// <summary>
        /// Number of sample Employers to generate for each Employee
        /// </summary>
        private int NumEmployers;

        /// <summary>
        /// Number of sample Notable Positions to generate for each Employee
        /// </summary>
        private int NumNotablePositions;

        /// <summary>
        /// Number of sample Positions to generate for each Employee
        /// </summary>
        private int NumPositions;

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
        private static Random random = new Random();

        /// <summary>
        /// Maximum number of custom retries on throttled requests
        /// </summary>
        private int MaxRetriesOnThrottles = 10;

        /// <summary>
        /// Creates a new instance of this DataGenerator
        /// </summary>
        /// <param name="DocumentClient">The Cosmos DB DocumentClient</param>
        public EmployeeSampleDataGenerator(DocumentClient client)
        {
            this.DocSizeInKb = int.Parse(ConfigurationManager.AppSettings["DocSizesInKb"]);            
            this.DatabaseName = ConfigurationManager.AppSettings["DatabaseName"];
            this.CollectionName = ConfigurationManager.AppSettings["CollectionName"];
            string cosmosDBEndpointUri = ConfigurationManager.AppSettings["CosmosDBEndpointUri"];
            string accountKey = ConfigurationManager.AppSettings["CosmosDBAuthKey"];

            this.DocumentClient = client;

            DetermineDocumentParameters();

            bool recreateSampleDataCollection = bool.Parse(ConfigurationManager.AppSettings["RecreateSampleDataCollection"]);
            if(recreateSampleDataCollection)
            {
                int sampleDataCollectionThroughput = int.Parse(ConfigurationManager.AppSettings["SampleDataCollectionThroughput"]);
                string sampleDataCollectionPartitionKey = ConfigurationManager.AppSettings["SampleDataCollectionPartitionKey"];

                CosmosDBHelper.CreateCollectionIfNotExistsAsync(
                    this.DocumentClient, 
                    this.DatabaseName, 
                    this.CollectionName, 
                    sampleDataCollectionThroughput, 
                    sampleDataCollectionPartitionKey, 
                    true).Wait();
            }
        }

        /// <summary>
        /// Generates sample data
        /// </summary>
        public override async Task GenerateSampleData()
        {
            List<string> documentsToImport = new List<string>();
            int numEmployeesToGenerate = int.Parse(ConfigurationManager.AppSettings["NumDocumentsToGenerate"]);
            int batchSizeForWrites = int.Parse(ConfigurationManager.AppSettings["BatchSizeForWrites"]);

            int currentBatchCount = 0;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int batchNum = 1;
            for (int eachEmployeeToGenerate = 0; eachEmployeeToGenerate < numEmployeesToGenerate; eachEmployeeToGenerate++)
            {
                Employee sampleEmployee = GenerateSampleEmployeeData();

                string employeeJson = JsonConvert.SerializeObject(sampleEmployee);
                documentsToImport.Add(employeeJson);
                currentBatchCount++;

                if (currentBatchCount == batchSizeForWrites)
                {
                    Console.WriteLine("Batch: {0}", batchNum);
                    batchNum++;

                    await this.WriteToCosmosDB(documentsToImport, this.DatabaseName, this.CollectionName, this.DocumentClient);
                    currentBatchCount = 0;
                    documentsToImport = new List<string>();
                }
            }

            // Flush any remaining sample documents to be written to Cosmos DB
            if(documentsToImport.Count > 0)
            {
                await this.WriteToCosmosDB(documentsToImport, this.DatabaseName, this.CollectionName, this.DocumentClient);
            }

            stopwatch.Stop();
            Console.WriteLine("Time taken = {0} milliseconds", stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Get the database if it exists, null if it doesn't.
        /// </summary>
        /// <returns>The requested database.</returns>
        private Database GetDatabaseIfExists(DocumentClient client, string databaseName)
        {
            return client.CreateDatabaseQuery().Where(d => d.Id == databaseName).AsEnumerable().FirstOrDefault();
        }

        /// <summary>
        /// Get the collection if it exists, null if it doesn't.
        /// </summary>
        /// <returns>The requested collection.</returns>
        private DocumentCollection GetCollectionIfExists(string databaseName, string collectionName)
        {
            if (GetDatabaseIfExists(this.DocumentClient, databaseName) == null)
            {
                return null;
            }

            return this.DocumentClient.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(databaseName))
                .Where(c => c.Id == collectionName).AsEnumerable().FirstOrDefault();
        }

        /// <summary>
        /// Determines the number of entities to be generate within each document, based on the
        /// specified size of each sample document
        /// </summary>
        private void DetermineDocumentParameters()
        {
            switch(this.DocSizeInKb)
            {
                case 2:
                        this.NumEmployers = 1;
                        this.NumNotablePositions = 5;
                        this.NumPositions = 4;
                        break;
                case 5:
                        this.NumEmployers = 4;
                        this.NumNotablePositions = 3;
                        this.NumPositions = 12;
                        break;
                case 1:
                default:
                    this.NumEmployers = 1;
                    this.NumNotablePositions = 1;
                    this.NumPositions = 1;
                    break;
            }
        }

        /// <summary>
        /// Generates a sample entity of the Employee class
        /// </summary>
        /// <returns></returns>
        private Employee GenerateSampleEmployeeData()
        {
            Employee employee = new Employee();
            employee.FirstName = Constants.GetRandomFirstName();
            employee.LastName = Constants.GetRandomLastName();
            employee.EmploymentStartDate = DateTime.Now.AddMonths(-72); ;
            employee.Age = random.Next(22, 50);
            employee.NumPatents = random.Next(0, 10);
            employee.SeniorityInYears = random.Next(0, 8);
            employee.Title = Constants.GetRandomTitle();
            employee.StillEmployed = true;
            employee.CurrentEmployerTicker = "MSFT";

            employee.PartitionKey = employee.FirstName;
            
            employee.EmploymentInfo = new EmploymentInfo();
            employee.EmploymentInfo.Employers = GenerateSampleEmployerData(NumEmployers, NumNotablePositions);
            employee.EmploymentInfo.Positions = GenerateSamplePositions(NumPositions);

            return employee;
        }

        /// <summary>
        /// Generates a sample list of Employer entities
        /// </summary>
        /// <param name="numEmployers">Number of employers to include in the List of Employers</param>
        /// <param name="numNotablePositions">Number of notable positions to generate for the Employee</param>
        /// <returns></returns>
        private List<Employer> GenerateSampleEmployerData(int numEmployers, int numNotablePositions)
        {
            List<Employer> employers = new List<Employer>();

            DateTime earliestDate = DateTime.Now.AddMonths(-72);

            for(int eachEmployer = 0; eachEmployer < numEmployers; eachEmployer++)
            {
                Employer employer = new Employer();
                employer.EmployerName = "Microsoft Corporation";
                employer.PositionsHeld = random.Next(1, 10);
                employer.NumManagers = random.Next(1, employer.PositionsHeld);
                employer.NumDirectReports = random.Next(0, 5);
                employer.ProductName = Constants.GetRandomProductName();
                employer.positions = GenerateSampleNotablePositions(numNotablePositions);

                employers.Add(employer);              
            }

            return employers;
        }

        /// <summary>
        /// Generates a list of Notable Positions for the Employee
        /// </summary>
        /// <param name="numNotablePositions">Number of Notable Employees to generate for this Employee</param>
        /// <returns></returns>
        private List<NotablePosition> GenerateSampleNotablePositions(int numNotablePositions)
        {
            List<NotablePosition> notablePositions = new List<NotablePosition>();

            DateTime earliestDate = DateTime.Now.AddMonths(-72);

            for (int eachNotablePosition = 0; eachNotablePosition < numNotablePositions; eachNotablePosition++)
            {
                NotablePosition notablePosition = new NotablePosition();
                notablePosition.StartDate = earliestDate;
                notablePosition.EndDate = earliestDate.AddMonths(1);
                notablePosition.PositionType = PositionType.FULL_TIME;

                notablePositions.Add(notablePosition);
            }

            return notablePositions;
        }

        /// <summary>
        /// Number of detailed positions to generate for this Employee
        /// </summary>
        /// <param name="numPositions">Number of positions to generate for this Employee</param>
        /// <returns></returns>
        private List<Position> GenerateSamplePositions(int numPositions)
        {
            List<Position> positions = new List<Position>();

            DateTime earliestDate = DateTime.Now.AddMonths(-72);

            for (int eachPosition = 0; eachPosition < numPositions; eachPosition++)
            {
                Position position = new Position();
                position.StartDate = earliestDate;
                position.EndDate = earliestDate.AddMonths(1);
                position.ManagedBy = String.Concat(Constants.GetRandomFirstName(), " ", Constants.GetRandomLastName());
                position.NumAwardedStockUnits = random.Next(0, 50);
                position.NumVestedStockUnits = random.Next(0, position.NumAwardedStockUnits);
                position.Salary = random.Next(100000,500000);

                positions.Add(position);
            }

            return positions;
        }
    }
}

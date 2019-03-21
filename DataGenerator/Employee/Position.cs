
namespace Microsoft.Azure.CosmosDB.Aggregations.DataGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    internal sealed class Position
    {
        /// <summary>
        /// Start date at this Position
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date at this Position
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Manager Name when at this Position
        /// </summary>
        public string ManagedBy { get; set; }

        /// <summary>
        /// Position Type when at this Position
        /// </summary>
        public PositionType PositionType { get; set; }

        /// <summary>
        /// Number of awarded Stock Units of the Employer when at this Position
        /// </summary>
        public int NumAwardedStockUnits { get; set; }

        /// <summary>
        /// Number of vested Stock Units of the Employer when at this Position
        /// </summary>
        public int NumVestedStockUnits { get; set; }

        /// <summary>
        /// Salary when at this Position
        /// </summary>
        public long Salary { get; set; }
    }
}

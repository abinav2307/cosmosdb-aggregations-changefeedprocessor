
namespace Microsoft.Azure.CosmosDB.Aggregations.ChangeFeed
{
    using System;

    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing;

    internal sealed class DocumentFeedObserverFactory : IChangeFeedObserverFactory
    {
        /// <summary>
        /// Instance of the DocumentClient, used to push failed batches of backups to the Cosmos DB collection
        /// tracking failures during the backup process.
        /// </summary>
        private DocumentClient DocumentClient;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="DocumentFeedObserverFactory" /> class.
        /// </summary>
        public DocumentFeedObserverFactory(DocumentClient client)
        {
            this.DocumentClient = client;            
        }

        /// <summary>
        /// Creates a document observer instance.
        /// </summary>
        /// <returns>A new DocumentFeedObserver instance.</returns>
        public IChangeFeedObserver CreateObserver()
        {
            Console.WriteLine("Creating DocumentFeedObserver");

            DocumentFeedObserver newObserver = new DocumentFeedObserver(this.DocumentClient);
            return newObserver as IChangeFeedObserver;
        }
    }
}

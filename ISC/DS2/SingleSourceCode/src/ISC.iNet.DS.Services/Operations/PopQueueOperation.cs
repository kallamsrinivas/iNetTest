using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.iNet;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.Services
{
    public class PopQueueOperation : PopQueueAction, IOperation
    {
        public PopQueueOperation() { }

        public PopQueueOperation( PopQueueAction popQueueAction ) : base( popQueueAction ) { }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Null.</returns>
        public DockingStationEvent Execute()
        {
            Log.Info( "PopQueueOperation: Deleting oldest message from iNet upload queue." );

            bool deleted = PersistedQueue.CreateInetInstance().Delete();

            Log.Info( "PopQueueOperation:" + ( deleted ? "Message deleted." : "No messages to delete." ) );

            return null;
        }
    }
}

using System.Data;

namespace EAABAddIn.Src.Core.Data
{
    public class DatabaseContext
    {
        private IDatabaseStrategy _strategy;

        public void SetStrategy(IDatabaseStrategy strategy)
        {
            _strategy = strategy;
        }

        public IDbConnection Connect(string connectionString)
        {
            if (_strategy == null)
            {
                throw new System.InvalidOperationException("Database strategy not set.");
            }
            return _strategy.GetConnection(connectionString);
        }
    }
}

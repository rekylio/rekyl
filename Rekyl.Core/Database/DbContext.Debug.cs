namespace Rekyl.Core.Database
{
    public partial class DbContext
    {
        internal void Reset()
        {
            R.DbDrop(DatabaseName).Run(_connection);
            Initalized = false;
            CheckAndPopulateIfNeeded();
        }
    }
}

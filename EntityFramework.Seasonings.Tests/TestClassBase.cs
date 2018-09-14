using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFramework.Seasonings.Tests
{
    [TestClass]
    public abstract class TestClassBase
    {
        private DbConnection connection = null;
        protected DatabaseContext GetContext()
        {
            return new DatabaseContext(connection);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            connection = new SQLiteConnection("Data Source=:memory:");
            connection.Open();
            using (DatabaseContext ctx = new DatabaseContext(connection))
            {
                ctx.Database.Initialize(true);
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            connection.Close();
            connection = null;
        }
    }
}

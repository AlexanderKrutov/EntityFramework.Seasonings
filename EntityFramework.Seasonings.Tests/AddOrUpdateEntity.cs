using System;
using System.Linq;
using System.Data.Entity;
using System.Data.Common;
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EntityFramework.Seasonings.Tests.Models;

namespace EntityFramework.Seasonings.Tests
{
    [TestClass]
    public class AddOrUpdateEntity : TestClassBase
    {
        [TestMethod]
        public void AddNewEntity()
        {
            using (DatabaseContext ctx = GetContext())
            {
                Blog blog = new Blog()
                {
                    Id = 7,
                    Title = "New Blog"
                };

                ctx.AddOrUpdateEntity(blog);
                ctx.SaveChanges();
            }

            using (DatabaseContext ctx = GetContext())
            {
                Blog blog = ctx.Blogs.FirstOrDefault(b => b.Id == 7 && b.Title == "New Blog");
                Assert.IsNotNull(blog);
            }
        }

        [TestMethod]
        public void UpdateExistingEntity()
        {
            // check blogs count and title of the first blog
            using (DatabaseContext ctx = GetContext())
            {
                int blogsCount = ctx.Blogs.Count();
                Assert.AreEqual(3, blogsCount);

                Blog blog = ctx.Blogs.FirstOrDefault(b => b.Id == 1);
                Assert.AreEqual("Blog 1", blog.Title);
            }

            // update first blog
            using (DatabaseContext ctx = GetContext())
            {
                Blog blog = new Blog()
                {
                    Id = 1,
                    Title = "Changed Blog Title"
                };

                ctx.AddOrUpdateEntity(blog);
                ctx.SaveChanges();
            }

            // check blogs count not changed
            // check blog title is updated
            using (DatabaseContext ctx = GetContext())
            {
                int blogsCount = ctx.Blogs.Count();
                Assert.AreEqual(3, blogsCount);

                Blog blog = ctx.Blogs.FirstOrDefault(b => b.Id == 1);
                Assert.AreEqual("Changed Blog Title", blog.Title);
            }
        }
    }
}

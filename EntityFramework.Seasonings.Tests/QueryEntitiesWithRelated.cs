﻿using System;
using System.Linq;
using System.Data.Entity;
using System.Data.Common;
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EntityFramework.Seasonings.Tests.Models;

namespace EntityFramework.Seasonings.Tests
{
    [TestClass]
    public class QueryEntitiesWithRelated : TestClassBase
    {
        [TestMethod]
        public void QueryAllProperties()
        {
            using (DatabaseContext ctx = GetContext())
            {
                var blog = ctx.QueryEntitiesWithRelated<Blog>().FirstOrDefault(b => b.Id == 1);
                Assert.AreEqual(2, blog.Posts.Count);
                Assert.AreEqual(1, blog.Posts.ElementAt(0).Comments.Count);
                Assert.AreEqual(blog.Posts.ElementAt(0).Id, blog.Posts.ElementAt(0).Author.Posts.ElementAt(0).Id);
            }
        }

        [TestMethod]
        public void QueryNamedPropertiesOnly()
        {
            using (DatabaseContext ctx = GetContext())
            {
                var blog = ctx.QueryEntitiesWithRelated<Blog>("Posts", "Posts.Author").FirstOrDefault(b => b.Id == 1);
                Assert.AreEqual(2, blog.Posts.Count);
                Assert.IsTrue(blog.Posts.All(p => p.Author != null));
                Assert.IsTrue(blog.Posts.All(p => p.Comments == null));
            }
        }
    }
}

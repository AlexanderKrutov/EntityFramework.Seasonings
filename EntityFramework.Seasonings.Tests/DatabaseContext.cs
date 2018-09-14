using EntityFramework.Seasonings.Tests.Models;
using SQLite.CodeFirst;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFramework.Seasonings.Tests
{
    public class DatabaseContext : DbContext
    {
        public DbSet<Author> Authors { get; set; }
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }

        public DatabaseContext(DbConnection connection) : base(connection, false)
        {
            Configuration.ProxyCreationEnabled = false;
            Configuration.LazyLoadingEnabled = false;
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            System.Data.Entity.Database.SetInitializer(new DatabaseInitializer(modelBuilder));
        }

        private class DatabaseInitializer : SqliteDropCreateDatabaseAlways<DatabaseContext>
        {
            public DatabaseInitializer(DbModelBuilder modelBuilder) : base(modelBuilder)
            {
                modelBuilder.Entity<Author>()
                    .ToTable("Authors")
                    .HasMany(a => a.Posts)
                    .WithRequired(p => p.Author)
                    .HasForeignKey(p => p.AuthorId);

                modelBuilder.Entity<Blog>()
                    .ToTable("Blogs")
                    .HasMany(s => s.Posts)
                    .WithRequired()
                    .HasForeignKey(p => p.BlogId);

                modelBuilder.Entity<Post>()
                    .ToTable("Posts")
                    .HasMany(p => p.Comments)
                    .WithRequired()
                    .HasForeignKey(c => c.PostId);

                modelBuilder.Entity<Comment>()
                    .ToTable("Comments");
            }

            protected override void Seed(DatabaseContext ctx)
            {
                base.Seed(ctx);

                var authors = new List<Author>
                {
                    new Author() {  Id = 1000, FirstName = "John", LastName = "Doe" },
                    new Author() {  Id = 1001, FirstName = "Alice", LastName = "Henderson" },
                    new Author() {  Id = 1002, FirstName = "Bob", LastName = "Sanders" },
                };
                ctx.Authors.AddRange(authors);
                ctx.SaveChanges();

                var blogs = new List<Blog>
                {
                    new Blog { Id = 1, Title = "Blog 1" },
                    new Blog { Id = 2, Title = "Blog 2" },
                    new Blog { Id = 3, Title = "Blog 3" },
                };
                ctx.Blogs.AddRange(blogs);
                ctx.SaveChanges();

                var posts = new List<Post>
                {
                    new Post() { Id = 10, BlogId = 1, Text = "Post 1 in Blog 1", AuthorId = 1000 },
                    new Post() { Id = 11, BlogId = 1, Text = "Post 2 in Blog 1", AuthorId = 1000 },
                    new Post() { Id = 20, BlogId = 2, Text = "Post 1 in Blog 2", AuthorId = 1001 },
                    new Post() { Id = 30, BlogId = 3, Text = "Post 1 in Blog 3", AuthorId = 1002 }
                };
                ctx.Posts.AddRange(posts);
                ctx.SaveChanges();

                var comments = new List<Comment>
                {
                    new Comment() { Id = 100, PostId = 10, Text = "Comment 1 for Post 1 in Blog 1" },
                    new Comment() { Id = 200, PostId = 20, Text = "Comment 1 for Post 1 in Blog 2" },
                };

                ctx.Comments.AddRange(comments);
                ctx.SaveChanges();
            }

        }
    }
}

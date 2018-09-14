# EntityFramework.Seasonings
This is a set of helpful extensions for Entity Framework. 
The main aim of this tiny lib is to reduce amount of boilerplate code.

The library provides convenient ways to add, update and delete entities in a less verbose manner than Entity Framework requires usually. 

The library still is under development so please consider the source code as experimental, unstable and not ready for production.

## Samples

### Querying with related entities 
#### Pure EF
```cs
using (var ctx = new DatabaseContext())
{
    var blog = ctx.Blogs
        .Include(b => b.Posts)
        .Include(b => b.Posts.Select(p => p.Author))
        .Include(b => b.Posts.Select(p => p.Comments))
        .FirstOrDefault();
}
```
#### With Seasonings
```cs
using (var ctx = new DatabaseContext())
{
    var blog = ctx.QueryEntitiesWithRelated<Blog>().FirstOrDefault();
}
```

### Add or update entity
#### Pure EF
```cs
using (var ctx = new DatabaseContext())
{
    if (ctx.Posts.Any(p => p.Id == post.Id))
    {
       ctx.Entry(blog).State = EntityState.Modified;
    }
    else
    {
       ctx.Entry(blog).State = EntityState.Added;
    }
    ctx.SaveChanges();
}
```
#### With Seasonings
```cs
using (var ctx = new DatabaseContext())
{
    ctx.AddOrUpdateEntity<Blog>(blog);
    ctx.SaveChanges();
}
```

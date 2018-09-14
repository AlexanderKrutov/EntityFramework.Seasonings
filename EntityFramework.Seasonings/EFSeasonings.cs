using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EntityFramework.Seasonings
{
    /// <summary>
    /// EntityFramework.Seasonings is a set of helpful extensions for Entity Framework.
    /// </summary>
    public static class EFSeasonings
    {
        /// <summary>
        /// Adds entity to corresponding db set if an entity with same primary key does not exist in database, 
        /// or updates existing instance with the same primary key.
        /// </summary>
        /// <typeparam name="TEntity">Entity class</typeparam>
        /// <param name="ctx">Context instance</param>
        /// <param name="entity">Entity instance to add or update.</param>
        public static void AddOrUpdateEntity<TEntity>(this DbContext ctx, TEntity entity) where TEntity : class
        {
            Type clrType = typeof(TEntity);
            EntityType entityType = ctx.GetEntityType(clrType);

            if (entityType == null)
            {
                throw new ArgumentException($"Class `{clrType.Name}` is not an entity type for database context {ctx.GetType().Name}.");
            }

            // name of key property
            string keyPropertyName = entityType.KeyProperties.First().Name;

            // key property
            PropertyInfo keyProperty = clrType.GetProperty(keyPropertyName);

            // key value
            object key = keyProperty.GetValue(entity);

            // Create "e" portion of lambda expression
            ParameterExpression parameter = Expression.Parameter(clrType, "e");

            // create "e.Id" portion of lambda expression
            MemberExpression expProperty = Expression.Property(parameter, keyProperty.Name);

            // create "'id'" portion of lambda expression
            var expKeyConstant = Expression.Constant(key);

            // create "e.Id == 'id'" portion of lambda expression
            var expEqual = Expression.Equal(expProperty, expKeyConstant);

            // finally create entire expression: "e => e.Id == 'id'"
            Expression<Func<TEntity, bool>> predicate = Expression.Lambda<Func<TEntity, bool>>(expEqual, new[] { parameter });

            // search existing entity
            var existing = ctx.QueryEntitiesWithRelated<TEntity>().FirstOrDefault(predicate);

            if (existing != null)
            {
                var navProperties = entityType.NavigationProperties.Select(np => ctx.Entry(entity).Member(np.Name)).ToArray();

                var collectionNavProperties = navProperties.OfType<DbCollectionEntry>().ToArray();
                if (collectionNavProperties.Any())
                {
                    UpdateRelatedCollections(ctx, existing, entity, collectionNavProperties);
                }

                var singleNavProperties = navProperties.OfType<DbReferenceEntry>().Where(np => np.CurrentValue != null).ToArray();
                foreach (var navProperty in singleNavProperties)
                {
                    ctx.Entry(navProperty.CurrentValue).State = EntityState.Added;
                }

                ctx.Entry(existing).CurrentValues.SetValues(entity);
            }
            else
            {
                ctx.Set<TEntity>().Add(entity);
            }

            var saveBehaviourProperties = clrType.GetProperties().Where(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(SaveConstraintAttribute))).ToList();
            foreach (var property in saveBehaviourProperties)
            {
                SaveConstraint constraint = property.GetCustomAttribute<SaveConstraintAttribute>().Behaviour;

                var edmMember = entityType.DeclaredMembers.First(p => p.Name == property.Name);

                // do not modify property if new value is default value
                if (constraint == SaveConstraint.NotEmpty && GetDefaultValueOfType(property.PropertyType) == property.GetValue(entity))
                {
                    if (edmMember.BuiltInTypeKind == BuiltInTypeKind.NavigationProperty)
                    {
                        // do nothing, navigation property with null value not saved by default
                    }
                    else if (edmMember.BuiltInTypeKind == BuiltInTypeKind.EdmProperty)
                    {
                        ctx.Entry(existing).Property(property.Name).IsModified = false;
                    }
                }

                // do not modify property if it's a referenced entity and it's already exist in db
                if (constraint == SaveConstraint.NotExists && existing != null)
                {
                    if (edmMember.BuiltInTypeKind == BuiltInTypeKind.NavigationProperty)
                    {
                        object newPropertyValue = property.GetValue(entity);
                        object existingPropertyValue = ctx.SearchEntityByKey(newPropertyValue);
                        if (newPropertyValue != null && existingPropertyValue != null)
                        {
                            ctx.Entry(newPropertyValue).State = EntityState.Detached;
                        }
                    }
                    else if (edmMember.BuiltInTypeKind == BuiltInTypeKind.EdmProperty)
                    {
                        throw new Exception($"{property.Name} property of type {clrType.Name} is not navigation property. {nameof(SaveConstraint.NotExists)} constraint can be applied for navigation properties only.");
                    }
                }

                // do not save properties marked with "Never"
                if (constraint == SaveConstraint.Never)
                {
                    if (edmMember.BuiltInTypeKind == BuiltInTypeKind.NavigationProperty)
                    {
                        object newPropertyValue = property.GetValue(entity);
                        if (newPropertyValue != null)
                        {
                            ctx.Entry(newPropertyValue).State = EntityState.Detached;
                        }
                    }
                    else if (edmMember.BuiltInTypeKind == BuiltInTypeKind.EdmProperty)
                    {
                        ctx.Entry(existing).Property(property.Name).IsModified = false;
                    }
                }
            }
        }

        /// <summary>
        /// Searches existing entity in database by its key.
        /// </summary>
        /// <param name="ctx">Context instance</param>
        /// <param name="entity">Entity instance to get key value from.</param>
        /// <returns>Existing entity instance or null if not found</returns>
        public static object SearchEntityByKey(this DbContext ctx, object entity)
        {
            Type clrType = entity.GetType();
            EntityType entityType = ctx.GetEntityType(clrType);

            if (entityType == null)
            {
                throw new ArgumentException($"Class `{clrType.Name}` is not an entity type for database context {ctx.GetType().Name}.");
            }

            // name of key property
            string keyPropertyName = entityType.KeyProperties.First().Name;

            // key property
            PropertyInfo keyProperty = clrType.GetProperty(keyPropertyName);

            // key value
            object key = keyProperty.GetValue(entity);

            // Create "e" portion of lambda expression
            ParameterExpression parameter = Expression.Parameter(clrType, "e");

            // create "e.Id" portion of lambda expression
            MemberExpression expProperty = Expression.Property(parameter, keyProperty.Name);

            // create "'id'" portion of lambda expression
            var expKeyConstant = Expression.Constant(key);

            // create "e.Id == 'id'" portion of lambda expression
            var expEqual = Expression.Equal(expProperty, expKeyConstant);

            // finally create entire expression: "e => e.Id == 'id'"

            var genericFunc = typeof(Func<,>).MakeGenericType(clrType, typeof(bool));

            var genericExpr = typeof(Expression<>).MakeGenericType(genericFunc);

            var predicate = Expression.Lambda(genericFunc, expEqual, new[] { parameter });

            // this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate

            var genericQueryable = typeof(IQueryable<>).MakeGenericType(clrType);

            var firstOrDefault = typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(x => x.Name == "FirstOrDefault" && x.GetParameters().Count() == 2);

            var genericFirstOrDefault = firstOrDefault.MakeGenericMethod(new[] { clrType });

            return genericFirstOrDefault.Invoke(null, new object[] { ctx.Set(clrType), predicate });
        }

        /// <summary>
        /// Deletes entities by predicate
        /// </summary>
        /// <typeparam name="TEntity">Entity class</typeparam>
        /// <param name="ctx">Context instance</param>
        /// <param name="predicate">Predicate to search entities to be deleted.</param>
        public static void DeleteEntities<TEntity>(this DbContext ctx, Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            var entities = ctx.Set<TEntity>().Where(predicate);
            foreach (TEntity e in entities)
            {
                ctx.Entry(e).State = EntityState.Deleted;
            }
        }

        /// <summary>
        /// Updates entities by applying update action for each entity instance found by predicate
        /// </summary>
        /// <typeparam name="TEntity">Entity class</typeparam>
        /// <param name="ctx">Context instance</param>
        /// <param name="predicate">Predicate to search entities to be updated.</param>
        /// <param name="updateAction">Action to be applied to each found entity.</param>
        public static void UpdateEntities<TEntity>(this DbContext ctx, Expression<Func<TEntity, bool>> predicate, Action<TEntity> updateAction) where TEntity : class
        {
            var entities = ctx.Set<TEntity>().Where(predicate);
            foreach (TEntity e in entities)
            {
                updateAction(e);
                ctx.Entry(e).CurrentValues.SetValues(e);
            }
        }

        private static void UpdateRelatedCollections<TEntity>(DbContext ctx, TEntity originalEntity, TEntity modifiedEntity, DbCollectionEntry[] collectionNavProperties) where TEntity : class
        {
            foreach (var navProperty in collectionNavProperties)
            {
                // navigation property info
                var navPropertyInfo = typeof(TEntity).GetProperty(navProperty.Name);

                // type of element for the collection
                var elementType = navPropertyInfo.PropertyType.GenericTypeArguments[0];

                // corresponding entity type
                EntityType elementEntityType = ctx.GetEntityType(elementType);

                // Name of primary key for the collection item entity
                string keyPropertyName = elementEntityType.KeyMembers[0].Name;

                // Getter of primary key for the collection item entity
                MethodInfo keyGetter = elementType.GetProperty(keyPropertyName, BindingFlags.Public | BindingFlags.Instance).GetGetMethod();
                Func<object, object> keySelector = item => keyGetter.Invoke(item, null);
                Func<object, object, bool> itemComparer = (first, second) => keyGetter.Invoke(first, null) == keyGetter.Invoke(second, null);

                IEnumerable originalItems = navPropertyInfo.GetValue(originalEntity) as IEnumerable;
                IEnumerable modifiedItems = navPropertyInfo.GetValue(modifiedEntity) as IEnumerable;

                if (originalItems != null && modifiedItems != null)
                {
                    CompareCollections(originalItems, modifiedItems,
                        (newItem) => ctx.Set(elementType).Add(newItem),
                        (originalItem) => ctx.Set(elementType).Remove(originalItem),
                        (originalItem, modifiedItem) => ctx.Entry(originalItem).CurrentValues.SetValues(modifiedItem),
                        itemComparer);
                }
            }
        }

        /// <summary>
        /// Compares two collections (original and modified) and performs actions for each result of comparison.
        /// </summary>
        /// <param name="originalItems">Original items collection</param>
        /// <param name="modifiedItems">Modified items collection</param>
        /// <param name="addedItemsAction">Action to be performed on each new item. New item means that it exists only in modified collection. Action argument is a new item.</param>
        /// <param name="removedItemsAction">Action to be performed on each removed item. Removed item means that it exists only in original collection. Action argument is a removed item.</param>
        /// <param name="updatedItemsAction">Action to be performed on each updated item. Updated means that it exists in both collections. First action argument is the item instance in original collection, second argument is the item instance in modified collection.</param>
        /// <param name="itemComparer">Function to compare two items. Returns true if items are equal, false otherwise.</param>
        private static void CompareCollections(IEnumerable originalItems, IEnumerable modifiedItems, Action<object> addedItemsAction, Action<object> removedItemsAction, Action<object, object> updatedItemsAction, Func<object, object, bool> itemComparer)
        {
            List<object> addedItems = new List<object>();
            List<object> removedItems = new List<object>();
            List<Tuple<object, object>> updatedItems = new List<Tuple<object, object>>();

            foreach (object originalItem in originalItems)
            {
                bool isRemovedItem = true;
                foreach (object modifiedItem in modifiedItems)
                {
                    if (itemComparer(originalItem, modifiedItem))
                    {
                        updatedItems.Add(new Tuple<object, object>(originalItem, modifiedItem));
                        isRemovedItem = false;
                        break;
                    }
                }

                if (isRemovedItem)
                {
                    removedItems.Add(originalItem);
                }
            }

            foreach (object modifiedItem in modifiedItems)
            {
                bool isNewItem = true;
                foreach (object originalItem in originalItems)
                {
                    if (itemComparer(originalItem, modifiedItem))
                    {
                        isNewItem = false;
                        break;
                    }
                }

                if (isNewItem)
                {
                    addedItems.Add(modifiedItem);
                }
            }

            addedItems.ForEach(i => addedItemsAction.Invoke(i));
            removedItems.ForEach(i => removedItemsAction.Invoke(i));
            updatedItems.ForEach(i => updatedItemsAction.Invoke(i.Item1, i.Item2));
        }

        /// <summary>
        /// Returns a <see cref="DbQuery{TEntity}"/> instance, with ability to include related entities by their property paths.
        /// </summary>
        /// <typeparam name="TEntity">The type entity for which a set should be returned.</typeparam>
        /// <param name="ctx">Context instance</param>
        /// <param name="relatedPropertiesNames">Array of related entities names to be included.</param>
        /// <returns>A set for the given entity type.</returns>
        public static DbQuery<TEntity> QueryEntitiesWithRelated<TEntity>(this DbContext ctx, params string[] relatedPropertiesNames) where TEntity : class
        {
            DbQuery<TEntity> query = ctx.Set<TEntity>();
            foreach (string name in relatedPropertiesNames)
            {
                query = query.Include(name);
            }
            return query;
        }

        /// <summary>
        /// Returns a <see cref="DbQuery{TEntity}"/> instance including all related entities.
        /// </summary>
        /// <typeparam name="TEntity">The type entity for which a set should be returned.</typeparam>
        /// <param name="ctx">Context instance</param>
        /// <returns>A set for the given entity type.</returns>
        public static DbQuery<TEntity> QueryEntitiesWithRelated<TEntity>(this DbContext ctx) where TEntity : class
        {
            DbQuery<TEntity> query = ctx.Set<TEntity>();
            List<string> names = GetNavigationPropertiesNames(ctx, typeof(TEntity));
            foreach (string name in names)
            {
                query = query.Include(name);
            }
            return query;
        }

        /// <summary>
        /// Gets entity type by CLR type.
        /// </summary>
        /// <param name="ctx">Context instance</param>
        /// <param name="type">Type to get EntityType for</param>
        /// <returns><see cref="EntityType"/> instance if type is an entity type, null otherwise.</returns>
        public static EntityType GetEntityType(this DbContext ctx, Type type)
        {
            // Will not work for EF Core: https://stackoverflow.com/a/37651883 
            // should use context.Model, then model.FindEntityType(source.Type) 

            ObjectItemCollection objectItemCollection = (ObjectItemCollection)((IObjectContextAdapter)ctx).ObjectContext.MetadataWorkspace.GetItemCollection(DataSpace.OSpace);
            ReadOnlyCollection<EntityType> entityTypes = objectItemCollection.GetItems<EntityType>();

            return entityTypes.FirstOrDefault(et => objectItemCollection.GetClrType(et) == type);
        }

        private static List<string> GetNavigationPropertiesNames(DbContext ctx, Type type, string prefix = null)
        {
            List<string> names = new List<string>();
            EntityType entityType = ctx.GetEntityType(type);
            if (entityType != null)
            {
                var navProperties = entityType.NavigationProperties.ToArray();
                foreach (var prop in navProperties)
                {
                    string propName = (prefix != null ? $"{prefix}." : "") + prop.Name;
                    Type propType = type.GetProperty(prop.Name).PropertyType;
                    if (typeof(IEnumerable).IsAssignableFrom(propType) && propType.GetGenericArguments().Length == 1)
                    {
                        propType = propType.GetGenericArguments()[0];
                    }

                    names.Add(propName);
                    names.AddRange(GetNavigationPropertiesNames(ctx, propType, propName));
                }
            }

            return names;
        }

        private static object GetDefaultValueOfType(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SaveConstraintAttribute : Attribute
    {
        public SaveConstraint Behaviour { get; private set; }
        public SaveConstraintAttribute(SaveConstraint behaviour)
        {
            Behaviour = behaviour;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum SaveConstraint
    {
        /// <summary>
        /// Property value will be saved with default save behaviour
        /// </summary>
        Default = 0,

        /// <summary>
        /// Property value will be saved only if its value is not null for reference types / not default for value types.
        /// </summary>
        NotEmpty = 1,

        /// <summary>
        /// Navigation property value will be saved only when referenced entity with specified primary key does not exist in database.
        /// </summary>
        NotExists = 2,

        /// <summary>
        /// Property will be never saved
        /// </summary>
        Never = 3
    }
}

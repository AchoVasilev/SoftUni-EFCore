using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MiniORM
{
    internal class ChangeTracker<T> where T : class, new()
    {
        private readonly List<T> allEntities;

        private readonly List<T> added;

        private readonly List<T> removed;

        public ChangeTracker(IEnumerable<T> entities)
        {
            this.added = new List<T>();
            this.removed = new List<T>();

            this.allEntities = CloneEntities(entities);
        }

        public IReadOnlyCollection<T> AllEntities => this.allEntities.AsReadOnly();

        public IReadOnlyCollection<T> Added => this.added.AsReadOnly();

        public IReadOnlyCollection<T> Removed => this.removed.AsReadOnly();

        public void Add(T item) => this.added.Add(item);

        public void Remove(T item) => this.removed.Add(item);

        public IEnumerable<T> GetModifiedEntities(DbSet<T> dbSet)
        {
            List<T> modifiedEntities = new List<T>();

            PropertyInfo[] primaryKeys = typeof(T).GetProperties()
                .Where(pi => pi.HasAttribute<KeyAttribute>())
                .ToArray();

            foreach (var proxyEntity in allEntities)
            {
                object[] primaryKeyValues = GetPrimaryKeyValues(primaryKeys, proxyEntity).ToArray();

                T entity = dbSet.Entities
                    .Single(e => GetPrimaryKeyValues(primaryKeys, e).SequenceEqual(primaryKeyValues));

                bool isModified = IsModified(proxyEntity, entity);

                if (isModified)
                {
                    modifiedEntities.Add(entity);
                }
            }

            return modifiedEntities;
        }

        private bool IsModified(T proxyEntity, T entity)
        {
            PropertyInfo[] monitoredProperties = typeof(T).GetProperties()
                .Where(pi => DbContext.AllowedSqlTypes.Contains(pi.PropertyType))
                .ToArray();

            PropertyInfo[] modifiedProperties = monitoredProperties
                .Where(pi => !Equals(pi.GetValue(entity), pi.GetValue(proxyEntity)))
                .ToArray();

            bool isModified = modifiedProperties.Any();

            return isModified;
        }

        private IEnumerable<object> GetPrimaryKeyValues(IEnumerable<PropertyInfo> primaryKeys, T proxyEntity)
        {
            return primaryKeys.Select(pk => pk.GetValue(proxyEntity));
        }

        private List<T> CloneEntities(IEnumerable<T> entities)
        {
            List<T> clonedEntities = new List<T>();

            PropertyInfo[] propertiesToClone = typeof(T).GetProperties()
                .Where(pi => DbContext.AllowedSqlTypes.Contains(pi.GetType()))
                .ToArray();

            foreach (var entity in entities)
            {
                T clone = Activator.CreateInstance<T>();

                foreach (var property in propertiesToClone)
                {
                    object value = property.GetValue(entity);

                    property.SetValue(clone, value);
                }

                clonedEntities.Add(clone);
            }

            return clonedEntities;
        }
    }
}
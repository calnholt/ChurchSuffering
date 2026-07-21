#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using ChurchSuffering.ECS.Core;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Services
{
    public static class ComponentLoggerService
    {
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldCache = new();
        private const int DefaultMaxDepth = 6;
        private const int CollectionCap = 20;

        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogComponent(IComponent component, string context, int maxDepth = DefaultMaxDepth)
        {
            if (component == null) return;

            var compObj = SerializeComponent(component, 1, maxDepth);
            var components = new JsonObject { [component.GetType().Name] = compObj };

            var entry = new JsonObject
            {
                ["entityId"] = component.Owner?.Id ?? -1,
                ["entityName"] = component.Owner?.Name ?? "unknown",
                ["components"] = components
            };

            LoggingService.Append(context, entry);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogEntity(Entity entity, string context, int maxDepth = DefaultMaxDepth)
        {
            if (entity == null) return;

            var components = new JsonObject();
            foreach (var component in entity.GetAllComponents())
                components[component.GetType().Name] = SerializeComponent(component, 1, maxDepth);

            var entry = new JsonObject
            {
                ["entityId"] = entity.Id,
                ["entityName"] = entity.Name,
                ["components"] = components
            };

            LoggingService.Append(context, entry);
        }

        // --- Serialization ---

        private static JsonObject SerializeComponent(IComponent component, int depth, int maxDepth)
        {
            var obj = new JsonObject();
            if (depth > maxDepth) { obj["_truncated"] = "(max depth)"; return obj; }

            foreach (var field in GetFields(component.GetType()))
            {
                var node = SerializeValue(field.GetValue(component), depth, maxDepth);
                if (node != null) obj[field.Name] = node;
            }
            return obj;
        }

        private static JsonNode? SerializeValue(object? value, int depth, int maxDepth)
        {
            if (value == null) return null;

            var type = value.GetType();

            if (IsPrimitiveLike(type)) return SerializePrimitive(value);

            switch (value)
            {
                case Vector2 vec:
                    return new JsonObject { ["x"] = MathF.Round(vec.X, 2), ["y"] = MathF.Round(vec.Y, 2) };

                case Color color:
                    return new JsonObject { ["r"] = color.R, ["g"] = color.G, ["b"] = color.B, ["a"] = color.A };

                case Entity entity:
                    return new JsonObject { ["id"] = entity.Id, ["name"] = entity.Name };

                case IComponent component when depth < maxDepth:
                    return SerializeComponent(component, depth + 1, maxDepth);

                case IComponent component:
                    return JsonValue.Create($"[{type.Name}] (max depth)");

                case IDictionary dict:
                    return SerializeDictionary(dict, depth, maxDepth);

                case IEnumerable enumerable when value is not string:
                    return SerializeEnumerable(enumerable, depth, maxDepth);

                case IDisposable:
                    return JsonValue.Create($"[{type.Name}]");

                default:
                    return SerializeObject(value, type, depth, maxDepth);
            }
        }

        private static JsonNode? SerializePrimitive(object value) => value switch
        {
            bool b    => JsonValue.Create(b),
            byte by   => JsonValue.Create(by),
            int i     => JsonValue.Create(i),
            long l    => JsonValue.Create(l),
            float f   => JsonValue.Create(f),
            double d  => JsonValue.Create(d),
            decimal m => JsonValue.Create(m),
            DateTime dt => JsonValue.Create(dt.ToString("O")),
            TimeSpan ts => JsonValue.Create(ts.ToString()),
            _           => JsonValue.Create(value.ToString() ?? string.Empty)
        };

        private static JsonArray SerializeEnumerable(IEnumerable enumerable, int depth, int maxDepth)
        {
            var arr = new JsonArray();
            int count = 0;
            foreach (var item in enumerable)
            {
                if (count >= CollectionCap) { arr.Add("...more"); break; }
                var node = SerializeValue(item, depth + 1, maxDepth);
                arr.Add(node ?? JsonValue.Create("null"));
                count++;
            }
            return arr;
        }

        private static JsonObject SerializeDictionary(IDictionary dict, int depth, int maxDepth)
        {
            var obj = new JsonObject();
            int count = 0;
            foreach (DictionaryEntry entry in dict)
            {
                if (count >= CollectionCap) { obj[$"[{count}]"] = $"...more"; break; }
                var keyNode = SerializeValue(entry.Key, depth + 1, maxDepth);
                var valNode = SerializeValue(entry.Value, depth + 1, maxDepth);
                obj[$"[{count}]key"] = keyNode;
                if (valNode != null) obj[$"[{count}]val"] = valNode;
                count++;
            }
            return obj;
        }

        private static JsonNode? SerializeObject(object obj, Type type, int depth, int maxDepth)
        {
            var fields = GetFields(type);
            if (fields.Length == 0) return JsonValue.Create(obj.ToString() ?? string.Empty);

            var result = new JsonObject();
            foreach (var field in fields)
            {
                var node = SerializeValue(field.GetValue(obj), depth + 1, maxDepth);
                if (node != null) result[field.Name] = node;
            }
            return result;
        }

        private static bool IsPrimitiveLike(Type type) =>
            type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(TimeSpan);

        private static FieldInfo[] GetFields(Type type) =>
            FieldCache.GetOrAdd(type, t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));
    }
}

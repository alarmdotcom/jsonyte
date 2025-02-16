﻿using System;
using System.Text.Json;
using Jsonyte.Serialization;
using Jsonyte.Serialization.Contracts;
using Jsonyte.Serialization.Metadata;
using Jsonyte.Validation;

namespace Jsonyte.Converters.Objects
{
    internal class JsonApiResourceObjectRelationshipConverter<T> : JsonApiRelationshipDetailsConverter<T>
    {
        private JsonTypeInfo? info;

        private JsonObjectConverter? objectConverter;

        public override RelationshipResource<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }

        public override RelationshipResource<T> Read(ref Utf8JsonReader reader, ref TrackedResources tracked, JsonSerializerOptions options)
        {
            var relationship = default(RelationshipResource<T>);

            var state = reader.ReadRelationship();

            while (reader.IsInObject())
            {
                var name = reader.ReadMember(ref state);

                if (name == RelationshipFlags.Data)
                {
                    relationship = ReadWrapped(ref reader, ref tracked, default, options);
                }
                else
                {
                    reader.Skip();
                }

                reader.Read();
            }

            state.Validate();

            return relationship;
        }

        public override RelationshipResource<T> ReadWrapped(ref Utf8JsonReader reader, ref TrackedResources tracked, RelationshipResource<T> existingValue, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return default;
            }

            var identifier = reader.ReadResourceIdentifier();

            if (tracked.TryGetIncluded(identifier, out var included))
            {
                return new RelationshipResource<T>((T) included.Value);
            }

            EnsureTypeInfo(options);

            var relationship = info!.Creator();

            if (relationship == null)
            {
                return default;
            }

            var id = identifier.Id;
            var type = identifier.Type;

            var idString = id.GetString();
            var typeString = type.GetString();

            info.IdMember.SetValue(relationship, idString);
            info.TypeMember.SetValue(relationship, typeString);

            tracked.SetIncluded(identifier, idString, typeString, GetConverter(options), relationship);

            return new RelationshipResource<T>((T) relationship);
        }

        public override void Write(Utf8JsonWriter writer, RelationshipResource<T> value, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }

        public override void Write(Utf8JsonWriter writer, ref TrackedResources tracked, RelationshipResource<T> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(JsonApiMembers.DataEncoded);

            WriteWrapped(writer, ref tracked, value, options);

            writer.WriteEndObject();
        }

        public override void WriteWrapped(Utf8JsonWriter writer, ref TrackedResources tracked, RelationshipResource<T> value, JsonSerializerOptions options)
        {
            if (value.Resource == null)
            {
                writer.WriteNullValue();

                return;
            }

            EnsureTypeInfo(options);

            var id = info!.IdMember.GetValue(value.Resource) as string;
            var type = info.TypeMember.GetValue(value.Resource) as string;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type))
            {
                throw new JsonApiException($"JSON:API relationship for '{typeof(T).Name}' must have both 'id' and 'type' values");
            }

            var idEncoded = id!.ToByteArray();
            var typeEncoded = type!.ToByteArray();

            writer.WriteStartObject();

            writer.WriteString(JsonApiMembers.IdEncoded, idEncoded);
            writer.WriteString(JsonApiMembers.TypeEncoded, typeEncoded);

            writer.WriteEndObject();

            tracked.SetIncluded(idEncoded, typeEncoded, id!, type!, GetConverter(options), value.Resource);
        }

        private JsonObjectConverter GetConverter(JsonSerializerOptions options)
        {
            return objectConverter ??= options.GetObjectConverter<T>();
        }

        private void EnsureTypeInfo(JsonSerializerOptions options)
        {
            info ??= options.GetTypeInfo(typeof(T));
        }
    }
}

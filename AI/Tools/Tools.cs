using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UnityREST.AI
{
    /// <summary>A function the model can call. <see cref="Parameters"/> is a JSON Schema object.</summary>
    public interface ITool
    {
        string Name { get; }

        string Description { get; }

        JObject Parameters { get; }

        /// <summary>Runs the tool. Return text (often JSON) for the model; throw to report an error to it.</summary>
        UniTask<string> InvokeAsync(JObject arguments, CancellationToken cancellationToken);
    }

    /// <summary>Delegate-backed <see cref="ITool"/>.</summary>
    public sealed class Tool : ITool
    {
        private readonly Func<JObject, CancellationToken, UniTask<string>> _invoke;

        private Tool(string name, string description, JObject parameters, Func<JObject, CancellationToken, UniTask<string>> invoke)
        {
            Name = name;
            Description = description;
            Parameters = parameters ?? new ToolSchema();
            _invoke = invoke;
        }

        public string Name { get; }

        public string Description { get; }

        public JObject Parameters { get; }

        public UniTask<string> InvokeAsync(JObject arguments, CancellationToken cancellationToken) => _invoke(arguments, cancellationToken);

        public static Tool Create(string name, string description, JObject parameters, Func<JObject, CancellationToken, UniTask<string>> invoke) =>
            new Tool(name, description, parameters, invoke);

        public static Tool Create(string name, string description, JObject parameters, Func<JObject, string> invoke) =>
            new Tool(name, description, parameters, (arguments, _) => UniTask.FromResult(invoke(arguments)));
    }

    /// <summary>Fluent JSON Schema builder for tool parameters.</summary>
    /// <example><code>new ToolSchema().String("city", "City name").Enum("unit", "Units", "celsius", "fahrenheit")</code></example>
    public sealed class ToolSchema
    {
        private readonly JObject _properties = new JObject();
        private readonly JArray _required = new JArray();

        public ToolSchema String(string name, string description, bool required = true) => Add(name, "string", description, required);

        public ToolSchema Number(string name, string description, bool required = true) => Add(name, "number", description, required);

        public ToolSchema Integer(string name, string description, bool required = true) => Add(name, "integer", description, required);

        public ToolSchema Boolean(string name, string description, bool required = true) => Add(name, "boolean", description, required);

        public ToolSchema Enum(string name, string description, params string[] values) => Enum(name, description, true, values);

        public ToolSchema Enum(string name, string description, bool required, params string[] values)
        {
            Add(name, "string", description, required);
            _properties[name]["enum"] = new JArray(values);
            return this;
        }

        /// <param name="itemType">JSON Schema type of each element: string, number, integer or boolean.</param>
        public ToolSchema Array(string name, string description, string itemType = "string", bool required = true)
        {
            Add(name, "array", description, required);
            _properties[name]["items"] = new JObject { ["type"] = itemType };
            return this;
        }

        public ToolSchema Object(string name, string description, ToolSchema schema, bool required = true)
        {
            var property = schema.Build();
            property["description"] = description;
            _properties[name] = property;

            if (required)
            {
                _required.Add(name);
            }

            return this;
        }

        public JObject Build()
        {
            var schema = new JObject { ["type"] = "object", ["properties"] = _properties.DeepClone() };

            if (_required.Count > 0)
            {
                schema["required"] = _required.DeepClone();
            }

            return schema;
        }

        public static implicit operator JObject(ToolSchema schema) => schema.Build();

        private ToolSchema Add(string name, string type, string description, bool required)
        {
            _properties[name] = new JObject { ["type"] = type, ["description"] = description };

            if (required)
            {
                _required.Add(name);
            }

            return this;
        }
    }
}

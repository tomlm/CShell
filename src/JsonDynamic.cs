using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CShellNet
{
    /// <summary>
    /// JSON you can walk with a dot: `json.owner.login`.
    /// </summary>
    /// <remarks>
    /// This is what AsJson() hands back, as `dynamic`, so a script can read the output of a tool
    /// without declaring a type for it:
    ///
    ///     var json = await Cmd("gh api repos/tomlm/CShell").AsJson();
    ///     Console.WriteLine(json.owner.login);
    ///     Console.WriteLine(json["stargazers_count"]);
    ///
    /// System.Text.Json has no equivalent of its own, mostly because dynamic dispatch cannot be
    /// trimmed or compiled ahead of time -- a real constraint for a shipped application and no
    /// constraint at all for a utility script, which is the only thing running this.
    ///
    /// It is a wrapper rather than a subclass because JsonObject is sealed and JsonNode's only
    /// constructor is internal, so neither can be derived from. Conversions cover the difference:
    /// assign it to a JsonNode, JsonObject or JsonArray and you get the node underneath, which
    /// keeps every typed System.Text.Json API available.
    ///
    /// A member that is not there reads as null rather than throwing, which is what makes
    /// `if (json.optional != null)` the way to test for a field. The cost is that a typo reads as
    /// null too, and only announces itself one hop later.
    /// </remarks>
    public class JsonDynamic : DynamicObject, IEnumerable<object>
    {
        private readonly JsonNode node;

        /// <summary>Wrap a parsed JSON node.</summary>
        /// <param name="node">the node to wrap, which may be null</param>
        public JsonDynamic(JsonNode node)
        {
            this.node = node;
        }

        /// <summary>The node underneath, for anything that wants the typed API.</summary>
        public JsonNode Node
        {
            get { return this.node; }
        }

        internal static object Wrap(JsonNode node)
        {
            return node == null ? null : new JsonDynamic(node);
        }

        /// <summary>json.owner -- a property that is not there reads as null.</summary>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = Property(binder.Name);
            return true;
        }

        /// <summary>json["owner"] for an object, json[0] for an array.</summary>
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            var index = indexes != null && indexes.Length > 0 ? indexes[0] : null;

            if (index is string name)
            {
                result = Property(name);
                return true;
            }

            if (index is int position && this.node is JsonArray array)
            {
                result = position >= 0 && position < array.Count ? Wrap(array[position]) : null;
                return true;
            }

            result = null;
            return true;
        }

        /// <summary>Assigning to a typed variable: string, int, JsonNode, a record, anything.</summary>
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            // Asking for a JsonNode, JsonObject or JsonArray gets the node itself rather than a
            // copy, so the typed API keeps working on the same instance.
            if (this.node != null && binder.Type.IsInstanceOfType(this.node))
            {
                result = this.node;
                return true;
            }

            if (binder.Type == typeof(string))
            {
                result = this.node == null ? null : this.node.ToString();
                return true;
            }

            result = this.node == null ? null : JsonSerializer.Deserialize(this.node, binder.Type, Json.Options);
            return true;
        }

        /// <summary>
        /// json.age == 42, json.name == "Joe", json.age + 1.
        /// </summary>
        /// <remarks>
        /// Without this every comparison throws RuntimeBinderException, because DynamicObject
        /// binds members and conversions but not operators. The value is read as whatever the
        /// other side is, then the operator is applied to that.
        /// </remarks>
        public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object result)
        {
            dynamic left = arg == null || this.node == null
                ? (object)null
                : JsonSerializer.Deserialize(this.node, arg.GetType(), Json.Options);
            dynamic right = arg;

            switch (binder.Operation)
            {
                case ExpressionType.Equal: result = left == right; return true;
                case ExpressionType.NotEqual: result = left != right; return true;
                case ExpressionType.LessThan: result = left < right; return true;
                case ExpressionType.LessThanOrEqual: result = left <= right; return true;
                case ExpressionType.GreaterThan: result = left > right; return true;
                case ExpressionType.GreaterThanOrEqual: result = left >= right; return true;
                case ExpressionType.Add: result = left + right; return true;
                case ExpressionType.Subtract: result = left - right; return true;
                case ExpressionType.Multiply: result = left * right; return true;
                case ExpressionType.Divide: result = left / right; return true;
                default: result = null; return false;
            }
        }

        /// <summary>The property names, so a debugger and `foreach` over an object can see them.</summary>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return this.node is JsonObject o ? o.Select(p => p.Key) : Enumerable.Empty<string>();
        }

        /// <summary>foreach over an array. Declared as object because IEnumerable&lt;dynamic&gt; is not a legal interface to implement.</summary>
        public IEnumerator<object> GetEnumerator()
        {
            var array = this.node as JsonArray;
            return array == null
                ? Enumerable.Empty<object>().GetEnumerator()
                : array.Select(Wrap).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>The value as text: a string without its quotes, anything else as JSON.</summary>
        public override string ToString()
        {
            return this.node == null ? String.Empty : this.node.ToString();
        }

        /// <summary>The node underneath, for typed System.Text.Json code.</summary>
        public static implicit operator JsonNode(JsonDynamic json)
        {
            return json == null ? null : json.node;
        }

        /// <summary>The node underneath as an object, or null if it is not one.</summary>
        public static implicit operator JsonObject(JsonDynamic json)
        {
            return json == null ? null : json.node as JsonObject;
        }

        /// <summary>The node underneath as an array, or null if it is not one.</summary>
        public static implicit operator JsonArray(JsonDynamic json)
        {
            return json == null ? null : json.node as JsonArray;
        }

        object Property(string name)
        {
            JsonNode value;
            return this.node is JsonObject o && o.TryGetPropertyValue(name, out value) ? Wrap(value) : null;
        }
    }
}

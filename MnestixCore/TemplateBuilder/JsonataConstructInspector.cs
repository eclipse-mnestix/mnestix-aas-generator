using System.Reflection;
using Jsonata.Net.Native;
using Jsonata.Net.Native.Impl;

namespace MnestixCore.TemplateBuilder;

/// <summary>
/// Detects JSONata constructs that are forbidden in stored expressions because they cannot
/// be governed by the evaluation timeout: function definitions (a recursive lambda
/// stack-overflows the process — StackOverflowException is uncatchable) and the
/// metaprogramming functions $eval/$assert/$error.
/// </summary>
/// <remarks>
/// This type is deliberately isolated from <see cref="BlueprintValidator"/>: it reaches into
/// the JSONata library's AST via reflection, so its coupling to library internals is contained
/// here and covered by its own tests. When the library changes, only this class and its tests move.
/// </remarks>
public sealed class JsonataConstructInspector : IJsonataConstructInspector
{
    // Variable references that let an expression escape any syntax-level filter
    // or abort evaluation with a message at runtime.
    private static readonly HashSet<string> ForbiddenVariables = new()
    {
        "eval", "assert", "error"
    };

    /// <inheritdoc />
    public string? FindForbiddenConstruct(JsonataQuery query)
    {
        // Iterative walk: a recursive walker would itself be a stack-overflow vector.
        var pending = new Stack<Node>();
        if (query.GetAst() is { } root)
        {
            pending.Push(root);
        }

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is LambdaNode)
            {
                return "a function definition";
            }

            if (node is VariableNode { value: { } variableName } && ForbiddenVariables.Contains(variableName))
            {
                return $"${variableName}";
            }

            foreach (var child in GetChildNodes(node))
            {
                pending.Push(child);
            }
        }

        return null;
    }

    /// <summary>
    /// Enumerates the child nodes of a Jsonata AST node. The library exposes children
    /// inconsistently (public fields on the concrete types, public properties on the base
    /// type), so they are collected via reflection over both.
    /// </summary>
    private static IEnumerable<Node> GetChildNodes(Node node)
    {
        var type = node.GetType();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (field.GetValue(node))
            {
                case Node child:
                    yield return child;
                    break;
                case IEnumerable<Node> children:
                    foreach (var child in children)
                    {
                        yield return child;
                    }
                    break;
            }
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (prop.GetValue(node))
            {
                case Node child:
                    yield return child;
                    break;
                case IEnumerable<Node> children:
                    foreach (var child in children)
                    {
                        yield return child;
                    }
                    break;
            }
        }
    }
}

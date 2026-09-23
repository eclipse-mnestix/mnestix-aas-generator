using FluentAssertions;
using Jsonata.Net.Native;
using MnestixCore.TemplateBuilder;

namespace Core.Tests.TemplateBuilder;

[TestFixture]
public class JsonataConstructInspectorTests
{
    private readonly JsonataConstructInspector _sut = new();

    [Test]
    public void FindForbiddenConstruct_LambdaDefinition_ReportsFunctionDefinition()
    {
        var query = new JsonataQuery("$map($.items, function($v) { $v })");

        _sut.FindForbiddenConstruct(query).Should().Be("a function definition");
    }

    [TestCase("$eval('$.name')", "$eval")]
    [TestCase("$assert($.active, 'must be active')", "$assert")]
    [TestCase("$error('boom')", "$error")]
    public void FindForbiddenConstruct_MetaprogrammingFunction_ReportsThatFunction(string expression, string expected)
    {
        var query = new JsonataQuery(expression);

        _sut.FindForbiddenConstruct(query).Should().Be(expected);
    }

    [Test]
    public void FindForbiddenConstruct_BuiltInHigherOrderFunction_ReturnsNull()
    {
        // $map/$join are built-ins, not user function definitions — they stay allowed.
        var query = new JsonataQuery("$join($map($.items, $.name))");

        _sut.FindForbiddenConstruct(query).Should().BeNull();
    }

    [Test]
    public void FindForbiddenConstruct_PlainPathExpression_ReturnsNull()
    {
        var query = new JsonataQuery("$.serialNumber");

        _sut.FindForbiddenConstruct(query).Should().BeNull();
    }

    [Test]
    public void FindForbiddenConstruct_ForbiddenFunctionNestedDeepInExpression_IsStillFound()
    {
        var query = new JsonataQuery("$uppercase($substring($eval('$.code'), 0, 3))");

        _sut.FindForbiddenConstruct(query).Should().Be("$eval");
    }
}

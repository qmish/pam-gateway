using FluentAssertions;
using PamGateway.Api;

namespace PamGateway.Tests.Unit;

public sealed class NestedParenthesesTests
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["env"] = "prod",
        ["role"] = "db",
        ["tier"] = "1",
        ["region"] = "eu"
    };

    [Fact]
    public void DoubleNestedParentheses()
    {
        LabelExpressionEvaluator.Evaluate(
            "((env=prod && role=db) || (env=dev && role=web)) && tier=1", Labels)
            .Should().BeTrue();
    }

    [Fact]
    public void TripleNestedParentheses()
    {
        LabelExpressionEvaluator.Evaluate(
            "(((env=prod))) && role=db", Labels)
            .Should().BeTrue();
    }

    [Fact]
    public void NestedNotWithParentheses()
    {
        LabelExpressionEvaluator.Evaluate(
            "!(env=dev || (role=web && tier=2))", Labels)
            .Should().BeTrue();

        LabelExpressionEvaluator.Evaluate(
            "!(env=prod || (role=web && tier=2))", Labels)
            .Should().BeFalse();
    }

    [Fact]
    public void ComplexNestedExpression()
    {
        LabelExpressionEvaluator.Evaluate(
            "(env=prod && (role=db || role=app)) && (tier=1 || (region=us && tier=2))", Labels)
            .Should().BeTrue();
    }

    [Fact]
    public void MismatchedParentheses_ReturnsFalse()
    {
        LabelExpressionEvaluator.Evaluate("((env=prod)", Labels)
            .Should().BeFalse();
    }

    [Fact]
    public void EmptyParentheses_ReturnsFalse()
    {
        LabelExpressionEvaluator.Evaluate("()", Labels)
            .Should().BeFalse();
    }

    [Fact]
    public void DeeplyNestedOr()
    {
        LabelExpressionEvaluator.Evaluate(
            "((env=dev || env=staging) || (env=prod || env=test))", Labels)
            .Should().BeTrue();
    }
}

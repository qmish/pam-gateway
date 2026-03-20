using FluentAssertions;
using PamGateway.Api;

namespace PamGateway.Tests.Unit;

public sealed class LabelExpressionEvaluatorTests
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["env"] = "prod",
        ["role"] = "db",
        ["criticality"] = "critical",
        ["team"] = "platform"
    };

    [Theory]
    [InlineData("env=prod", true)]
    [InlineData("env=dev", false)]
    [InlineData("env=Prod", true)]
    [InlineData("role=db", true)]
    [InlineData("role=web", false)]
    public void Evaluate_SimpleEquality(string expression, bool expected)
    {
        LabelExpressionEvaluator.Evaluate(expression, Labels).Should().Be(expected);
    }

    [Theory]
    [InlineData("env!=prod", false)]
    [InlineData("env!=dev", true)]
    [InlineData("role!=web", true)]
    [InlineData("role!=db", false)]
    public void Evaluate_NotEqual(string expression, bool expected)
    {
        LabelExpressionEvaluator.Evaluate(expression, Labels).Should().Be(expected);
    }

    [Theory]
    [InlineData("env=prod && role=db", true)]
    [InlineData("env=prod && role=web", false)]
    [InlineData("env=dev && role=db", false)]
    [InlineData("env=dev && role=web", false)]
    public void Evaluate_And(string expression, bool expected)
    {
        LabelExpressionEvaluator.Evaluate(expression, Labels).Should().Be(expected);
    }

    [Theory]
    [InlineData("env=prod || role=web", true)]
    [InlineData("env=dev || role=db", true)]
    [InlineData("env=dev || role=web", false)]
    public void Evaluate_Or(string expression, bool expected)
    {
        LabelExpressionEvaluator.Evaluate(expression, Labels).Should().Be(expected);
    }

    [Theory]
    [InlineData("!env=dev", true)]
    [InlineData("!env=prod", false)]
    [InlineData("!role=web", true)]
    public void Evaluate_Not(string expression, bool expected)
    {
        LabelExpressionEvaluator.Evaluate(expression, Labels).Should().Be(expected);
    }

    [Fact]
    public void Evaluate_ExistsExpression()
    {
        LabelExpressionEvaluator.Evaluate("env", Labels).Should().BeTrue();
        LabelExpressionEvaluator.Evaluate("missing_label", Labels).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ComplexExpression()
    {
        LabelExpressionEvaluator.Evaluate(
            "env=prod && (role=db || role=web) && criticality=critical", Labels)
            .Should().BeTrue();

        LabelExpressionEvaluator.Evaluate(
            "env=prod && (role=cache || role=web) && criticality=critical", Labels)
            .Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NestedParentheses()
    {
        LabelExpressionEvaluator.Evaluate(
            "(env=prod && role=db) || (env=dev && role=web)", Labels)
            .Should().BeTrue();

        LabelExpressionEvaluator.Evaluate(
            "(env=dev && role=db) || (env=prod && role=web)", Labels)
            .Should().BeFalse();
    }

    [Fact]
    public void Evaluate_QuotedValues()
    {
        LabelExpressionEvaluator.Evaluate("env=\"prod\"", Labels).Should().BeTrue();
        LabelExpressionEvaluator.Evaluate("env=\"dev\"", Labels).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_EmptyOrNullExpression_ReturnsFalse(string? expression)
    {
        LabelExpressionEvaluator.Evaluate(expression!, Labels).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NullLabels_ReturnsFalse()
    {
        LabelExpressionEvaluator.Evaluate("env=prod", null).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_EmptyLabels_ReturnsFalse()
    {
        LabelExpressionEvaluator.Evaluate("env=prod", new Dictionary<string, string>())
            .Should().BeFalse();
    }

    [Fact]
    public void Evaluate_InvalidExpression_ReturnsFalse()
    {
        LabelExpressionEvaluator.Evaluate("&&&&", Labels).Should().BeFalse();
        LabelExpressionEvaluator.Evaluate("(((", Labels).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_CombinedAndOrNot()
    {
        LabelExpressionEvaluator.Evaluate(
            "env=prod && !role=web || team=platform", Labels)
            .Should().BeTrue();
    }
}

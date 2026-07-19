using System.Globalization;
using NL.Core.Ast;

namespace NL.Core;

/// <summary>
/// Evaluates a parsed NLEvent <see cref="ConfigAst"/> against incoming <see cref="GameEvent"/>s.
/// This is the "enforcement" half of the NLEvents idea from nl.txt section 3 — the parser only
/// builds the AST, this class is what actually turns it into allow/block decisions.
/// </summary>
public sealed class RuleEngine
{
    private readonly Dictionary<string, EventBlock> _events;
    private readonly List<string> _loadWarnings = new();

    public RuleEngine(ConfigAst config)
    {
        _events = config.Events.ToDictionary(e => e.Name, e => e);

        foreach (var block in config.Events)
        {
            if (!DefinitelyTerminates(block.Body))
            {
                _loadWarnings.Add(
                    $"event '{block.Name}' has a code path with no explicit action; " +
                    "it will default to allow if that path is taken");
            }
        }
    }

    public static RuleEngine FromSource(string source) => new(Parser.Parse(source));

    /// <summary>Diagnostics produced while loading the config (not fatal, but worth surfacing
    /// to the streamer authoring it — see docs/NLEVENT_LANGUAGE_SPEC_v0.1.md).</summary>
    public IReadOnlyList<string> LoadWarnings => _loadWarnings;

    public ActionResult Evaluate(GameEvent gameEvent)
    {
        if (!_events.TryGetValue(gameEvent.Name, out var block))
        {
            // No rule authored for this event: nothing to enforce, so allow by default.
            return ActionResult.Allow();
        }

        string? pendingWarning = null;
        var result = TryEvaluateStatements(block.Body, gameEvent, ref pendingWarning);
        return result ?? ActionResult.Allow(pendingWarning);
    }

    private static ActionResult? TryEvaluateStatements(
        List<Statement> statements, GameEvent evt, ref string? pendingWarning)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case WarnStatement warn:
                    pendingWarning = warn.Message;
                    break;

                case ActionStatement action:
                    var decision = action.Kind == ActionKind.Allow ? Decision.Allow : Decision.Block;
                    return new ActionResult(decision, pendingWarning);

                case IfStatement ifStatement:
                    var branch = EvaluateConditionExpr(ifStatement.Condition, evt) ? ifStatement.Then : ifStatement.Else;
                    if (branch is not null)
                    {
                        var branchResult = TryEvaluateStatements(branch, evt, ref pendingWarning);
                        if (branchResult is not null)
                        {
                            return branchResult;
                        }
                    }

                    break;

                default:
                    throw new InvalidOperationException($"unhandled statement type {statement.GetType()}");
            }
        }

        return null;
    }

    /// <summary>
    /// Conservative static check used only to populate <see cref="LoadWarnings"/>: true if every
    /// path through <paramref name="statements"/> ends in an explicit action statement.
    /// </summary>
    private static bool DefinitelyTerminates(List<Statement> statements)
    {
        if (statements.Count == 0)
        {
            return false;
        }

        return statements[^1] switch
        {
            ActionStatement => true,
            IfStatement ifStatement => ifStatement.Else is not null
                && DefinitelyTerminates(ifStatement.Then)
                && DefinitelyTerminates(ifStatement.Else),
            _ => false,
        };
    }

    private static bool EvaluateConditionExpr(ConditionExpr expr, GameEvent evt) => expr switch
    {
        Condition simple => EvaluateSimpleCondition(simple, evt),
        CompoundCondition compound => compound.Op == "and"
            ? EvaluateConditionExpr(compound.Left, evt) && EvaluateConditionExpr(compound.Right, evt)
            : EvaluateConditionExpr(compound.Left, evt) || EvaluateConditionExpr(compound.Right, evt),
        _ => throw new InvalidOperationException($"unhandled condition type {expr.GetType()}"),
    };

    private static bool EvaluateSimpleCondition(Condition condition, GameEvent evt)
    {
        var left = ResolveOperand(condition.Left, evt);
        var right = ResolveOperand(condition.Right, evt);
        return Compare(left, right, condition.Comparator);
    }

    private static OperandValue ResolveOperand(Operand operand, GameEvent evt)
    {
        switch (operand.Kind)
        {
            case OperandKind.Number:
                return OperandValue.Num(double.Parse(operand.Text, CultureInfo.InvariantCulture));
            case OperandKind.String:
                return OperandValue.Str(operand.Text);
            case OperandKind.Identifier:
                if (evt.Properties.TryGetValue(operand.Text, out var value))
                {
                    return OperandValue.Num(value);
                }

                throw new InvalidOperationException(
                    $"event '{evt.Name}' has no property '{operand.Text}' to compare against");
            default:
                throw new InvalidOperationException($"unhandled operand kind {operand.Kind}");
        }
    }

    private static bool Compare(OperandValue left, OperandValue right, string op)
    {
        if (left.Number is { } l && right.Number is { } r)
        {
            return op switch
            {
                ">" => l > r,
                "<" => l < r,
                ">=" => l >= r,
                "<=" => l <= r,
                "==" => l == r,
                "!=" => l != r,
                _ => throw new InvalidOperationException($"unknown comparator '{op}'"),
            };
        }

        var leftText = left.Text ?? left.Number?.ToString(CultureInfo.InvariantCulture) ?? "";
        var rightText = right.Text ?? right.Number?.ToString(CultureInfo.InvariantCulture) ?? "";

        return op switch
        {
            "==" => leftText == rightText,
            "!=" => leftText != rightText,
            _ => throw new InvalidOperationException($"comparator '{op}' requires two numeric operands"),
        };
    }

    private readonly record struct OperandValue(double? Number, string? Text)
    {
        public static OperandValue Num(double n) => new(n, null);

        public static OperandValue Str(string s) => new(null, s);
    }
}

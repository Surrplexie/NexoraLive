using NL.ConfigEditor.Model;
using NL.Core;
using NL.Core.Ast;

namespace NL.ConfigEditor;

/// <summary>
/// Reads an existing <c>.nle</c> file via <c>NL.Core.Parser</c> and converts the resulting
/// <see cref="ConfigAst"/> into an editor <see cref="ConfigModel"/> that can be roundtripped
/// back to <c>.nle</c> text through <see cref="NleWriter"/>.
/// </summary>
public static class NleLoader
{
    /// <summary>Parses <paramref name="source"/> text and converts to a <see cref="ConfigModel"/>.</summary>
    public static ConfigModel Load(string source)
    {
        var ast = Parser.Parse(source);
        var model = new ConfigModel();

        foreach (var h in ast.HotkeyDeclarations)
        {
            model.Hotkeys.Add(new HotkeyEntry { Combo = h.ComboText, Action = h.Action });
        }

        foreach (var evt in ast.Events)
        {
            var entry = new EventEntry { Name = evt.Name };
            entry.Statements.AddRange(evt.Body.Select(ConvertStatement));
            model.Events.Add(entry);
        }

        return model;
    }

    private static StatementEntry ConvertStatement(Statement s) => s switch
    {
        ActionStatement a => new StatementEntry
        {
            Type = a.Kind switch
            {
                ActionKind.Allow => StatementType.Allow,
                ActionKind.Deny  => StatementType.Deny,
                _                => StatementType.Block,
            },
        },

        WarnStatement w => new StatementEntry
        {
            Type = StatementType.Warn,
            WarnMessage = w.Message,
        },

        IfStatement ifStmt => new StatementEntry
        {
            Type = StatementType.If,
            Condition = ConvertConditionExpr(ifStmt.Condition),
            ThenBody = ifStmt.Then.Select(ConvertStatement).ToList(),
            ElseBody = ifStmt.Else?.Select(ConvertStatement).ToList(),
        },

        _ => new StatementEntry { Type = StatementType.Allow },
    };

    private static ConditionEntry ConvertConditionExpr(ConditionExpr expr)
    {
        var entry = new ConditionEntry { Parts = [], Joins = [] };
        FlattenInto(expr, entry);
        return entry;
    }

    private static void FlattenInto(ConditionExpr expr, ConditionEntry into)
    {
        switch (expr)
        {
            case Condition simple:
                into.Parts.Add(new SimpleConditionEntry
                {
                    Left  = simple.Left.Text,
                    Op    = simple.Comparator,
                    Right = simple.Right.Text,
                });
                break;

            case CompoundCondition compound:
                FlattenInto(compound.Left, into);
                into.Joins.Add(compound.Op);   // "and" | "or"
                FlattenInto(compound.Right, into);
                break;
        }
    }
}

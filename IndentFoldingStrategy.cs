// Source - https://stackoverflow.com/a/47588975
// Posted by Momo, modified by community. See post 'Timeline' for change history
// Retrieved 2026-05-14, License - CC BY-SA 3.0
// I am too dumb to write my own strategy ;)

using System.Collections.Generic;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace Rizor;

/// <summary>
/// Allows producing tab based foldings
/// </summary>
public class IndentFoldingStrategy
{
    internal class TabIndent(int iIndentSize, int iLineStart, int iLineEnd)
    {
        public int IndentSize = iIndentSize;
        public int LineStart = iLineStart;
        public int LineEnd = iLineEnd;
        public int StartOffset => LineStart + IndentSize - 1;
        public int TextLength => LineEnd - StartOffset;
    }

    /// <summary>
    /// Creates a new TabFoldingStrategy.
    /// </summary>
    public IndentFoldingStrategy()
    {
    }

    /// <summary>
    /// Create <see cref="NewFolding"/>s for the specified document.
    /// </summary>
    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        return CreateNewFoldings(document);
    }

    /// <summary>
    /// Create <see cref="NewFolding"/>s for the specified document.
    /// </summary>
    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document)
    {
        List<NewFolding> newFoldings = new List<NewFolding>();

        int documentIndent = 0;
        List<TabIndent> tabIndents = new List<TabIndent>();
        foreach (DocumentLine line in document.Lines)
        {
            int lineIndent = 0;
            for (int i = line.Offset; i < line.EndOffset; i++)
            {
                char c = document.GetCharAt(i);
                if (c == '\t' || c == ' ')
                {
                    lineIndent++;
                }
                else
                {
                    break;
                }
            }

            if (lineIndent > documentIndent)
            {
                tabIndents.Add(new TabIndent(lineIndent, line.PreviousLine.Offset, line.PreviousLine.EndOffset));
            }
            else if (lineIndent < documentIndent)
            {
                List<TabIndent> closedIndents = tabIndents.FindAll(x => x.IndentSize > lineIndent);
                closedIndents.ForEach(x =>
                {
                    newFoldings.Add(new NewFolding(x.StartOffset, line.PreviousLine.EndOffset)
                    {
                        Name = document.GetText(x.StartOffset, x.TextLength)
                    });
                    tabIndents.Remove(x);
                });
            }

            documentIndent = lineIndent;
        }

        tabIndents.ForEach(x => { newFoldings.Add(new NewFolding(x.StartOffset, document.TextLength)); });

        newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return newFoldings;
    }

    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        int firstErrorOffset;
        IEnumerable<NewFolding> newFoldings = CreateNewFoldings(document, out firstErrorOffset);
        manager.UpdateFoldings(newFoldings, firstErrorOffset);
    }
}
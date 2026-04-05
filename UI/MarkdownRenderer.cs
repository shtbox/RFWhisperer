using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SDRSharp.RFWhisperer.UI
{
    /// <summary>
    /// Parses a subset of Markdown and writes it into a RichTextBox as styled runs.
    /// Handles: headings, bold, italic, inline code, code blocks, bullets, numbered
    /// lists, horizontal rules, and plain paragraphs.
    /// No external dependencies — pure WinForms.
    /// </summary>
    public static class MarkdownRenderer
    {
        // Fonts
        private static readonly Font FontBody     = new("Segoe UI",     10f);
        private static readonly Font FontBold     = new("Segoe UI",     10f, FontStyle.Bold);
        private static readonly Font FontItalic   = new("Segoe UI",     10f, FontStyle.Italic);
        private static readonly Font FontBoldItalic = new("Segoe UI",   10f, FontStyle.Bold | FontStyle.Italic);
        private static readonly Font FontH1       = new("Segoe UI",     14f, FontStyle.Bold);
        private static readonly Font FontH2       = new("Segoe UI",     12f, FontStyle.Bold);
        private static readonly Font FontH3       = new("Segoe UI",     10.5f, FontStyle.Bold);
        private static readonly Font FontCode     = new("Consolas",      9f);
        private static readonly Font FontCodeBlock = new("Consolas",     9f);

        // Colours
        private static readonly Color ColCode     = Color.FromArgb(200, 200, 140);
        private static readonly Color ColH1       = Color.FromArgb(255, 255, 255);
        private static readonly Color ColH2       = Color.FromArgb(200, 220, 255);
        private static readonly Color ColH3       = Color.FromArgb(170, 200, 240);
        private static readonly Color ColHRule    = Color.FromArgb(80, 80, 88);
        private static readonly Color ColCodeBg   = Color.FromArgb(38, 38, 42);
        private static readonly Color ColBullet   = Color.FromArgb(0, 122, 204);

        /// <summary>
        /// Append a markdown string to the RichTextBox with full formatting.
        /// Call from the UI thread (or use Invoke).
        /// </summary>
        public static void Append(RichTextBox rtb, string markdown, Color defaultForeColor, Color defaultBackColor)
        {
            if (string.IsNullOrEmpty(markdown)) return;

            // Normalise line endings
            string text = markdown.Replace("\r\n", "\n").Replace("\r", "\n");

            // Split into blocks separated by blank lines
            var lines = text.Split('\n');

            bool inCodeBlock = false;
            var codeLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // ── Code fence ───────────────────────────────────────────────────
                if (line.TrimStart().StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        inCodeBlock = true;
                        codeLines.Clear();
                    }
                    else
                    {
                        // Flush code block
                        AppendCodeBlock(rtb, string.Join("\n", codeLines), defaultBackColor);
                        codeLines.Clear();
                        inCodeBlock = false;
                    }
                    continue;
                }

                if (inCodeBlock) { codeLines.Add(line); continue; }

                // ── Headings ─────────────────────────────────────────────────────
                if (line.StartsWith("### "))
                {
                    AppendNewlineIfNeeded(rtb);
                    AppendRun(rtb, line[4..].TrimEnd(), FontH3, ColH3, defaultBackColor);
                    AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    continue;
                }
                if (line.StartsWith("## "))
                {
                    AppendNewlineIfNeeded(rtb);
                    AppendRun(rtb, line[3..].TrimEnd(), FontH2, ColH2, defaultBackColor);
                    AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    continue;
                }
                if (line.StartsWith("# "))
                {
                    AppendNewlineIfNeeded(rtb);
                    AppendRun(rtb, line[2..].TrimEnd(), FontH1, ColH1, defaultBackColor);
                    AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    continue;
                }

                // ── Horizontal rule ───────────────────────────────────────────────
                if (Regex.IsMatch(line, @"^[-*_]{3,}\s*$"))
                {
                    AppendNewlineIfNeeded(rtb);
                    AppendRun(rtb, new string('─', 42), FontBody, ColHRule, defaultBackColor);
                    AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    continue;
                }

                // ── Bullet list ───────────────────────────────────────────────────
                var bulletMatch = Regex.Match(line, @"^(\s*)[•\-\*] (.+)$");
                if (bulletMatch.Success)
                {
                    int indent = bulletMatch.Groups[1].Value.Length;
                    string bullet = indent > 0 ? "  ◦  " : "  •  ";
                    AppendRun(rtb, bullet, FontBody, ColBullet, defaultBackColor);
                    AppendInline(rtb, bulletMatch.Groups[2].Value, defaultForeColor, defaultBackColor);
                    AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    continue;
                }

                // ── Numbered list ─────────────────────────────────────────────────
                var numMatch = Regex.Match(line, @"^(\s*)\d+\. (.+)$");
                if (numMatch.Success)
                {
                    // Preserve the number
                    var numPart = Regex.Match(line, @"^(\s*)(\d+\.) (.+)$");
                    AppendRun(rtb, "  " + numPart.Groups[2].Value + " ", FontBold, ColBullet, defaultBackColor);
                    AppendInline(rtb, numPart.Groups[3].Value, defaultForeColor, defaultBackColor);
                    AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    continue;
                }

                // ── Blank line → paragraph gap ────────────────────────────────────
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Only add spacing if there's already content (avoid leading whitespace)
                    if (rtb.TextLength > 0)
                        AppendNewline(rtb, defaultForeColor, defaultBackColor);
                    continue;
                }

                // ── Normal paragraph line ─────────────────────────────────────────
                AppendInline(rtb, line, defaultForeColor, defaultBackColor);
                AppendNewline(rtb, defaultForeColor, defaultBackColor);
            }

            // Flush unclosed code block
            if (inCodeBlock && codeLines.Count > 0)
                AppendCodeBlock(rtb, string.Join("\n", codeLines), defaultBackColor);

            rtb.ScrollToCaret();
        }

        // ── Inline parser (bold, italic, code within a line) ─────────────────────

        private static void AppendInline(RichTextBox rtb, string line, Color fg, Color bg)
        {
            // Pattern: **bold**, *italic*, ***bold+italic***, `code`
            var pattern = new Regex(@"(\*\*\*.+?\*\*\*|\*\*.+?\*\*|\*.+?\*|`.+?`)", RegexOptions.Singleline);
            int pos = 0;

            foreach (Match m in pattern.Matches(line))
            {
                // Plain text before this match
                if (m.Index > pos)
                    AppendRun(rtb, line[pos..m.Index], FontBody, fg, bg);

                string inner = m.Value;

                if (inner.StartsWith("***") && inner.EndsWith("***"))
                    AppendRun(rtb, inner[3..^3], FontBoldItalic, fg, bg);
                else if (inner.StartsWith("**") && inner.EndsWith("**"))
                    AppendRun(rtb, inner[2..^2], FontBold, fg, bg);
                else if (inner.StartsWith("*") && inner.EndsWith("*"))
                    AppendRun(rtb, inner[1..^1], FontItalic, fg, bg);
                else if (inner.StartsWith("`") && inner.EndsWith("`"))
                    AppendRun(rtb, inner[1..^1], FontCode, ColCode, bg);

                pos = m.Index + m.Length;
            }

            // Remaining plain text
            if (pos < line.Length)
                AppendRun(rtb, line[pos..], FontBody, fg, bg);
        }

        // ── Primitives ────────────────────────────────────────────────────────────

        private static void AppendRun(RichTextBox rtb, string text, Font font, Color fg, Color bg)
        {
            int start = rtb.TextLength;
            rtb.AppendText(text);
            rtb.Select(start, text.Length);
            rtb.SelectionFont      = font;
            rtb.SelectionColor     = fg;
            rtb.SelectionBackColor = bg;
            rtb.SelectionLength    = 0;
        }

        private static void AppendCodeBlock(RichTextBox rtb, string code, Color parentBg)
        {
            AppendNewlineIfNeeded(rtb);
            // Leading padding line
            AppendRun(rtb, " ", FontCodeBlock, ColCode, ColCodeBg);
            AppendNewline(rtb, ColCode, ColCodeBg);

            foreach (var codeLine in code.Split('\n'))
            {
                // Pad line to ensure background fills visually
                AppendRun(rtb, " " + codeLine, FontCodeBlock, ColCode, ColCodeBg);
                AppendNewline(rtb, ColCode, ColCodeBg);
            }

            // Trailing padding line
            AppendRun(rtb, " ", FontCodeBlock, ColCode, ColCodeBg);
            AppendNewline(rtb, ColCode, parentBg);
            AppendNewline(rtb, ColCode, parentBg);
        }

        private static void AppendNewline(RichTextBox rtb, Color fg, Color bg)
        {
            int start = rtb.TextLength;
            rtb.AppendText("\n");
            rtb.Select(start, 1);
            rtb.SelectionFont      = FontBody;
            rtb.SelectionColor     = fg;
            rtb.SelectionBackColor = bg;
            rtb.SelectionLength    = 0;
        }

        private static void AppendNewlineIfNeeded(RichTextBox rtb)
        {
            if (rtb.TextLength > 0 && !rtb.Text.EndsWith('\n'))
                rtb.AppendText("\n");
        }
    }
}

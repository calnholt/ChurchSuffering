using System;
using System.Collections.Generic;
using System.Text;

namespace ChurchSuffering.ECS.Utils.RichText
{
    public static class RichTextParser
    {
        // Parses [tag k=v k2=v2] ... [/tag]. Supports nesting. Escapes with [[ and ]].
        public static RichTextDocument Parse(string input)
        {
            var doc = new RichTextDocument();
            if (string.IsNullOrEmpty(input)) return doc;

            int i = 0;
            var stack = new Stack<TagNode>();
            var currentList = doc.Children;
            var textBuffer = new StringBuilder();

            void FlushText()
            {
                if (textBuffer.Length == 0) return;
                currentList.Add(new TextRunNode { Text = textBuffer.ToString() });
                textBuffer.Clear();
            }

            while (i < input.Length)
            {
                char c = input[i];

                // Escape [[ or ]] => [ or ]
                if (c == '[' && i + 1 < input.Length && input[i + 1] == '[')
                {
                    textBuffer.Append('[');
                    i += 2;
                    continue;
                }
                if (c == ']' && i + 1 < input.Length && input[i + 1] == ']')
                {
                    textBuffer.Append(']');
                    i += 2;
                    continue;
                }

                if (c == '[')
                {
                    // Try to parse a tag
                    int close = input.IndexOf(']', i + 1);
                    if (close <= i)
                    {
                        // malformed, treat as literal
                        textBuffer.Append(c);
                        i++;
                        continue;
                    }
                    string inside = input.Substring(i + 1, close - (i + 1));
                    bool closing = inside.StartsWith("/");
                    if (closing)
                    {
                        var name = inside.Length > 1 ? inside.Substring(1).Trim().ToLowerInvariant() : string.Empty;
                        i = close + 1;
                        // Flush any pending text before closing
                        FlushText();
                        if (stack.Count == 0)
                        {
                            // stray close -> literal
                            textBuffer.Append('[').Append(inside).Append(']');
                            continue;
                        }
                        var open = stack.Pop();
                        if (!string.Equals(open.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            // mismatched -> treat closing as literal
                            stack.Push(open);
                            textBuffer.Append("[/").Append(name).Append(']');
                            continue;
                        }
                        // Close tag: attach to parent list
                        if (stack.Count > 0)
                        {
                            var parent = stack.Peek();
                            parent.Children.Add(open);
                            currentList = parent.Children;
                        }
                        else
                        {
                            doc.Children.Add(open);
                            currentList = doc.Children;
                        }
                        continue;
                    }
                    else
                    {
                        // Opening tag: [name k=v]
                        var (name, attrs) = ParseTagHeader(inside);
                        if (string.IsNullOrEmpty(name))
                        {
                            // not a valid tag
                            textBuffer.Append('[').Append(inside).Append(']');
                            i = close + 1;
                            continue;
                        }
                        // Flush text before pushing new tag
                        FlushText();
                        var node = new TagNode { Name = name, Attributes = attrs, Children = new List<IRichTextNode>() };
                        stack.Push(node);
                        currentList = node.Children;
                        i = close + 1;
                        continue;
                    }
                }

                // default: text
                textBuffer.Append(c);
                i++;
            }

            // Flush trailing text
            FlushText();

            // Any unclosed tags -> treat as literal by unwinding
            while (stack.Count > 0)
            {
                var open = stack.Pop();
                // Reconstruct literal [name ...]children[/name]
                var sb = new StringBuilder();
                sb.Append('[').Append(open.Name);
                foreach (var kv in open.Attributes)
                {
                    sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
                }
                sb.Append(']');
                foreach (var ch in open.Children)
                {
                    if (ch is TextRunNode tr) sb.Append(tr.Text);
                    else if (ch is TagNode tn)
                    {
                        // nested tags become literal recursively in simplest form
                        sb.Append('[').Append(tn.Name).Append(']');
                    }
                }
                sb.Append("[/").Append(open.Name).Append(']');
                doc.Children.Add(new TextRunNode { Text = sb.ToString() });
            }

            return doc;
        }

        private static (string, Dictionary<string, string>) ParseTagHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header)) return (string.Empty, new Dictionary<string, string>());
            var parts = SplitWhitespace(header.Trim());
            if (parts.Count == 0) return (string.Empty, new Dictionary<string, string>());
            string name = parts[0].ToLowerInvariant();
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < parts.Count; i++)
            {
                var p = parts[i];
                int eq = p.IndexOf('=');
                if (eq <= 0 || eq >= p.Length - 1) { attrs[p] = "true"; continue; }
                string key = p.Substring(0, eq).Trim();
                string val = p.Substring(eq + 1).Trim();
                attrs[key] = val;
            }
            return (name, attrs);
        }

        private static List<string> SplitWhitespace(string s)
        {
            var list = new List<string>();
            var cur = new StringBuilder();
            bool inQuote = false;
            char quoteChar = '\0';
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inQuote)
                {
                    if (c == quoteChar)
                    {
                        inQuote = false;
                    }
                    else
                    {
                        cur.Append(c);
                    }
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    inQuote = true;
                    quoteChar = c;
                    continue;
                }
                if (char.IsWhiteSpace(c))
                {
                    if (cur.Length > 0) { list.Add(cur.ToString()); cur.Clear(); }
                    continue;
                }
                cur.Append(c);
            }
            if (cur.Length > 0) list.Add(cur.ToString());
            return list;
        }
    }
}




using System.Collections.Generic;

namespace ChurchSuffering.ECS.Utils.RichText
{
    public interface IRichTextNode { }

    public sealed class TextRunNode : IRichTextNode
    {
        public string Text { get; set; } = string.Empty;
    }

    public sealed class TagNode : IRichTextNode
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public List<IRichTextNode> Children { get; set; } = new List<IRichTextNode>();
    }

    public sealed class RichTextDocument
    {
        public List<IRichTextNode> Children { get; set; } = new List<IRichTextNode>();
    }
}




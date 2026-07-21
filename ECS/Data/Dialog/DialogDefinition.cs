using System.Collections.Generic;

namespace ChurchSuffering.ECS.Data.Dialog
{
    public class DialogLine
    {
        public string actor { get; set; }
        public string message { get; set; }
    }

    public class DialogDefinition
    {
        // Id is the filename (without extension), e.g. "desert_1"
        public string id { get; set; }
        public List<DialogLine> lines { get; set; } = new List<DialogLine>();
        public Dictionary<string, List<DialogLine>> segments { get; set; } = new Dictionary<string, List<DialogLine>>();

        public IReadOnlyList<DialogLine> ResolveSegment(string segmentId)
        {
            if (!string.IsNullOrWhiteSpace(segmentId))
            {
                if (segments != null && segments.TryGetValue(segmentId, out var segment))
                {
                    return segment;
                }

                return System.Array.Empty<DialogLine>();
            }

            return lines ?? new List<DialogLine>();
        }
    }
}

using System.Text.RegularExpressions;

namespace ChurchSuffering.ECS.Utils
{
	public static class StringUtils
	{
		public static string ToSentenceCase(string input)
		{
			if (string.IsNullOrWhiteSpace(input)) return string.Empty;
			string trimmed = input.Trim().Replace('_', ' ');
			// Insert spaces between camel/pascal case boundaries
			string withSpaces = Regex.Replace(trimmed, @"(?<=[a-z])([A-Z])", " $1");
			withSpaces = Regex.Replace(withSpaces, @"(?<=[A-Z])([A-Z][a-z])", " $1");
			string lower = withSpaces.ToLowerInvariant();
			return char.ToUpper(lower[0]) + lower.Substring(1);
		}

		public static string ToTitleCase(string input)
		{
			if (string.IsNullOrWhiteSpace(input)) return string.Empty;
			string trimmed = input.Trim().Replace('_', ' ');
			// Insert spaces between camel/pascal case boundaries
			string withSpaces = Regex.Replace(trimmed, @"(?<=[a-z])([A-Z])", " $1");
			withSpaces = Regex.Replace(withSpaces, @"(?<=[A-Z])([A-Z][a-z])", " $1");
			string lower = withSpaces.ToLowerInvariant();
			// Capitalize first letter of each word
			return Regex.Replace(lower, @"\b\w", m => m.Value.ToUpper());
		}
	}
}



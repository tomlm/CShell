using System;
using System.Text.Json;

namespace CShellNet
{
    /// <summary>
    /// How CShell reads the JSON that command line tools produce.
    /// </summary>
    /// <remarks>
    /// System.Text.Json is stricter out of the box than the JSON real tools emit, and stricter
    /// than the Newtonsoft reader this replaced. Three of its defaults are relaxed here, each
    /// because a script would otherwise fail on output it had no hand in producing:
    ///
    ///   * property names are matched case insensitively, so `{"name":"..."}` still fills a
    ///     `Name` property. Nearly every CLI emits camelCase or snake_case JSON while the C#
    ///     record modelling it is PascalCase, and the strict default would not error -- it would
    ///     hand back an object with every field left at its default, which is the worst way to
    ///     fail.
    ///   * trailing commas are allowed.
    ///   * comments are skipped rather than rejected.
    ///
    /// A byte order mark is trimmed for the same reason: a UTF-8 BOM is not valid JSON, tools
    /// and files on Windows produce it constantly, and the resulting error names a character
    /// nobody can see.
    /// </remarks>
    internal static class Json
    {
        internal static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        internal static readonly JsonDocumentOptions DocumentOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>Whatever a tool wrote, made safe to hand to the parser.</summary>
        internal static string Clean(string json)
        {
            return json == null ? null : json.TrimStart('﻿', '​').Trim();
        }
    }
}

using System.Text.RegularExpressions;
using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public sealed class CredentialService : ICredentialService
{
    private static readonly Regex SecretPattern = new(
        @"(?i)(api[_-]?key|token|secret|password|authorization)(\s*[:=]\s*)([^\s,;]+)",
        RegexOptions.Compiled);

    public SecretInput Create(string value) => new(value);

    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return SecretPattern.Replace(text, "$1$2[REDACTED]");
    }
}

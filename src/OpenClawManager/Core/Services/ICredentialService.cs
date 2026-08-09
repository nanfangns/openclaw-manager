using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public interface ICredentialService
{
    string Redact(string text);
    SecretInput Create(string value);
}

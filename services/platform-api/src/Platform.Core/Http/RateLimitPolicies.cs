namespace Platform.Core.Http;

/// <summary>
/// Named per-person limits on the routes a candidate or a client could flood (PLAN D92). They count only candidates and
/// clients; staff are bound by their general per-minute limit alone. The numbers live in the API's settings.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Upload links (<c>POST /v1/files/presign-upload</c>): each one opens a PUT into the documents bucket.</summary>
    public const string Uploads = "uploads";

    /// <summary>«Cuenta para tu pago» (<c>POST /v1/candidate-portal/bank-accounts</c>).</summary>
    public const string BankAccounts = "bank-accounts";

    /// <summary>«Mis datos» (<c>PUT /v1/candidate-portal/profile</c>).</summary>
    public const string Profile = "profile";

    /// <summary>A client's comment on a vacante, which notifies the staff.</summary>
    public const string Comments = "comments";
}

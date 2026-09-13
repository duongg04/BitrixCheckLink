namespace BitrixChecker.Configuration;

public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

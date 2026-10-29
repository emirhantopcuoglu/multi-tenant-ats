namespace Ats.Modules.CandidateAccounts.Application;

public interface ICandidatePasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

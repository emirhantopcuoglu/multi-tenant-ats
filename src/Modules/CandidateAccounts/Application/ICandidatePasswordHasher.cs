namespace Ats.Modules.CandidateAccounts.Application;

// Hashes and verifies candidate passwords. A thin port so the auth service depends only on
// "hash / verify" — the algorithm and its work factor stay an Infrastructure detail.
public interface ICandidatePasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Applications;

// Needs real PostgreSQL: SearchVector is a stored generated column and websearch_to_tsquery is a
// server function.
[Collection("Integration")]
public sealed class CandidateSearchTests
{
    private readonly PostgresContainerFixture _fixture;

    public CandidateSearchTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("Nadia")]
    [InlineData("Okonkwo")]
    [InlineData("nadia okonkwo")]
    [InlineData("nadia.okonkwo@acme.test")]
    public async Task should_find_a_candidate_by_any_indexed_field(string term)
    {
        // The generated column covers first name, last name and email, the search box promises
        // exactly those three, so each one is asserted rather than assumed.
        var tenant = new FixedTenant(Guid.NewGuid());
        await SeedAsync(tenant, ("Nadia", "Okonkwo", "nadia.okonkwo@acme.test"));

        var result = await SearchAsync(tenant, term);

        var found = Assert.Single(result.Items);
        Assert.Equal("nadia.okonkwo@acme.test", found.Email);
    }

    [Fact]
    public async Task should_not_return_another_tenants_candidates()
    {
        // The repository has no tenant predicate and relies on the global query filter. Losing the filter
        // would let one company search another's candidates.
        var owner = new FixedTenant(Guid.NewGuid());
        var stranger = new FixedTenant(Guid.NewGuid());
        await SeedAsync(owner, ("Nadia", "Okonkwo", "nadia@acme.test"));

        var result = await SearchAsync(stranger, "Nadia");

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task should_not_return_soft_deleted_candidates()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var candidates = await SeedAsync(tenant, ("Deleted", "Person", "deleted@acme.test"));

        await using (var db = NewDb(tenant))
        {
            var candidate = await db.Candidates.SingleAsync(c => c.Id == candidates[0]);
            db.Candidates.Remove(candidate);
            await db.SaveChangesAsync();
        }

        var result = await SearchAsync(tenant, "Deleted");

        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("&")]
    [InlineData("\"unclosed quote")]
    [InlineData("a & | ! b")]
    [InlineData("-")]
    public async Task should_survive_punctuation_a_user_types(string term)
    {
        // websearch_to_tsquery treats operators as text; to_tsquery would throw on a stray quote. The point
        // is that it doesn't throw.
        var tenant = new FixedTenant(Guid.NewGuid());
        await SeedAsync(tenant, ("Nadia", "Okonkwo", "nadia@acme.test"));

        var result = await SearchAsync(tenant, term);

        Assert.NotNull(result.Items);
    }

    [Fact]
    public async Task should_rank_a_candidate_matching_both_terms_above_one_matching_only_one()
    {
        // An OR query on purpose: websearch_to_tsquery ANDs bare words, so "Zeynep Kaya" would return one
        // row and never exercise the ordering.
        var tenant = new FixedTenant(Guid.NewGuid());
        await SeedAsync(
            tenant,
            // Seeded and named so the alphabetical tiebreak would put Ahmet first: only the rank can
            // produce the expected order.
            ("Ahmet", "Kaya", "ahmet.kaya@acme.test"),
            ("Zeynep", "Kaya", "zeynep.kaya@acme.test"));

        var result = await SearchAsync(tenant, "Zeynep OR Kaya");

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("zeynep.kaya@acme.test", result.Items[0].Email);
    }

    [Fact]
    public async Task should_order_equally_ranked_matches_by_surname_then_first_name()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        await SeedAsync(
            tenant,
            ("Bora", "Yilmaz", "bora@acme.test"),
            ("Ada", "Yilmaz", "ada@acme.test"),
            ("Cem", "Aydin", "cem@acme.test"));

        var result = await SearchAsync(tenant, "Yilmaz OR Aydin");

        // Same rank across all three, so the tiebreak decides: Aydin before Yilmaz, then Ada before Bora.
        Assert.Equal(
            new[] { "cem@acme.test", "ada@acme.test", "bora@acme.test" },
            result.Items.Select(c => c.Email).ToArray());
    }

    [Fact]
    public async Task should_page_without_losing_or_repeating_rows()
    {
        // TotalCount must count every match, not just the page, the UI derives its page count from it,
        // so a windowed count would strand the later pages behind a "1 of 1".
        var tenant = new FixedTenant(Guid.NewGuid());
        await SeedAsync(
            tenant,
            ("Ada", "Sharedname", "a@acme.test"),
            ("Bora", "Sharedname", "b@acme.test"),
            ("Cem", "Sharedname", "c@acme.test"));

        var first = await SearchAsync(tenant, "Sharedname", page: 1, pageSize: 2);
        var second = await SearchAsync(tenant, "Sharedname", page: 2, pageSize: 2);

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Empty(first.Items.Select(c => c.Id).Intersect(second.Items.Select(c => c.Id)));
    }

    private async Task<PagedResult<CandidateSearchResultDto>> SearchAsync(
        FixedTenant tenant, string term, int page = 1, int pageSize = 20)
    {
        await using var db = NewDb(tenant);
        return await new CandidateSearchRepository(db).SearchAsync(term, page, pageSize);
    }

    private async Task<List<Guid>> SeedAsync(
        FixedTenant tenant, params (string First, string Last, string Email)[] people)
    {
        await using var db = NewDb(tenant);
        var ids = new List<Guid>();

        foreach (var (first, last, email) in people)
        {
            var candidate = Candidate.Create(email, first, last);
            db.Candidates.Add(candidate);
            ids.Add(candidate.Id);
        }

        await db.SaveChangesAsync();
        return ids;
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}

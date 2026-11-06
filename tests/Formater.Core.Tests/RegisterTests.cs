using Formater.Core;

public sealed class RegisterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "formater-reg-" + Guid.NewGuid().ToString("N"));
    private QuotationRegister NewRegister() => new(Path.Combine(_dir, "register.json"));
    private const string P = Numbering.DefaultPattern;

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static string Issue(QuotationRegister reg, int fy, string? manual = null)
    {
        using var issue = reg.BeginIssue();
        var (reference, number) = manual is null ? issue.Allocate(P, fy) : (manual, 0);
        issue.Commit(new RegisterEntry { Ref = reference, Number = number, FyStart = fy, IssuedAt = DateTime.Now, Company = "X" });
        return reference;
    }

    [Fact]
    public void Numbers_count_up_and_are_saved()
    {
        var reg = NewRegister();
        Assert.Equal("T/2627/00001", reg.PeekNext(P, 2026));
        Assert.Equal("T/2627/00001", Issue(reg, 2026));
        Assert.Equal("T/2627/00002", Issue(reg, 2026));

        var again = NewRegister(); // read back from disk
        Assert.Equal("T/2627/00003", again.PeekNext(P, 2026));
        Assert.Equal(2, again.Entries().Count);
        Assert.Equal(2, again.LastUsedNumber(2026));
    }

    [Fact]
    public void Each_financial_year_restarts_at_one()
    {
        var reg = NewRegister();
        Issue(reg, 2026);
        Issue(reg, 2026);
        Assert.Equal("T/2728/00001", Issue(reg, 2027));
        Assert.Equal("T/2627/00003", reg.PeekNext(P, 2026));
    }

    [Fact]
    public void An_allocation_that_is_not_committed_uses_no_number()
    {
        var reg = NewRegister();
        using (var issue = reg.BeginIssue())
            Assert.Equal("T/2627/00001", issue.Allocate(P, 2026).Ref); // e.g. the PDF could not be written
        Assert.Equal("T/2627/00001", reg.PeekNext(P, 2026));
        Assert.Empty(reg.Entries());
    }

    [Fact]
    public void A_ref_can_never_be_issued_twice()
    {
        var reg = NewRegister();
        Issue(reg, 2026, "100186");
        Assert.NotNull(reg.Find(" 100186 "));
        Assert.Throws<InvalidOperationException>(() => Issue(reg, 2026, "100186"));
        Assert.Single(reg.Entries());
    }

    [Fact]
    public void Automatic_numbers_skip_refs_typed_by_hand()
    {
        var reg = NewRegister();
        Issue(reg, 2026, "t/2627/00001");
        Assert.Equal("T/2627/00002", Issue(reg, 2026));
    }

    [Fact]
    public void Next_number_can_move_forward_but_never_back_to_a_used_number()
    {
        var reg = NewRegister();
        Issue(reg, 2026);
        Issue(reg, 2026);
        reg.SetNextNumber(2026, 152, P);
        Assert.Equal("T/2627/00152", Issue(reg, 2026));
        var ex = Assert.Throws<InvalidOperationException>(() => reg.SetNextNumber(2026, 100, P));
        Assert.Contains("T/2627/00152", ex.Message);
        reg.SetNextNumber(2026, 153, P); // the next free number is fine
        Assert.Throws<InvalidOperationException>(() => reg.SetNextNumber(2026, 0, P));
    }

    [Fact]
    public void Cancelled_quotations_keep_their_number()
    {
        var reg = NewRegister();
        var r = Issue(reg, 2026);
        reg.Cancel(r, "Wrong rate");
        var entry = Assert.Single(reg.Entries());
        Assert.True(entry.Cancelled);
        Assert.Equal("Wrong rate", entry.CancelReason);
        Assert.Equal("T/2627/00002", reg.PeekNext(P, 2026));
    }

    [Fact]
    public async Task Two_issues_cannot_run_at_once()
    {
        var reg = NewRegister();
        var first = reg.BeginIssue();
        var waited = Task.Run(() =>
        {
            using var second = reg.BeginIssue();
            return second.Allocate(P, 2026).Ref;
        });
        // Blocked while the first holds the register.
        Assert.NotSame(waited, await Task.WhenAny(waited, Task.Delay(500)));
        first.Commit(new RegisterEntry { Ref = first.Allocate(P, 2026).Ref, Number = 1, FyStart = 2026 });
        first.Dispose();
        Assert.Same(waited, await Task.WhenAny(waited, Task.Delay(5000)));
        Assert.Equal("T/2627/00002", await waited);
    }
}

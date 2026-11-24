using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Tests.Provisioning;
using AbrIT.Panel.Domain.Workspaces;
using AbrIT.Panel.Nextcloud;

namespace AbrIT.Panel.Domain.Tests.Workspaces;

public class TeamWorkspaceTests
{
    private const long FiveHundredMb = 500L * 1024 * 1024;
    private const string Group = "abrit-team-1405-000001";

    private static async Task<(Harness H, Order Order)> ActiveTeamOrderAsync()
    {
        var h = new Harness();
        var order = await h.PlaceAsync(plan: Harness.Team);
        Assert.Equal(OrderStatus.Active, (await h.Approval.ApproveAsync(order.OrderId, Harness.Admin)).Status);
        return (h, order);
    }

    [Fact]
    public async Task Owner_is_seat_one_and_members_get_group_and_quota()
    {
        var (h, order) = await ActiveTeamOrderAsync();

        var member = await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "  Sara@Example.COM ");

        Assert.NotNull(member);
        Assert.Equal(2, member.MemberNumber);
        Assert.Equal("sara@example.com", member.Email);
        Assert.Equal("abrit-ord1405-000001-m2", member.ExternalUserId);
        Assert.Equal(WorkspaceMemberStatus.Active, member.Status);

        var account = await h.Mock.GetUserAsync(member.ExternalUserId, default);
        Assert.Equal(FiveHundredMb, account!.QuotaBytes);
        Assert.Equal(["abrit-ord1405-000001", "abrit-ord1405-000001-m2"], h.Mock.GroupMembers(Group));

        var view = await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId);
        Assert.Equal(2, view!.SeatsUsed);
        Assert.Equal(3, view.SeatLimit);
    }

    [Fact]
    public async Task Seat_cap_rejects_fourth_member()
    {
        var (h, order) = await ActiveTeamOrderAsync();
        await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com");
        await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m3@example.com");
        var accounts = h.Mock.CreateUserCalls;

        var ex = await Assert.ThrowsAsync<WorkspaceException>(
            () => h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m4@example.com"));

        Assert.Equal(WorkspaceError.SeatLimitReached, ex.Error);
        Assert.Equal(accounts, h.Mock.CreateUserCalls);
        Assert.Equal(3, (await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId))!.SeatsUsed);
    }

    [Theory]
    [InlineData("m2@example.com")]
    [InlineData("M2@EXAMPLE.com")]
    [InlineData("user-a@example.test")] // the owner's own e-mail
    public async Task Duplicate_email_is_rejected(string duplicate)
    {
        var (h, order) = await ActiveTeamOrderAsync();
        await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com");

        var ex = await Assert.ThrowsAsync<WorkspaceException>(
            () => h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, duplicate));

        Assert.Equal(WorkspaceError.DuplicateEmail, ex.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("Name <a@example.com>")]
    [InlineData("a@example.com, b@example.com")]
    public async Task Invalid_email_is_rejected(string email)
    {
        var (h, order) = await ActiveTeamOrderAsync();

        var ex = await Assert.ThrowsAsync<WorkspaceException>(
            () => h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, email));

        Assert.Equal(WorkspaceError.InvalidEmail, ex.Error);
    }

    [Fact]
    public async Task Parallel_adds_of_same_email_create_one_account()
    {
        var (h, order) = await ActiveTeamOrderAsync();
        var accountsBefore = h.Mock.CreateUserCalls;

        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            try
            {
                return await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "same@example.com");
            }
            catch (WorkspaceException ex) when (ex.Error == WorkspaceError.DuplicateEmail)
            {
                return null;
            }
        })));

        Assert.Single(attempts, m => m is not null);
        Assert.Equal(accountsBefore + 1, h.Mock.CreateUserCalls);
        var view = await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId);
        Assert.Single(view!.Members);
        Assert.Equal(2, view.SeatsUsed);
    }

    [Fact]
    public async Task Parallel_adds_never_exceed_the_cap()
    {
        var (h, order) = await ActiveTeamOrderAsync();

        await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Task.Run(async () =>
        {
            try
            {
                await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, $"p{i}@example.com");
            }
            catch (WorkspaceException ex) when (ex.Error == WorkspaceError.SeatLimitReached)
            {
            }
        })));

        var view = await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId);
        Assert.Equal(3, view!.SeatsUsed);
        Assert.Equal(2, view.Members.Count);
        Assert.Equal(3, h.Mock.GroupMembers(Group).Count);
    }

    [Fact]
    public async Task Remove_frees_a_seat_and_disables_the_account()
    {
        var (h, order) = await ActiveTeamOrderAsync();
        var m2 = await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com");
        await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m3@example.com");

        var removed = await h.Workspaces.RemoveMemberAsync(Harness.UserA, order.OrderId, m2!.MemberNumber);
        var again = await h.Workspaces.RemoveMemberAsync(Harness.UserA, order.OrderId, m2.MemberNumber);

        Assert.Equal(WorkspaceMemberStatus.Removed, removed!.Status);
        Assert.Equal(WorkspaceMemberStatus.Removed, again!.Status);
        Assert.Equal(1, h.Mock.DisableUserCalls);
        Assert.False((await h.Mock.GetUserAsync(m2.ExternalUserId, default))!.Enabled);
        Assert.DoesNotContain(m2.ExternalUserId, h.Mock.GroupMembers(Group));

        // Seat freed: a new member fits, gets a new number (ids are never reused) and the same e-mail may return.
        var m4 = await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com");
        Assert.Equal(4, m4!.MemberNumber);
        Assert.Equal(3, (await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId))!.SeatsUsed);
    }

    [Fact]
    public async Task Another_customer_cannot_see_add_or_remove_members()
    {
        var (h, order) = await ActiveTeamOrderAsync();
        var member = await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com");
        var accounts = h.Mock.CreateUserCalls;

        Assert.Null(await h.Workspaces.GetForOwnerAsync(Harness.UserB, order.OrderId));
        Assert.Null(await h.Workspaces.AddMemberAsync(Harness.UserB, order.OrderId, "intruder@example.com"));
        Assert.Null(await h.Workspaces.RemoveMemberAsync(Harness.UserB, order.OrderId, member!.MemberNumber));

        Assert.Equal(accounts, h.Mock.CreateUserCalls);
        Assert.Equal(0, h.Mock.DisableUserCalls);
        var view = await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId);
        Assert.Equal(WorkspaceMemberStatus.Active, view!.Members[0].Status);
    }

    [Fact]
    public async Task Starter_orders_have_no_workspace_actions()
    {
        var h = new Harness();
        var order = await h.PlaceAsync(plan: Harness.Starter);
        await h.Approval.ApproveAsync(order.OrderId, Harness.Admin);

        Assert.Null(await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId));
        Assert.Null(await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com"));
        Assert.Null(await h.Workspaces.RemoveMemberAsync(Harness.UserA, order.OrderId, 2));
        Assert.Equal(0, h.WorkspaceRepo.SaveCount);
    }

    [Fact]
    public async Task Team_order_that_is_not_active_refuses_members()
    {
        var h = new Harness();
        var order = await h.PlaceAsync(plan: Harness.Team);

        var ex = await Assert.ThrowsAsync<WorkspaceException>(
            () => h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com"));

        Assert.Equal(WorkspaceError.NotAvailable, ex.Error);
        Assert.Equal(0, h.Mock.CreateUserCalls);
    }

    [Fact]
    public async Task Connector_failure_keeps_seat_free_and_retry_reuses_external_id()
    {
        var (h, order) = await ActiveTeamOrderAsync();
        h.Mock.FailNextCalls = 1;

        var ex = await Assert.ThrowsAsync<WorkspaceException>(
            () => h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com"));
        Assert.Equal(WorkspaceError.ConnectorFailed, ex.Error);
        var view = await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId);
        Assert.Equal(1, view!.SeatsUsed);
        Assert.Equal(WorkspaceMemberStatus.Failed, view.Members[0].Status);

        var retried = await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com");
        Assert.Equal(2, retried!.MemberNumber);
        Assert.Equal(WorkspaceMemberStatus.Active, retried.Status);
        Assert.Single((await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId))!.Members);
    }

    [Fact]
    public async Task Failed_remove_keeps_member_active()
    {
        var (h, order) = await ActiveTeamOrderAsync();
        var member = await h.Workspaces.AddMemberAsync(Harness.UserA, order.OrderId, "m2@example.com");
        h.Mock.FailNextCalls = 1;

        var ex = await Assert.ThrowsAsync<WorkspaceException>(
            () => h.Workspaces.RemoveMemberAsync(Harness.UserA, order.OrderId, member!.MemberNumber));

        Assert.Equal(WorkspaceError.ConnectorFailed, ex.Error);
        Assert.Equal(MockNextcloudProvisioner.InjectedFailureCode, ex.ConnectorErrorCode);
        var view = await h.Workspaces.GetForOwnerAsync(Harness.UserA, order.OrderId);
        Assert.Equal(WorkspaceMemberStatus.Active, view!.Members[0].Status);
        Assert.Equal(2, view.SeatsUsed);
    }
}

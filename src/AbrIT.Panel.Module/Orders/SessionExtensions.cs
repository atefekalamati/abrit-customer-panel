using YesSql;

namespace AbrIT.Panel.Module.Orders;

internal static class SessionExtensions
{
    /// <summary>
    /// YesSql's identity map returns the instance already tracked by this session, even if another request has
    /// committed a newer version since. Detaching and re-querying yields the stored state; the fresh instance is
    /// tracked, so a later save updates the same document (DECISIONS D-12).
    /// </summary>
    public static async Task<T?> GetFreshAsync<T>(this ISession session, Func<Task<T>> query)
        where T : class
    {
        T? tracked = await query();
        if (tracked is null)
        {
            return null;
        }

        session.Detach(tracked);
        return await query();
    }
}

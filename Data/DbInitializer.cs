using wekdi.Data;

public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        context.Database.EnsureCreated();
        // Nothing else needed, you’ll create admins/members via the app
    }
}

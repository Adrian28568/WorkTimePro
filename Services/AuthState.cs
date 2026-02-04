namespace WorkTimePro.Api.Services
{
    public static class AuthState
    {
        // TEMP: logged-in user (worker1)
        public static int? CurrentUserId { get; set; } = 2;
        public static string Role { get; set; } = "Worker";
    }
}

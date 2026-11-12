namespace QuickEditer.Core
{
    public class AuthData
    {
        public string PasswordHash { get; set; } = "";
        public string PasswordSalt { get; set; } = "";
        public bool MustChangePassword { get; set; } = false;
        public int LoginFailCount { get; set; } = 0;
        public DateTime? LoginLockedUntil { get; set; }
    }
}

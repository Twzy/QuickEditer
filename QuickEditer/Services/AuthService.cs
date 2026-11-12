using System.Security.Cryptography;
using System.Text;
using QuickEditer.Core;

namespace QuickEditer.Services
{
    public class AuthService
    {
        private const int MaxFailCount = 5;
        private const int LockMinutes = 30;
        private const int CookieDays = 7;
        private const string InitialPassword = "quickadmin";
        private const int Pbkdf2Iterations = 100000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public bool IsLocked =>
            Config.Auth.LoginLockedUntil.HasValue &&
            Config.Auth.LoginLockedUntil.Value > DateTime.Now;

        public TimeSpan LockRemaining =>
            IsLocked ? Config.Auth.LoginLockedUntil!.Value - DateTime.Now : TimeSpan.Zero;

        public bool IsFirstLogin => string.IsNullOrEmpty(Config.Auth.PasswordHash);

        public bool MustChangePassword => Config.Auth.MustChangePassword || IsFirstLogin;

        public int CookieDaysValue => CookieDays;

        public void EnsureInitialPassword()
        {
            if (string.IsNullOrEmpty(Config.Auth.PasswordHash))
            {
                SetPassword(InitialPassword, mustChange: true);
            }
        }

        public void SetPassword(string password, bool mustChange = false)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Pbkdf2Hash(password, salt);

            Config.Auth.PasswordSalt = Convert.ToBase64String(salt);
            Config.Auth.PasswordHash = Convert.ToBase64String(hash);
            Config.Auth.MustChangePassword = mustChange;
            Config.Auth.LoginFailCount = 0;
            Config.Auth.LoginLockedUntil = null;
            Config.SaveAuth();
        }

        public (bool Ok, string Error) Login(string password)
        {
            if (IsLocked)
                return (false, $"登录已锁定，请 {Math.Ceiling(LockRemaining.TotalMinutes)} 分钟后再试。");

            if (string.IsNullOrEmpty(Config.Auth.PasswordHash))
                return (false, "未设置密码。");

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(Config.Auth.PasswordSalt);
                expected = Convert.FromBase64String(Config.Auth.PasswordHash);
            }
            catch
            {
                return (false, "密码数据损坏，请联系管理员。");
            }

            var actual = Pbkdf2Hash(password, salt);
            if (CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                Config.Auth.LoginFailCount = 0;
                Config.Auth.LoginLockedUntil = null;
                Config.SaveAuth();
                return (true, "");
            }

            Config.Auth.LoginFailCount++;
            if (Config.Auth.LoginFailCount >= MaxFailCount)
            {
                Config.Auth.LoginLockedUntil = DateTime.Now.AddMinutes(LockMinutes);
                Config.Auth.LoginFailCount = 0;
                Config.SaveAuth();
                return (false, $"密码错误次数过多，登录锁定 {LockMinutes} 分钟。");
            }

            Config.SaveAuth();
            return (false, $"密码错误，剩余 {MaxFailCount - Config.Auth.LoginFailCount} 次机会。");
        }

        public string GenerateAuthToken()
        {
            var raw = $"{Config.Auth.PasswordHash}|quickediter";
            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        }

        public bool ValidateAuthToken(string? token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (string.IsNullOrEmpty(Config.Auth.PasswordHash)) return false;
            return token == GenerateAuthToken();
        }

        private static byte[] Pbkdf2Hash(string password, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                HashSize);
        }

        public static (bool Ok, string Error) ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return (false, "密码至少 8 位");

            if (!password.Any(char.IsUpper))
                return (false, "密码需包含大写字母");

            if (!password.Any(char.IsLower))
                return (false, "密码需包含小写字母");

            if (!password.Any(char.IsDigit))
                return (false, "密码需包含数字");

            if (!password.Any(c => !char.IsLetterOrDigit(c)))
                return (false, "密码需包含特殊字符");

            return (true, "");
        }
    }
}
using System.Security.Cryptography;

namespace A1.Api.Utilities
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int DefaultIterations = 150_000;

        public static void Hash(
            string password,
            out byte[] hash,
            out byte[] salt,
            out int iterations)
        {
            iterations = DefaultIterations;
            salt = RandomNumberGenerator.GetBytes(SaltSize);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256);

            hash = pbkdf2.GetBytes(KeySize);
        }

        public static bool Verify(
            string password,
            byte[] storedHash,
            byte[] storedSalt,
            int iterations = DefaultIterations)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                storedSalt,
                iterations,
                HashAlgorithmName.SHA256);

            var computed = pbkdf2.GetBytes(KeySize);

            return CryptographicOperations.FixedTimeEquals(
                computed,
                storedHash);
        }
    }
}


using System;
using System.Security.Cryptography;
using System.Text;

namespace KioskAdmin.Services
{
    public static class PinGate
    {
        private const int Iterations = 100_000;
        private const int KeyLength = 32;

        public static (string Hash, string Salt) Create(string pin)
        {
            if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
            {
                throw new ArgumentException("PIN must be at least 4 characters", nameof(pin));
            }

            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Derive(pin, salt);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public static bool Verify(string pin, string storedHash, string storedSalt)
        {
            if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            {
                return false;
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(storedSalt);
                expected = Convert.FromBase64String(storedHash);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Derive(pin, salt);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static byte[] Derive(string pin, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, Iterations, HashAlgorithmName.SHA256, KeyLength);
        }
    }
}

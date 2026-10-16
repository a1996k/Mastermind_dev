using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;

namespace FifthPress.Controllers
{
    public class UserAccountController : DataController
    {
        private const int SaltSize = 16;
        private const int HashSize = 20;
        private const int Iterations = 10000;

        protected static string HashPassword(string password, out string salt)
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var saltBytes = new byte[SaltSize];
                rng.GetBytes(saltBytes);
                salt = Convert.ToBase64String(saltBytes);

                // Hash the password with the salt
                var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations);
                var hash = pbkdf2.GetBytes(HashSize);

                return Convert.ToBase64String(hash);
            }
        }

        protected static bool AreHashesEqual(byte[] firstHash, byte[] secondHash)
        {
            int minHashLength = Math.Min(firstHash.Length, secondHash.Length);
            var xor = firstHash.Length ^ secondHash.Length;
            for (int i = 0; i < minHashLength; i++)
                xor |= firstHash[i] ^ secondHash[i];
            return 0 == xor;
        }

        protected static bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            var saltBytes = Convert.FromBase64String(storedSalt);
            var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations);
            var hash = pbkdf2.GetBytes(HashSize);
            var storedHashBytes = Convert.FromBase64String(storedHash);

            // Compare the hashes
            return AreHashesEqual(storedHashBytes, hash);
        }
    }
}
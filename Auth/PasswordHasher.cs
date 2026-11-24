using System.Security.Cryptography;
using System.Text;

namespace BotArena.Auth;

public static class PasswordHasher
{
    public const int Iterations = 150_000;

    public static (byte[] Hash, byte[] Salt) Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        return (Derive(password, salt, Iterations), salt);
    }

    public static bool Verify(string password, byte[] hash, byte[] salt, int iterations) =>
        CryptographicOperations.FixedTimeEquals(Derive(password, salt, iterations), hash);

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);
}

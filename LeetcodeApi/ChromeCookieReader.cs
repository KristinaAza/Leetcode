using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LeetcodeApi
{
    // From https://stackoverflow.com/questions/60230456/dpapi-fails-with-cryptographicexception-when-trying-to-decrypt-chrome-cookies
    public class ChromeCookieReader
    {
        private readonly string _connectionString;
        private readonly byte[] _encryptionKey;

        public ChromeCookieReader()
        {
            var localApplicationDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var fullUserDataPath = Path.Combine(localApplicationDataFolder, @"Google\Chrome\User Data");

            _connectionString = GetConnectionString(fullUserDataPath);
            _encryptionKey = GetEncryptionKey(fullUserDataPath);
        }

        private static string GetConnectionString(string userDataPath)
        {
            var dbPath = Path.Combine(userDataPath, @"Default\Cookies");
            if (!File.Exists(dbPath))
                throw new FileNotFoundException("Can't find cookie store.", dbPath);

            return $"Data Source={dbPath}";
        }

        public Dictionary<string, string> ReadCookies(string hostName)
        {
            if (hostName == null)
                throw new ArgumentNullException(nameof(hostName));

            using var connection = new SqliteConnection(_connectionString);
            const string query = "SELECT name, encrypted_value FROM cookies WHERE host_key like @hostName";
            return connection.Query<(string name, byte[] encrypted)>(query, new { hostName = $"%{hostName}%" })
                .Select(n => (n.name, value: Decrypt(n.encrypted, _encryptionKey)))
                .ToDictionary(k => k.name, v => v.value);
        }

        private static byte[] GetEncryptionKey(string userDataPath)
        {
            var localStatePath = Path.Combine(userDataPath, "Local State");
            if (!File.Exists(localStatePath))
                throw new FileNotFoundException("Can't find local state store.", localStatePath);
            var localStateJson = File.ReadAllText(localStatePath);
            var encKey = GetEncryptedKeyFromLocalStateJson(localStateJson);
            var encryptedData = Convert.FromBase64String(encKey).Skip(5).ToArray();
            return ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.LocalMachine);
        }

        private static string GetEncryptedKeyFromLocalStateJson(string localStateJson)
        {
            var key = JObject.Parse(localStateJson)["os_crypt"]?["encrypted_key"]?.ToString();
            if (key == null)
                throw new InvalidDataException("Local State file doesn't have encryption key.");
            return key;
        }

        private static string Decrypt(byte[] message, byte[] key, int nonSecretPayloadLength = 3)
        {
            const int keyBitSize = 256;
            if (key == null || key.Length != keyBitSize / 8)
                throw new ArgumentException($"Key needs to be {keyBitSize} bit!", nameof(key));
            if (message == null || message.Length == 0)
                throw new ArgumentException("Message required!", nameof(message));

            using var cipherStream = new MemoryStream(message);
            using var cipherReader = new BinaryReader(cipherStream);

            cipherReader.ReadBytes(nonSecretPayloadLength);
            const int nonceBitSize = 96;
            var nonce = cipherReader.ReadBytes(nonceBitSize / 8);
            var cipher = new GcmBlockCipher(new AesEngine());
            const int macBitSize = 128;
            var parameters = new AeadParameters(new KeyParameter(key), macBitSize, nonce);
            cipher.Init(false, parameters);
            var cipherText = cipherReader.ReadBytes(message.Length);
            var plainText = new byte[cipher.GetOutputSize(cipherText.Length)];

            var len = cipher.ProcessBytes(cipherText, 0, cipherText.Length, plainText, 0);
            cipher.DoFinal(plainText, len);

            return Encoding.Default.GetString(plainText);
        }
    }
}
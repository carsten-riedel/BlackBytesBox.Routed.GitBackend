using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlackBytesBox.Routed.GitBackend.Tests.EphemeralClient
{
    /// <summary>
    /// A custom HTTP client that generates an ephemeral RSA key pair for each secure connection initiation.
    /// It sends the public key via the X-Client-PublicKey header, receives an encrypted JSON response,
    /// and stores both the secret from the decrypted JSON response and the server's public key.
    /// It also provides a method to POST data with an encrypted body using the stored secret.
    /// </summary>
    public class EphemeralSecureHttpClient
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Gets the server's public key received from the latest secure connection.
        /// </summary>
        public string ServerPublicKey { get; private set; } = String.Empty;

        /// <summary>
        /// Gets the secret received from the decrypted JSON response.
        /// This secret is used for encrypting subsequent POST request bodies.
        /// </summary>
        public string Secret { get; private set; } = String.Empty;

        private readonly string _initiateOrRefreshEndpoint;

        /// <summary>
        /// Private constructor. Use CreateAsync to instantiate and initialize.
        /// </summary>
        /// <param name="initiateOrRefreshEndpoint">The endpoint URL for initiating/refreshing the secure connection.</param>
        private EphemeralSecureHttpClient(string initiateOrRefreshEndpoint)
        {
            _httpClient = new HttpClient();
            _initiateOrRefreshEndpoint = initiateOrRefreshEndpoint;
        }

        /// <summary>
        /// Factory method to create an instance of EphemeralSecureHttpClient and initialize it asynchronously.
        /// </summary>
        /// <param name="initiateOrRefreshEndpoint">The endpoint URL for initiating/refreshing the secure connection.</param>
        /// <returns>An initialized instance of EphemeralSecureHttpClient.</returns>
        public static async Task<EphemeralSecureHttpClient> CreateAsync(string initiateOrRefreshEndpoint)
        {
            var client = new EphemeralSecureHttpClient(initiateOrRefreshEndpoint);
            await client.InitiateOrRefreshSecureConnectionAsync();
            return client;
        }

        /// <summary>
        /// Initiates or refreshes the secure connection by sending an ephemeral RSA public key to the server.
        /// The server responds with an encrypted JSON payload (containing a secret) and its public key.
        /// The encrypted response body is decrypted using the ephemeral private key, and the secret is extracted.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InitiateOrRefreshSecureConnectionAsync()
        {
            // Generate an ephemeral RSA key pair for this connection.
            using (RSA rsa = RSA.Create(2048))
            {
                // Extract the client's public key as XML.
                string clientPublicKeyXml = rsa.ToXmlString(false);

                // Build the HTTP request and include the client's public key.
                var request = new HttpRequestMessage(HttpMethod.Get, _initiateOrRefreshEndpoint);
                request.Headers.Add("X-Client-PublicKey", clientPublicKeyXml);

                // Send the request.
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                // Extract the server's public key from the response header.
                if (response.Headers.Contains("X-Server-PublicKey"))
                {
                    ServerPublicKey = string.Join("", response.Headers.GetValues("X-Server-PublicKey"));
                }
                else
                {
                    throw new Exception("Missing X-Server-PublicKey header in the response.");
                }

                // Read the encrypted response body (Base64-encoded string).
                string encryptedBodyBase64 = await response.Content.ReadAsStringAsync();

                // Convert Base64 to bytes and decrypt using the ephemeral RSA private key.
                byte[] encryptedBytes = Convert.FromBase64String(encryptedBodyBase64);
                byte[] decryptedBytes = rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.OaepSHA256);
                string decryptedJson = Encoding.UTF8.GetString(decryptedBytes);

                // Parse the JSON to extract the secret.
                // Expected JSON: { "secret": "password" }
                using (JsonDocument doc = JsonDocument.Parse(decryptedJson))
                {
                    if (doc.RootElement.TryGetProperty("secret", out JsonElement secretElement))
                    {
                        // Guard against a null string (if the secret property exists but is null).
                        Secret = secretElement.GetString() ?? throw new Exception("The 'secret' property is null.");
                    }
                    else
                    {
                        throw new Exception("JSON response does not contain 'secret' property.");
                    }
                }
            }
        }

        /// <summary>
        /// Sends an HTTP POST request with an encrypted body and an ephemeral RSA key pair in the header.
        /// The plaintext content is encrypted using AES with a key derived from the stored secret.
        /// The server is expected to encrypt its response using the provided ephemeral public key.
        /// This method then decodes the server's response using the corresponding ephemeral private key.
        /// </summary>
        /// <param name="requestUri">The URI to send the POST request to.</param>
        /// <param name="plainTextContent">The plaintext content to encrypt and send.</param>
        /// <returns>The decrypted plaintext response from the server.</returns>
        public async Task<string> PostSecureAndDecodeResponseAsync(string requestUri, string plainTextContent)
        {
            if (string.IsNullOrEmpty(Secret))
            {
                throw new InvalidOperationException("Secure connection not initiated. Secret is missing.");
            }

            // Derive a 256-bit AES key from the secret using SHA-256.
            byte[] key;
            using (SHA256 sha256 = SHA256.Create())
            {
                key = sha256.ComputeHash(Encoding.UTF8.GetBytes(Secret));
            }

            // Encrypt the plaintext content using AES.
            byte[] encryptedBytes = EncryptWithAes(plainTextContent, key, out byte[] iv);

            // Prepend the IV to the encrypted data.
            byte[] payloadBytes = new byte[iv.Length + encryptedBytes.Length];
            Buffer.BlockCopy(iv, 0, payloadBytes, 0, iv.Length);
            Buffer.BlockCopy(encryptedBytes, 0, payloadBytes, iv.Length, encryptedBytes.Length);

            // Convert the payload to Base64 for transmission.
            string encryptedPayloadBase64 = Convert.ToBase64String(payloadBytes);

            // Generate an ephemeral RSA key pair for this POST request.
            using (RSA ephemeralRsa = RSA.Create(2048))
            {
                string ephemeralPublicKeyXml = ephemeralRsa.ToXmlString(false);

                // Build the HTTP POST request and add the ephemeral public key in the header.
                var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                request.Headers.Add("X-Client-PublicKey", ephemeralPublicKeyXml);
                request.Content = new StringContent(encryptedPayloadBase64, Encoding.UTF8, "text/plain");

                // Send the POST request.
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                // Read the server's encrypted response body (Base64-encoded string).
                string encryptedResponseBase64 = await response.Content.ReadAsStringAsync();
                byte[] encryptedResponseBytes = Convert.FromBase64String(encryptedResponseBase64);

                // Decrypt the server's response using the ephemeral RSA private key.
                byte[] decryptedResponseBytes = ephemeralRsa.Decrypt(encryptedResponseBytes, RSAEncryptionPadding.OaepSHA256);
                string decryptedResponse = Encoding.UTF8.GetString(decryptedResponseBytes);

                return decryptedResponse;
            }
        }


        /// <summary>
        /// Encrypts the specified plaintext using AES in CBC mode with PKCS7 padding.
        /// A random IV is generated.
        /// </summary>
        /// <param name="plainText">The plaintext to encrypt.</param>
        /// <param name="key">The AES key.</param>
        /// <param name="iv">Outputs the randomly generated IV.</param>
        /// <returns>The encrypted bytes.</returns>
        private byte[] EncryptWithAes(string plainText, byte[] key, out byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();
                iv = aes.IV;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    return ms.ToArray();
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using Commons.DB;
using Microsoft.AspNetCore.Identity;

namespace Commons
{
    public class Client : NotifyingSerializable
    {
        [Key]
        [Column(TypeName = "BLOB")]
        public Guid ID { get; set; }

        private string _Name = "";
        public string Name { get => _Name; set => Set(ref _Name, value); }

        public string? PasswordHash { get; set; }
        public byte[]? EncryptedPrivateKey { get; set; }
        public byte[]? PublicKey { get; set; }

        [NotMapped]
        private byte[]? PrivateKey{ get; set; }

        [NotMapped]
        private RSA rsa = RSA.Create();

        [NotMapped]
        private PasswordHasher<Client> passwordHasher = new PasswordHasher<Client>();

        public virtual ICollection<Space> Spaces { get; set; } = new ObservableCollection<Space>();

        public Client() : base() { }
        public Client(byte[] buffer) : base(buffer) { }
        public Client(byte[] buffer, ref int offset) : base(buffer, ref offset) { }

        public void SetPassword(string password)
        {
            PasswordHash = passwordHasher.HashPassword(this, password);

            // TODO: Re-encrypt private key when password changes
        }

        public void GenerateKeys(string password)
        {
            PbeParameters privateKeyEncryptionParameters = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000);
            EncryptedPrivateKey = rsa.ExportEncryptedPkcs8PrivateKey(password, privateKeyEncryptionParameters);
            PrivateKey = rsa.ExportPkcs8PrivateKey();
            PublicKey = rsa.ExportRSAPublicKey();
        }

        public void DecryptPrivateKey(string password)
        {
            rsa.ImportEncryptedPkcs8PrivateKey(password, EncryptedPrivateKey, out int numBytesRead);
            PrivateKey = rsa.ExportPkcs8PrivateKey();
        }

        public bool ValidatePassword(string password)
        {
            if (PasswordHash == null) throw new NullReferenceException(nameof(PasswordHash));

            PasswordVerificationResult result = passwordHasher.VerifyHashedPassword(this, PasswordHash, password);
            return result == PasswordVerificationResult.Success;
        }

        public override int Serialize(byte[] buffer, ref int offset)
        {
            if (PublicKey == null) throw new NullReferenceException(nameof(PublicKey));

            int startingOffset = offset;

            // ID
            ID.TryWriteBytes(buffer);
            offset += GUID_LENGTH;

            // Public key length
            BitConverter.TryWriteBytes(new Span<byte>(buffer, offset, sizeof(int)), PublicKey.Length);
            offset += sizeof(int);

            // Public key
            Array.Copy(PublicKey, 0, buffer, offset, PublicKey.Length);
            offset += PublicKey.Length;

            // Name
            int numNameBytes = Encoding.UTF8.GetBytes(Name, 0, Name.Length, buffer, GUID_LENGTH);
            offset += numNameBytes;
            return offset - startingOffset;
        }

        public override void Deserialize(byte[] buffer, ref int offset)
        {
            // ID
            ID = new Guid(new ReadOnlySpan<byte>(buffer, offset, GUID_LENGTH));
            offset += GUID_LENGTH;

            // Public Key length
            int publicKeyLength = BitConverter.ToInt32(buffer, offset);
            offset += sizeof(int);

            // Public key
            PublicKey = new byte[publicKeyLength];
            Array.Copy(buffer, offset, PublicKey, 0, publicKeyLength);
            offset += publicKeyLength;

            // Name
            Name = Encoding.UTF8.GetString(buffer, offset, buffer.Length - GUID_LENGTH);
            offset += buffer.Length - GUID_LENGTH;
        }
    }
}

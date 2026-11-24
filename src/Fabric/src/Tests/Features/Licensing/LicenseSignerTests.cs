using System.Security.Cryptography;
using System.Text;
using Bit.Fabric.Server.Api.Features.Licensing;

namespace Bit.Fabric.Tests.Features.Licensing;

/// <summary>
/// The signing key comes from an environment secret, made with whatever tool was at hand: <c>openssl pkey -outform DER</c>
/// writes an EC key as SEC1, .NET and <c>openssl pkcs8</c> as PKCS#8. Both are the same key, and the server must start with
/// either, rather than fail its deployment on the encoding.
/// </summary>
[TestClass, TestCategory("UnitTest")]
public class LicenseSignerTests
{
    [TestMethod]
    public void AKeyInEitherEncoding_Should_SignWhatItsPublicHalfChecks()
    {
        using var original = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = original.ExportSubjectPublicKeyInfo();
        var data = Encoding.ASCII.GetBytes("bfl1.payload");

        foreach (var der in new[] { original.ExportPkcs8PrivateKey(), original.ExportECPrivateKey() })
        {
            using var imported = LicenseSigner.ImportPrivateKey(der);
            Assert.AreSequenceEqual(publicKey, imported.ExportSubjectPublicKeyInfo());

            var signature = imported.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            Assert.IsTrue(original.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }
    }
}

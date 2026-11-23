using System.Net;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// bit Fabric opens addresses its customers type, every minute; one that resolves to the server's own network would let
/// anyone who adds a project reach it (SSRF). A plain unit test: the guard is a pure function of the address.
/// </summary>
[TestClass, TestCategory("UnitTest")]
public partial class PublicAddressGuardTests
{
    [TestMethod]
    [DataRow("10.0.0.5"), DataRow("127.0.0.1"), DataRow("0.0.0.0"), DataRow("169.254.169.254"), DataRow("172.16.0.1"),
     DataRow("172.31.255.255"), DataRow("192.168.1.1"), DataRow("100.64.0.1"), DataRow("198.18.0.1"), DataRow("192.0.2.10"),
     DataRow("224.0.0.1"), DataRow("255.255.255.255"), DataRow("::1"), DataRow("fe80::1"), DataRow("fd00::1"),
     DataRow("::ffff:10.0.0.5"), DataRow("64:ff9b::a00:5"), DataRow("2001:db8::1"), DataRow("ff02::1")]
    public void AnInternalAddress_Should_NotBePublic(string address)
    {
        Assert.IsFalse(PublicAddressGuard.IsPublic(IPAddress.Parse(address)), address);
    }

    [TestMethod]
    [DataRow("1.1.1.1"), DataRow("8.8.8.8"), DataRow("172.32.0.1"), DataRow("100.128.0.1"), DataRow("2606:4700:4700::1111"),
     DataRow("::ffff:8.8.8.8"), DataRow("64:ff9b::808:808")]
    public void APublicAddress_Should_BePublic(string address)
    {
        Assert.IsTrue(PublicAddressGuard.IsPublic(IPAddress.Parse(address)), address);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using GymApp.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace GymApp.Tests.Services;

public class EfiServiceTests
{
    private static EfiOptions ValidOptions() => new()
    {
        ClientId = "client123",
        ClientSecret = "secret456",
        PixKey = "chave@pix.com",
        CertificateBase64 = Convert.ToBase64String(new byte[] { 0x01, 0x02, 0x03 }),
        CertificatePassword = "certpass",
        PlatformPayeeCode = "platform123",
        PlatformFeePercent = 10,
        Sandbox = true
    };

    [Fact]
    public void IsConfigured_WhenAllRequiredFieldsPresent_ReturnsTrue()
    {
        var opts = ValidOptions();
        var logger = new Mock<ILogger<EfiService>>();
        var service = new EfiService(opts, logger.Object);

        service.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void IsConfigured_WhenClientIdEmpty_ReturnsFalse()
    {
        var opts = ValidOptions();
        opts.ClientId = string.Empty;
        var logger = new Mock<ILogger<EfiService>>();
        var service = new EfiService(opts, logger.Object);

        service.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_WhenClientSecretEmpty_ReturnsFalse()
    {
        var opts = ValidOptions();
        opts.ClientSecret = string.Empty;
        var logger = new Mock<ILogger<EfiService>>();
        var service = new EfiService(opts, logger.Object);

        service.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_WhenPixKeyEmpty_ReturnsFalse()
    {
        var opts = ValidOptions();
        opts.PixKey = string.Empty;
        var logger = new Mock<ILogger<EfiService>>();
        var service = new EfiService(opts, logger.Object);

        service.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_WhenClientIdNull_ReturnsFalse()
    {
        var opts = ValidOptions();
        opts.ClientId = null!;
        var logger = new Mock<ILogger<EfiService>>();
        var service = new EfiService(opts, logger.Object);

        service.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_WhenOnlyCertificateEmpty_StillTrue()
    {
        var opts = ValidOptions();
        opts.CertificateBase64 = string.Empty;
        var logger = new Mock<ILogger<EfiService>>();
        var service = new EfiService(opts, logger.Object);

        service.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void EfiChargeResult_ContainsAllFields()
    {
        var result = new EfiChargeResult("tx123", "pixcode456", "base64qr789");

        result.TxId.Should().Be("tx123");
        result.PixCopyPaste.Should().Be("pixcode456");
        result.QrCodeBase64.Should().Be("base64qr789");
    }
}
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GymApp.Api.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Xunit;

namespace GymApp.Tests.Services;

public class EvolutionApiServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly IConfiguration _configuration;
    private readonly EvolutionApiService _sut;

    public EvolutionApiServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Evolution:ApiKey"] = "test-api-key"
            })
            .Build();
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        httpClient.BaseAddress = new Uri("http://evolution-api.example.com");
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Evolution"))
            .Returns(httpClient);
        _sut = new EvolutionApiService(_httpClientFactoryMock.Object, _configuration);
    }

    #region CreateInstanceAsync Tests

    [Fact]
    public async Task CreateInstanceAsync_WithValidPhoneNumber_StripsFormatting()
    {
        var instanceName = "test-instance";
        var phoneNumber = "+55 11 99999-9999";
        var expectedNormalizedNumber = "5511999999999";

        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await _sut.CreateInstanceAsync(instanceName, phoneNumber);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.PathAndQuery.Should().Be("/instance/create");

        var content = await capturedRequest.Content!.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("instanceName").GetString().Should().Be(instanceName);
        content.GetProperty("number").GetString().Should().Be(expectedNormalizedNumber);
        content.GetProperty("integration").GetString().Should().Be("WHATSAPP-BAILEYS");
        content.GetProperty("qrcode").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("+1 234 567-8900", "12345678900")]
    [InlineData("11987654321", "11987654321")]
    [InlineData("+55-21-99999-8888", "5521999998888")]
    public async Task CreateInstanceAsync_VariousPhoneNumberFormats_NormalizesCorrectly(
        string input, string expected)
    {
        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await _sut.CreateInstanceAsync("instance", input);

        var content = await capturedRequest!.Content!.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("number").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task CreateInstanceAsync_NonSuccessStatus_ThrowsInvalidOperationException()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Instance creation failed")
            });

        var act = () => _sut.CreateInstanceAsync("test-instance", "5511999999999");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Evolution API error*BadRequest*Instance creation failed*");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CreateInstanceAsync_VariousErrorStatuses_ThrowsException(
        HttpStatusCode statusCode)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("error")
            });

        var act = () => _sut.CreateInstanceAsync("test-instance", "5511999999999");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(string.Format("*{0}*{1}*", "Evolution API error", statusCode));
    }

    #endregion

    #region DeleteInstanceAsync Tests

    [Fact]
    public async Task DeleteInstanceAsync_BestEffort_DoesNotThrowOnError()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = () => _sut.DeleteInstanceAsync("test-instance");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteInstanceAsync_Success_DoesNotThrow()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var act = () => _sut.DeleteInstanceAsync("test-instance");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteInstanceAsync_SendsDeleteRequest()
    {
        var instanceName = "my-instance";
        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await _sut.DeleteInstanceAsync(instanceName);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Delete);
        capturedRequest.RequestUri!.PathAndQuery.Should().Be(string.Format("/instance/delete/{0}", instanceName));
    }

    #endregion

    #region GetInstanceStateAsync Tests

    [Fact]
    public async Task GetInstanceStateAsync_Success_ReturnsState()
    {
        var expectedState = "CONNECTED";
        var responseJson = JsonSerializer.Serialize(new { state = expectedState });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });

        var result = await _sut.GetInstanceStateAsync("test-instance");

        result.Should().Be(expectedState);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetInstanceStateAsync_NonSuccessStatus_ReturnsNull(HttpStatusCode statusCode)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));

        var result = await _sut.GetInstanceStateAsync("test-instance");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInstanceStateAsync_SuccessWithNullStateInJson_ReturnsNull()
    {
        var responseJson = JsonSerializer.Serialize(new { state = (string?)null });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });

        var result = await _sut.GetInstanceStateAsync("test-instance");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInstanceStateAsync_SendsGetRequest()
    {
        var instanceName = "my-instance";
        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"state\":\"CONNECTED\"}")
            });

        await _sut.GetInstanceStateAsync(instanceName);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Get);
        capturedRequest.RequestUri!.PathAndQuery.Should().Be(string.Format("/instance/connectionState/{0}", instanceName));
    }

    #endregion
}

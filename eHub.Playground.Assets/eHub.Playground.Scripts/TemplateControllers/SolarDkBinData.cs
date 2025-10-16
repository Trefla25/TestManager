using System.Text;
using System.Text.Json;
using eController.Util.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace eHub.Playground.Scripts.TemplateControllers;

[ApiController]
[Route("api/bin/data")]
public class TestController : ControllerBase
{

    private readonly ILogger<TestController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public TestController(ILogger<TestController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> ReceiveBinData(BinData Data)
    {
        _logger.LogTrace($"[{RouteData}]: Called");
        using var httpClient = _httpClientFactory.CreateClient();
        var Username = "test";
        var Password = "test";
        httpClient.DefaultRequestHeaders.Add($"Authorization", $"Basic {Base64Encode($"{Username}:{Password}")}");
        string jsonString = JsonSerializer.Serialize(Data, SharedJsonOptions.Default);
        var postback = JsonSerializer.Serialize(new BinLocationData { BinId = Data.BinId, BinLocation = "IN", Payload = jsonString, TransactionId = Data.TransactionId }, SharedJsonOptions.Default);
        var payload = new StringContent(postback, Encoding.UTF8, "application/json");
        HttpResponseMessage httpResponseMessage = await httpClient.PostAsync("https://postman-echo.com/post", payload);
        var ret = await httpResponseMessage.Content.ReadAsStringAsync();
        return Ok(postback);
    }
    public static string Base64Encode(string textToEncode)
    {
        byte[] textAsBytes = Encoding.UTF8.GetBytes(textToEncode);
        return Convert.ToBase64String(textAsBytes);
    }
    public class BinData
    {
        public int TransactionId { get; set; }
        public int BinId { get; set; }
        public string Payload { get; set; }
    }
    public class BinLocationData
    {
        public int TransactionId { get; set; }
        public int BinId { get; set; }
        public string BinLocation { get; set; }
        public string Payload { get; set; }
    }
}

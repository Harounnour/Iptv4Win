using System.Net;
using Iptv4Win.Models;
using Iptv4Win.Services;
using Xunit;

namespace Iptv4Win.Tests;

public sealed class StalkerClientTests
{
    [Fact]
    public void RejectsMalformedMacAddress()
    {
        var source = new PlaylistSource { Type=PlaylistType.Stalker, Url="http://portal.example/c/", Mac="00:1A:79:AB:CD" };
        Assert.Throws<InvalidOperationException>(() => new StalkerClient(source, new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))));
    }

    [Fact]
    public async Task FallsBackToPortalPhpAndLoadsChannels()
    {
        var requests = new List<string>();
        var ipHeaders = new List<string>();
        var handler = new FakeHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            requests.Add(url);
            if (request.Headers.TryGetValues("X-Device-IP", out var ipValues)) ipHeaders.AddRange(ipValues);
            if (url.Contains("/server/load.php", StringComparison.OrdinalIgnoreCase))
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            var query = request.RequestUri.Query;
            var json = query.Contains("handshake") ? """{"js":{"token":"unit-test-token"}}"""
                : query.Contains("get_profile") ? """{"js":{"id":"device"}}"""
                : query.Contains("get_genres") ? """{"js":[{"id":"1","title":"News"}]}"""
                : query.Contains("get_all_channels") ? """{"js":{"data":[{"number":"1","name":"News HD","tv_genre_id":"1","cmd":"ffmpeg http://stream.example/live"}]}}"""
                : """{"js":{}}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });
        using var http = new HttpClient(handler);
        using var client = new StalkerClient(new PlaylistSource
        {
            Type=PlaylistType.Stalker, Url="http://portal.example/c/", Mac="00:1A:79:AA:BB:CC",
            SerialNumber="SERIAL", DeviceId="DEVICE1", DeviceId2="DEVICE2", IpAddress="192.0.2.15"
        }, http);

        var channels = await client.GetChannelsAsync();

        Assert.Single(channels);
        Assert.Equal("News HD", channels[0].Name);
        Assert.Equal("News", channels[0].Group);
        Assert.Contains(requests, x => x.Contains("/portal.php", StringComparison.OrdinalIgnoreCase) && x.Contains("handshake"));
        Assert.Contains("192.0.2.15", ipHeaders);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}

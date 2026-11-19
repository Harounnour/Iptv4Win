package com.iptv4win.tv

import android.net.Uri
import org.json.JSONArray
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import java.util.Locale

class IptvRepository {
    private fun get(url: String, headers: Map<String, String> = emptyMap()): String {
        val connection = URL(url).openConnection() as HttpURLConnection
        connection.connectTimeout = 20000
        connection.readTimeout = 30000
        connection.instanceFollowRedirects = true
        connection.setRequestProperty("User-Agent", "IPTV4WinTV/1.0 Android")
        headers.forEach { (k, v) -> connection.setRequestProperty(k, v) }
        try {
            val code = connection.responseCode
            val stream = if (code in 200..299) connection.inputStream else connection.errorStream
            val body = stream?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }.orEmpty()
            if (code !in 200..299) error("HTTP $code : $body")
            return body
        } finally { connection.disconnect() }
    }

    fun load(source: PlaylistSource): List<TvChannel> = when (source.type) {
        "M3U" -> parseM3u(get(source.url))
        "Xtream" -> loadXtream(source)
        "Stalker" -> loadStalker(source)
        else -> error("Format de playlist non pris en charge")
    }

    private fun parseM3u(text: String): List<TvChannel> {
        val result = mutableListOf<TvChannel>()
        var name = ""
        var group = ""
        var number = 1
        text.lineSequence().forEach { raw ->
            val line = raw.trim()
            when {
                line.startsWith("#EXTINF", true) -> {
                    name = line.substringAfterLast(',').trim().ifBlank { "Chaîne $number" }
                    group = Regex("group-title=\"([^\"]*)\"", RegexOption.IGNORE_CASE).find(line)?.groupValues?.get(1).orEmpty()
                }
                line.isNotBlank() && !line.startsWith("#") -> {
                    if (line.startsWith("http://") || line.startsWith("https://") || line.startsWith("rtsp://")) {
                        result += TvChannel(number++, name.ifBlank { line }, group, classify(name, group), line)
                    }
                    name = ""; group = ""
                }
            }
        }
        return result
    }

    private fun classify(name: String, group: String): String {
        val value = "$name $group".lowercase(Locale.ROOT)
        return when {
            listOf("series", "série", "serie").any(value::contains) -> "Series"
            listOf("film", "movie", "vod", "cinema").any(value::contains) -> "Films"
            else -> "Live"
        }
    }

    private fun loadXtream(s: PlaylistSource): List<TvChannel> {
        val base = s.url.trimEnd('/')
        val auth = "username=${enc(s.username)}&password=${enc(s.password)}"
        val root = JSONObject(get("$base/player_api.php?$auth"))
        val result = mutableListOf<TvChannel>()
        val liveCats = categories(get("$base/player_api.php?$auth&action=get_live_categories"))
        val live = JSONArray(get("$base/player_api.php?$auth&action=get_live_streams"))
        for (i in 0 until live.length()) {
            val x = live.getJSONObject(i); val id = x.optString("stream_id")
            result += TvChannel(x.optInt("num", i + 1), x.optString("name"), liveCats[x.optString("category_id")].orEmpty(), "Live",
                "$base/live/${enc(s.username)}/${enc(s.password)}/$id.${x.optString("container_extension", "ts")}")
        }
        val vodCats = categories(get("$base/player_api.php?$auth&action=get_vod_categories"))
        val vod = JSONArray(get("$base/player_api.php?$auth&action=get_vod_streams"))
        for (i in 0 until vod.length()) {
            val x = vod.getJSONObject(i); val id = x.optString("stream_id")
            result += TvChannel(i + 1, x.optString("name"), vodCats[x.optString("category_id")].orEmpty(), "Films",
                "$base/movie/${enc(s.username)}/${enc(s.password)}/$id.${x.optString("container_extension", "mp4")}")
        }
        val seriesCats = categories(get("$base/player_api.php?$auth&action=get_series_categories"))
        val series = JSONArray(get("$base/player_api.php?$auth&action=get_series"))
        for (i in 0 until series.length()) {
            val x = series.getJSONObject(i)
            result += TvChannel(i + 1, x.optString("name"), seriesCats[x.optString("category_id")].orEmpty(), "Series", "", x.optString("series_id"))
        }
        return result
    }

    private fun categories(json: String): Map<String, String> {
        val a = JSONArray(json); val out = mutableMapOf<String, String>()
        for (i in 0 until a.length()) { val x = a.getJSONObject(i); out[x.optString("category_id")] = x.optString("category_name") }
        return out
    }

    private fun loadStalker(s: PlaylistSource): List<TvChannel> {
        require(s.mac.matches(Regex("(?i)^([0-9a-f]{2}:){5}[0-9a-f]{2}$"))) { "Adresse MAC Stalker invalide." }
        val base = s.url.trimEnd('/').removeSuffix("/server/load.php").removeSuffix("/portal.php").removeSuffix("/c")
        val endpointCandidates = listOf("$base/server/load.php", "$base/portal.php")
        val referer = "$base/c/"
        val ua = "Mozilla/5.0 (QtEmbedded; U; Linux; C) AppleWebKit/533.3 (KHTML, like Gecko) MAG250 stbapp ver: 2 rev: 250 Safari/533.3"
        var token = ""
        var active = endpointCandidates.first()
        fun stalkerGet(query: String): JSONObject {
            var failure: Exception? = null
            for (endpoint in listOf(active) + endpointCandidates.filter { it != active }) {
                try {
                    val cookie = "mac=${s.mac}; stb_lang=fr; timezone=Europe/Paris" + if (token.isNotEmpty()) "; token=$token" else ""
                    val headers = mutableMapOf("User-Agent" to ua, "X-User-Agent" to "Model: MAG250; Link: WiFi", "Referer" to referer, "Cookie" to cookie)
                    if (token.isNotEmpty()) headers["Authorization"] = "Bearer $token"
                    if (s.serial.isNotBlank()) headers["X-Serial-Number"] = s.serial
                    if (s.deviceId.isNotBlank()) headers["X-Device-ID"] = s.deviceId
                    if (s.deviceId2.isNotBlank()) headers["X-Device-ID2"] = s.deviceId2
                    val root = JSONObject(get("$endpoint?$query&JsHttpRequest=1-xml", headers))
                    val js = root.opt("js")
                    val data = if (js is JSONObject) js else if (js is String) JSONObject(js) else JSONObject()
                    active = endpoint
                    return data
                } catch (e: Exception) { failure = e }
            }
            throw IllegalStateException("Connexion au portail Stalker impossible : ${failure?.message}")
        }
        token = stalkerGet("type=stb&action=handshake&token=").optString("token")
        require(token.isNotBlank()) { "Handshake Stalker refusé. Vérifiez MAC et portail." }
        stalkerGet("type=stb&action=get_profile")
        val genresJson = stalkerGet("type=itv&action=get_genres")
        val genres = mutableMapOf<String, String>()
        val genreArray = genresJson.optJSONArray("data") ?: JSONArray()
        for (i in 0 until genreArray.length()) { val g = genreArray.getJSONObject(i); genres[g.optString("id")] = g.optString("title") }
        val channelsJson = stalkerGet("type=itv&action=get_all_channels")
        val data = channelsJson.optJSONArray("data") ?: channelsJson.optJSONObject("data")?.optJSONArray("data") ?: channelsJson.optJSONArray("js") ?: JSONArray()
        val out = mutableListOf<TvChannel>()
        for (i in 0 until data.length()) {
            val c = data.getJSONObject(i)
            out += TvChannel(c.optInt("number", i + 1), c.optString("name"), genres[c.optString("tv_genre_id")].orEmpty(),
                "Live", "", c.optString("cmd"))
        }
        return out.map { it.copy(streamUrl = if (it.stalkerCmd.isBlank()) "" else "stalker://${Uri.encode(it.stalkerCmd)}") }
    }

    fun resolveStalker(source: PlaylistSource, command: String): Pair<String, Map<String, String>> {
        val base = source.url.trimEnd('/').removeSuffix("/server/load.php").removeSuffix("/portal.php").removeSuffix("/c")
        val referer = "$base/c/"
        val ua = "Mozilla/5.0 (QtEmbedded; U; Linux; C) AppleWebKit/533.3 (KHTML, like Gecko) MAG250 stbapp ver: 2 rev: 250 Safari/533.3"
        val endpoint = listOf("$base/server/load.php", "$base/portal.php")
        var token = ""
        fun request(query: String): JSONObject {
            var error: Exception? = null
            endpoint.forEach { ep ->
                try {
                    val headers = mutableMapOf("User-Agent" to ua, "X-User-Agent" to "Model: MAG250; Link: WiFi", "Referer" to referer,
                        "Cookie" to "mac=${source.mac}; stb_lang=fr; timezone=Europe/Paris" + if (token.isNotEmpty()) "; token=$token" else "")
                    if (token.isNotEmpty()) headers["Authorization"] = "Bearer $token"
                    if (source.serial.isNotBlank()) headers["X-Serial-Number"] = source.serial
                    if (source.deviceId.isNotBlank()) headers["X-Device-ID"] = source.deviceId
                    if (source.deviceId2.isNotBlank()) headers["X-Device-ID2"] = source.deviceId2
                    val root = JSONObject(get("$ep?$query&JsHttpRequest=1-xml", headers))
                    val js = root.opt("js")
                    return if (js is JSONObject) js else JSONObject(js?.toString().orEmpty())
                } catch (e: Exception) { error = e }
            }
            throw IllegalStateException("Échec Stalker : ${error?.message}")
        }
        token = request("type=stb&action=handshake&token=").optString("token")
        require(token.isNotBlank()) { "Handshake Stalker refusé." }
        val linkData = request("type=itv&action=create_link&cmd=${enc(command)}&series=&forced_storage=0&disable_ad=0&download=0")
        var stream = linkData.optString("cmd").ifBlank { linkData.optString("url") }.trim().removePrefix("ffmpeg ").removePrefix("auto ").trim('"', '\'')
        if (stream.startsWith("//")) stream = "http:$stream"
        require(stream.startsWith("http://") || stream.startsWith("https://") || stream.startsWith("rtsp://")) { "URL de flux Stalker invalide." }
        return stream to mapOf("User-Agent" to ua, "Referer" to referer, "Cookie" to "mac=${source.mac}; stb_lang=fr; timezone=Europe/Paris; token=$token", "Authorization" to "Bearer $token")
    }

    private fun enc(value: String) = URLEncoder.encode(value, "UTF-8")
}
